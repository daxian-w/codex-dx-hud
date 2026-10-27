using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace CodexRateMonitorNative
{
    internal sealed class UpdateInfo
    {
        public string Version { get; set; }
        public string ReleaseName { get; set; }
        public string Notes { get; set; }
        public string ReleaseUrl { get; set; }
        public string PackageName { get; set; }
        public string PackageUrl { get; set; }
        public string ChecksumsUrl { get; set; }
        public DateTimeOffset? PublishedAt { get; set; }
    }

    internal sealed class UpdateCheckResult
    {
        public bool Success { get; set; }
        public bool UpdateAvailable { get; set; }
        public string Error { get; set; }
        public UpdateInfo Info { get; set; }
    }

    internal sealed class UpdateChecker : IDisposable
    {
        private const string LatestReleaseApi =
            "https://api.github.com/repos/daxian-w/codex-huds/releases/latest";
        private const string LatestReleasePage =
            "https://github.com/daxian-w/codex-huds/releases/latest";
        private static readonly TimeSpan BackgroundInterval = TimeSpan.FromHours(6);

        private readonly string currentVersion;
        private readonly System.Threading.Timer timer;
        private int checking;
        private int manualRequested;
        private bool disposed;

        public event Action<UpdateCheckResult, bool> CheckCompleted;

        public UpdateChecker(string version)
        {
            currentVersion = version;
            timer = new System.Threading.Timer(
                delegate { Check(false); },
                null,
                TimeSpan.FromSeconds(10),
                BackgroundInterval);
        }

        public void Check(bool manual)
        {
            if (disposed)
                return;
            if (Interlocked.Exchange(ref checking, 1) != 0)
            {
                if (manual)
                    Interlocked.Exchange(ref manualRequested, 1);
                return;
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                UpdateCheckResult result;
                try
                {
                    result = ReadLatestRelease(currentVersion);
                }
                catch (Exception ex)
                {
                    result = new UpdateCheckResult
                    {
                        Success = false,
                        Error = DescribeFailure(ex)
                    };
                }
                finally
                {
                    Interlocked.Exchange(ref checking, 0);
                }

                bool effectiveManual = manual || Interlocked.Exchange(ref manualRequested, 0) != 0;
                Action<UpdateCheckResult, bool> handler = CheckCompleted;
                if (handler != null && !disposed)
                    handler(result, effectiveManual);
            });
        }

        internal static UpdateCheckResult ParseReleaseResponse(
            string currentVersion,
            string json)
        {
            var serializer = new JavaScriptSerializer();
            var root = serializer.DeserializeObject(json) as Dictionary<string, object>;
            if (root == null)
                throw new InvalidDataException("Invalid release response.");

            string tag = GetString(root, "tag_name");
            string latestVersion = NormalizeVersion(tag);
            Version current;
            Version latest;
            if (!Version.TryParse(NormalizeVersion(currentVersion), out current) ||
                !Version.TryParse(latestVersion, out latest))
                throw new InvalidDataException("Invalid release version.");

            var info = new UpdateInfo
            {
                Version = latestVersion,
                ReleaseName = GetString(root, "name"),
                Notes = GetString(root, "body") ?? string.Empty,
                ReleaseUrl = GetString(root, "html_url")
            };

            DateTimeOffset published;
            if (DateTimeOffset.TryParse(
                GetString(root, "published_at"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out published))
                info.PublishedAt = published;

            var assets = GetList(root, "assets");
            string expectedPackage = "CodexRateMonitor-" + latestVersion + "-windows-x64.zip";
            foreach (object raw in assets)
            {
                var asset = raw as Dictionary<string, object>;
                if (asset == null)
                    continue;
                string name = GetString(asset, "name") ?? string.Empty;
                string url = GetString(asset, "browser_download_url");
                if (string.Equals(name, expectedPackage, StringComparison.OrdinalIgnoreCase))
                {
                    info.PackageName = name;
                    info.PackageUrl = url;
                }
                else if (string.Equals(name, "SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase))
                {
                    info.ChecksumsUrl = url;
                }
            }

            return new UpdateCheckResult
            {
                Success = true,
                UpdateAvailable = latest > current,
                Info = info
            };
        }

        private static UpdateCheckResult ReadLatestRelease(string currentVersion)
        {
            try
            {
                string json = DownloadText(LatestReleaseApi, 15000);
                return ParseReleaseResponse(currentVersion, json);
            }
            catch (WebException)
            {
                // GitHub's unauthenticated API quota is shared by public IP. The
                // regular releases redirect is not API-rate-limited, so it keeps
                // update detection working even when the API returns HTTP 403.
                return ReadLatestReleaseFromRedirect(currentVersion);
            }
        }

        private static UpdateCheckResult ReadLatestReleaseFromRedirect(string currentVersion)
        {
            var request = CreateRequest(LatestReleasePage, 15000);
            request.AllowAutoRedirect = false;
            using (var response = (HttpWebResponse)request.GetResponse())
            {
                string location = response.Headers[HttpResponseHeader.Location];
                Uri releaseUri;
                if (string.IsNullOrWhiteSpace(location) ||
                    !Uri.TryCreate(new Uri(LatestReleasePage), location, out releaseUri) ||
                    !string.Equals(releaseUri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Invalid latest release redirect.");

                const string marker = "/releases/tag/";
                int markerIndex = releaseUri.AbsolutePath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (markerIndex < 0)
                    throw new InvalidDataException("Invalid latest release tag.");
                string tag = Uri.UnescapeDataString(
                    releaseUri.AbsolutePath.Substring(markerIndex + marker.Length)).Trim('/');
                string latestVersion = NormalizeVersion(tag);
                Version current;
                Version latest;
                if (!Version.TryParse(NormalizeVersion(currentVersion), out current) ||
                    !Version.TryParse(latestVersion, out latest))
                    throw new InvalidDataException("Invalid release version.");

                string packageName = "CodexRateMonitor-" + latestVersion + "-windows-x64.zip";
                string downloadRoot = "https://github.com/daxian-w/codex-huds/releases/download/" +
                    Uri.EscapeDataString(tag) + "/";
                string notes = string.Empty;
                try
                {
                    notes = DownloadText(
                        "https://raw.githubusercontent.com/daxian-w/codex-huds/" +
                        Uri.EscapeDataString(tag) + "/.github/release-notes/" +
                        Uri.EscapeDataString(tag) + ".md",
                        15000);
                }
                catch (WebException)
                {
                }

                return new UpdateCheckResult
                {
                    Success = true,
                    UpdateAvailable = latest > current,
                    Info = new UpdateInfo
                    {
                        Version = latestVersion,
                        ReleaseName = tag,
                        Notes = notes,
                        ReleaseUrl = releaseUri.AbsoluteUri,
                        PackageName = packageName,
                        PackageUrl = downloadRoot + Uri.EscapeDataString(packageName),
                        ChecksumsUrl = downloadRoot + "SHA256SUMS.txt"
                    }
                };
            }
        }

        internal static string DownloadText(string url, int timeoutMs)
        {
            var request = CreateRequest(url, timeoutMs);
            request.Accept = "application/vnd.github+json";
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                return reader.ReadToEnd();
        }

        internal static void DownloadFile(string url, string path, int timeoutMs)
        {
            var request = CreateRequest(url, timeoutMs);
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var input = response.GetResponseStream())
            using (var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                input.CopyTo(output);
        }

        private static HttpWebRequest CreateRequest(string url, int timeoutMs)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.UserAgent = "CodexRateMonitor/" + BuildVersion.Value;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.Timeout = timeoutMs;
            request.ReadWriteTimeout = timeoutMs;
            return request;
        }

        private static string DescribeFailure(Exception exception)
        {
            var web = exception as WebException;
            if (web == null)
                return exception.GetType().Name;
            var response = web.Response as HttpWebResponse;
            return response == null
                ? "WebException status=" + web.Status
                : "WebException status=" + web.Status +
                  " http=" + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
        }

        private static string NormalizeVersion(string value)
        {
            value = (value ?? string.Empty).Trim();
            return value.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                ? value.Substring(1)
                : value;
        }

        private static string GetString(Dictionary<string, object> source, string key)
        {
            object value;
            return source != null && source.TryGetValue(key, out value) && value != null
                ? Convert.ToString(value, CultureInfo.InvariantCulture)
                : null;
        }

        private static IEnumerable GetList(Dictionary<string, object> source, string key)
        {
            object value;
            if (source != null && source.TryGetValue(key, out value) && value is IEnumerable)
                return (IEnumerable)value;
            return new object[0];
        }

        public void Dispose()
        {
            disposed = true;
            timer.Dispose();
        }
    }

    internal sealed class PreparedUpdate
    {
        public string RootPath { get; set; }
        public string PackagePath { get; set; }
    }

    internal static class UpdateInstaller
    {
        private const string HelperSwitch = "--apply-update";
        private const string CleanupSwitch = "--cleanup-update";

        public static bool TryHandleCommandLine(string[] args)
        {
            if (args == null || args.Length == 0)
                return false;
            if (string.Equals(args[0], HelperSwitch, StringComparison.OrdinalIgnoreCase))
            {
                ApplyUpdate(args);
                return true;
            }
            if (string.Equals(args[0], CleanupSwitch, StringComparison.OrdinalIgnoreCase))
            {
                string root = args.Length > 1 ? args[1] : null;
                ThreadPool.QueueUserWorkItem(delegate { CleanupLater(root); });
                return false;
            }
            return false;
        }

        public static PreparedUpdate Prepare(UpdateInfo info)
        {
            if (info == null || string.IsNullOrWhiteSpace(info.PackageUrl) ||
                string.IsNullOrWhiteSpace(info.ChecksumsUrl) ||
                string.IsNullOrWhiteSpace(info.PackageName))
                throw new InvalidOperationException(I18n.T("UpdateAssetsMissing"));
            ValidateDownloadUrl(info.PackageUrl);
            ValidateDownloadUrl(info.ChecksumsUrl);

            string root = Path.Combine(
                Path.GetTempPath(),
                "CodexRateMonitor-update-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                string package = Path.Combine(root, info.PackageName);
                string sums = Path.Combine(root, "SHA256SUMS.txt");
                DownloadFile(info.PackageUrl, package);
                DownloadFile(info.ChecksumsUrl, sums);
                VerifyChecksum(package, sums, info.PackageName);
                return new PreparedUpdate { RootPath = root, PackagePath = package };
            }
            catch
            {
                try
                {
                    if (Directory.Exists(root))
                        Directory.Delete(root, true);
                }
                catch
                {
                }
                throw;
            }
        }

        public static void LaunchHelper(PreparedUpdate update, string version)
        {
            string helper = Path.Combine(update.RootPath, "CodexRateMonitor.Updater.exe");
            File.Copy(Application.ExecutablePath, helper, true);
            string installDirectory = Path.GetDirectoryName(Application.ExecutablePath);
            string arguments = string.Join(" ", new[]
            {
                HelperSwitch,
                Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture),
                Quote(update.PackagePath),
                Quote(installDirectory),
                Quote(Application.ExecutablePath),
                Quote(update.RootPath),
                Quote(version ?? string.Empty)
            });

            var start = new ProcessStartInfo(helper, arguments);
            start.UseShellExecute = true;
            if (!CanWriteDirectory(installDirectory))
                start.Verb = "runas";
            Process.Start(start);
        }

        private static void ApplyUpdate(string[] args)
        {
            if (args.Length < 7)
                return;
            int parentPid;
            if (!int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out parentPid))
                return;

            string package = args[2];
            string installDirectory = args[3];
            string executable = args[4];
            string root = args[5];
            try
            {
                WaitForParentExit(parentPid, executable, 30000);

                string staging = Path.Combine(root, "staging");
                Directory.CreateDirectory(staging);
                ZipFile.ExtractToDirectory(package, staging);
                string payload = FindPayloadDirectory(staging);
                CopyPayload(payload, installDirectory);

                var restart = new ProcessStartInfo(
                    executable,
                    CleanupSwitch + " " + Quote(root));
                restart.UseShellExecute = true;
                Process.Start(restart);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    I18n.F("UpdateInstallFailed", ex.Message),
                    I18n.T("UpdateTitle"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        internal static void WaitForParentExit(int parentPid, string expectedExecutable, int timeoutMilliseconds)
        {
            Process parent;
            try { parent = Process.GetProcessById(parentPid); }
            catch (ArgumentException) { return; } // Parent already exited.
            using (parent)
            {
                if (parent.HasExited) return;
                string actual;
                try { actual = parent.MainModule.FileName; }
                catch (System.ComponentModel.Win32Exception)
                {
                    if (parent.HasExited) return;
                    throw;
                }
                if (!string.Equals(Path.GetFullPath(actual), Path.GetFullPath(expectedExecutable), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The update parent executable does not match.");
                if (!parent.WaitForExit(timeoutMilliseconds))
                    throw new InvalidOperationException("Codex Rate Monitor did not exit in time.");
            }
        }

        private static void CopyPayload(string source, string destination)
        {
            CopyPayloadTransactional(source, destination, CopyWithRetry);
        }

        // Keep a complete backup before the first replacement. The injectable
        // copy operation lets regression tests fail after real earlier writes.
        internal static void CopyPayloadTransactional(string source, string destination, Action<string, string> copy)
        {
            source = NormalizeUpdateDirectory(source);
            destination = NormalizeUpdateDirectory(destination);
            string sourcePrefix = source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string destinationPrefix = destination.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase) || source.StartsWith(destinationPrefix, StringComparison.OrdinalIgnoreCase) ||
                destination.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Update source and installation directories must be separate.");
            string backupRoot = Path.Combine(Path.GetDirectoryName(source), "rollback-" + Guid.NewGuid().ToString("N"));
            string[] files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
            Array.Sort(files, delegate(string left, string right)
            {
                bool leftExe = string.Equals(
                    Path.GetFileName(left), "CodexRateMonitor.exe", StringComparison.OrdinalIgnoreCase);
                bool rightExe = string.Equals(
                    Path.GetFileName(right), "CodexRateMonitor.exe", StringComparison.OrdinalIgnoreCase);
                if (leftExe == rightExe)
                    return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
                return leftExe ? 1 : -1;
            });
            var replacements = new List<PayloadReplacement>();
            foreach (string file in files)
            {
                string relative = file.Substring(sourcePrefix.Length);
                string target = Path.Combine(destination, relative);
                if (string.Equals(relative, "settings.json", StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(target))
                    continue;
                EnsureSafeUpdateTarget(destination, target);
                if (Directory.Exists(target)) throw new IOException("An update file conflicts with an installation directory.");
                var entry = new PayloadReplacement { Source = file, Target = target,
                    Backup = Path.Combine(backupRoot, relative), Existed = File.Exists(target) };
                if (entry.Existed)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(entry.Backup));
                    File.Copy(target, entry.Backup, false);
                }
                replacements.Add(entry);
            }
            var attempted = new List<PayloadReplacement>();
            var createdDirectories = new List<string>();
            try
            {
                foreach (PayloadReplacement entry in replacements)
                {
                    EnsureSafeUpdateTarget(destination, entry.Target);
                    CreateUpdateDirectories(Path.GetDirectoryName(entry.Target), createdDirectories);
                    attempted.Add(entry); // File.Copy can fail after creating/truncating a file.
                    copy(entry.Source, entry.Target);
                }
            }
            catch (Exception original)
            {
                var failures = new List<Exception>();
                for (int index = attempted.Count - 1; index >= 0; index--)
                {
                    PayloadReplacement entry = attempted[index];
                    try
                    {
                        EnsureSafeUpdateTarget(destination, entry.Target);
                        if (entry.Existed) CopyWithRetry(entry.Backup, entry.Target);
                        else if (File.Exists(entry.Target)) File.Delete(entry.Target);
                    }
                    catch (Exception rollbackError) { failures.Add(rollbackError); }
                }
                for (int index = createdDirectories.Count - 1; index >= 0; index--)
                {
                    string directory = createdDirectories[index];
                    try
                    {
                        // Empty-directory deletion only. Preserve files another
                        // application may have added while the update ran.
                        if (directory.StartsWith(destinationPrefix, StringComparison.OrdinalIgnoreCase))
                        {
                            EnsureSafeUpdateTarget(destination, directory);
                            if (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length == 0)
                                Directory.Delete(directory, false);
                        }
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
                if (failures.Count > 0)
                {
                    failures.Insert(0, original);
                    throw new IOException("Update rollback was incomplete. Backups retained at: " + backupRoot,
                        new AggregateException(failures));
                }
                throw new IOException("Update failed; previous files restored.", original);
            }
        }

        private sealed class PayloadReplacement
        {
            public string Source, Target, Backup;
            public bool Existed;
        }

        private static void CreateUpdateDirectories(string directory, List<string> created)
        {
            var missing = new Stack<string>();
            for (string current = directory; !Directory.Exists(current); current = Path.GetDirectoryName(current))
                missing.Push(current);
            while (missing.Count > 0)
            {
                string current = missing.Pop();
                Directory.CreateDirectory(current);
                created.Add(current);
            }
        }

        private static void EnsureSafeUpdateTarget(string root, string target)
        {
            string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(target);
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Update path escaped the installation directory.");
            for (string current = full; current != null && current.Length >= root.Length; current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) &&
                    (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Update target contains a reparse point.");
        }

        private static string NormalizeUpdateDirectory(string path)
        {
            string full = Path.GetFullPath(path);
            string trimmed = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string root = Path.GetPathRoot(full);
            return trimmed.Length < root.Length ? root : trimmed;
        }

        private static void CopyWithRetry(string source, string target)
        {
            Exception last = null;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    File.Copy(source, target, true);
                    return;
                }
                catch (Exception ex)
                {
                    last = ex;
                    Thread.Sleep(500);
                }
            }
            throw new IOException("Unable to replace " + Path.GetFileName(target) + ".", last);
        }

        private static string FindPayloadDirectory(string staging)
        {
            string[] executables = Directory.GetFiles(
                staging, "CodexRateMonitor.exe", SearchOption.AllDirectories);
            if (executables.Length != 1)
                throw new InvalidDataException("Release package layout is invalid.");
            return Path.GetDirectoryName(executables[0]);
        }

        private static void VerifyChecksum(string package, string sumsPath, string packageName)
        {
            string expected = null;
            foreach (string line in File.ReadAllLines(sumsPath))
            {
                if (line.EndsWith(packageName, StringComparison.OrdinalIgnoreCase))
                {
                    expected = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)[0];
                    break;
                }
            }
            if (string.IsNullOrWhiteSpace(expected))
                throw new InvalidDataException(I18n.T("UpdateChecksumMissing"));
            string actual;
            using (var stream = File.OpenRead(package))
            using (var sha = System.Security.Cryptography.SHA256.Create())
                actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(I18n.T("UpdateChecksumFailed"));
        }

        private static void DownloadFile(string url, string path)
        {
            UpdateChecker.DownloadFile(url, path, 120000);
        }

        private static void ValidateDownloadUrl(string value)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) ||
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(I18n.T("UpdateAssetsMissing"));
        }

        private static bool CanWriteDirectory(string directory)
        {
            string probe = Path.Combine(directory, ".codex-rate-monitor-update-" + Guid.NewGuid().ToString("N"));
            try
            {
                File.WriteAllText(probe, string.Empty);
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static void CleanupLater(string root)
        {
            string full;
            if (!IsOwnedUpdateDirectory(root, out full)) return;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    // Revalidate before each recursive delete, including retries.
                    if (!IsOwnedUpdateDirectory(root, out full)) return;
                    if (Directory.Exists(full)) Directory.Delete(full, true);
                    return;
                }
                catch { Thread.Sleep(1000); }
            }
        }

        internal static bool IsOwnedUpdateDirectory(string root, out string full)
        {
            full = null;
            if (string.IsNullOrWhiteSpace(root)) return false;
            try
            {
                full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                const string prefix = "CodexRateMonitor-update-";
                string name = Path.GetFileName(full);
                Guid identifier;
                if (!string.Equals(Path.GetDirectoryName(full), temp, StringComparison.OrdinalIgnoreCase) ||
                    !name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                    !Guid.TryParseExact(name.Substring(prefix.Length), "N", out identifier)) return false;
                return !Directory.Exists(full) || (File.GetAttributes(full) & FileAttributes.ReparsePoint) == 0;
            }
            catch { return false; }
        }
    }
}
