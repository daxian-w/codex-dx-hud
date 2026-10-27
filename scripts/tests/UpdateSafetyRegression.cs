using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

internal static class UpdateSafetyRegression
{
    private static Type installer;
    private static int checks;
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        checks++;
    }
    private static bool Allowed(string path)
    {
        object[] arguments = { path, null };
        return (bool)installer.GetMethod("IsOwnedUpdateDirectory", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, arguments);
    }
    private static void RejectParent(int pid, string path, string expectedMessage)
    {
        try
        {
            installer.GetMethod("WaitForParentExit", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { pid, path, 0 });
            throw new Exception("Updater accepted a parent that must not be replaced");
        }
        catch (TargetInvocationException error)
        {
            Check(error.InnerException is InvalidOperationException && error.InnerException.Message == expectedMessage,
                "Updater rejected the parent for the wrong reason");
        }
    }
    private static void VerifyPayloadRollback()
    {
        string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures-" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "payload"), target = Path.Combine(root, "installed");
        Directory.CreateDirectory(Path.Combine(source, "b-added"));
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(source, "a-existing.txt"), "new a");
        File.WriteAllText(Path.Combine(source, "b-added", "new.txt"), "new file");
        File.WriteAllText(Path.Combine(source, "z-failure.txt"), "new z");
        File.WriteAllText(Path.Combine(source, "CodexRateMonitor.exe"), "new exe");
        File.WriteAllText(Path.Combine(source, "settings.json"), "factory settings");
        File.WriteAllText(Path.Combine(target, "a-existing.txt"), "old a");
        File.WriteAllText(Path.Combine(target, "z-failure.txt"), "old z");
        File.WriteAllText(Path.Combine(target, "CodexRateMonitor.exe"), "old exe");
        File.WriteAllText(Path.Combine(target, "settings.json"), "user settings");
        File.WriteAllText(Path.Combine(target, "unrelated.txt"), "keep me");
        MethodInfo inject = installer.GetMethod("CopyPayloadTransactional", BindingFlags.Static | BindingFlags.NonPublic,
            null, new[] { typeof(string), typeof(string), typeof(Action<string, string>) }, null);
        MethodInfo apply = installer.GetMethod("CopyPayload", BindingFlags.Static | BindingFlags.NonPublic,
            null, new[] { typeof(string), typeof(string) }, null);
        int writes = 0;
        Action<string, string> failAfterPartialWrite = delegate(string from, string to)
        {
            writes++;
            if (Path.GetFileName(from) == "z-failure.txt")
            {
                File.WriteAllText(to, "partial replacement");
                throw new IOException("Synthetic write failure");
            }
            File.Copy(from, to, true);
        };
        bool failed = false;
        try { inject.Invoke(null, new object[] { source, target, failAfterPartialWrite }); }
        catch (TargetInvocationException error)
        {
            failed = error.InnerException is IOException && error.InnerException.Message == "Update failed; previous files restored.";
        }
        Check(failed && writes >= 3, "Update did not fail after actual earlier file replacements");
        Check(File.ReadAllText(Path.Combine(target, "a-existing.txt")) == "old a", "Earlier replacement was not rolled back");
        Check(File.ReadAllText(Path.Combine(target, "z-failure.txt")) == "old z", "Partially written file was not restored");
        Check(!File.Exists(Path.Combine(target, "b-added", "new.txt")), "New file survived rollback");
        Check(!Directory.Exists(Path.Combine(target, "b-added")), "New empty directory survived rollback");
        Check(File.ReadAllText(Path.Combine(target, "settings.json")) == "user settings", "Rollback changed user settings");
        Check(File.ReadAllText(Path.Combine(target, "CodexRateMonitor.exe")) == "old exe", "Failed update replaced the executable");
        Check(File.ReadAllText(Path.Combine(target, "unrelated.txt")) == "keep me", "Rollback changed unrelated files");
        apply.Invoke(null, new object[] { source, target });
        Check(File.ReadAllText(Path.Combine(target, "a-existing.txt")) == "new a", "Successful update did not replace an existing file");
        Check(File.ReadAllText(Path.Combine(target, "z-failure.txt")) == "new z", "Successful update did not replace the final resource");
        Check(File.ReadAllText(Path.Combine(target, "CodexRateMonitor.exe")) == "new exe", "Successful update did not install the executable");
        Check(File.ReadAllText(Path.Combine(target, "b-added", "new.txt")) == "new file", "Successful update lost a new resource");
        Check(File.ReadAllText(Path.Combine(target, "settings.json")) == "user settings", "Successful update overwrote user settings");
        Check(File.ReadAllText(Path.Combine(target, "unrelated.txt")) == "keep me", "Successful update overwrote unrelated files");
        int unexpectedWrites = 0;
        using (FileStream locked = File.Open(Path.Combine(target, "a-existing.txt"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            bool rejected = false;
            try
            {
                inject.Invoke(null, new object[] { source, target, (Action<string, string>)delegate { unexpectedWrites++; } });
            }
            catch (TargetInvocationException error) { rejected = error.InnerException is IOException; }
            Check(rejected && unexpectedWrites == 0, "Update wrote installation files before completing all backups");
        }
        bool overlapRejected = false;
        try { apply.Invoke(null, new object[] { source, source.ToUpperInvariant() }); }
        catch (TargetInvocationException error) { overlapRejected = error.InnerException is InvalidDataException; }
        Check(overlapRejected, "Update accepted overlapping source and destination paths");
        string[] priorBackups = Directory.GetDirectories(root, "rollback-*");
        File.WriteAllText(Path.Combine(source, "a-existing.txt"), "newer a");
        FileStream rollbackLock = null;
        bool incomplete = false;
        try
        {
            Action<string, string> failAndLockRollback = delegate(string from, string to)
            {
                if (Path.GetFileName(from) == "z-failure.txt")
                {
                    rollbackLock = File.Open(Path.Combine(target, "a-existing.txt"), FileMode.Open, FileAccess.Read, FileShare.None);
                    throw new IOException("Synthetic failure with blocked rollback");
                }
                File.Copy(from, to, true);
            };
            inject.Invoke(null, new object[] { source, target, failAndLockRollback });
        }
        catch (TargetInvocationException error)
        {
            incomplete = error.InnerException is IOException &&
                error.InnerException.Message.StartsWith("Update rollback was incomplete.", StringComparison.Ordinal) &&
                error.InnerException.InnerException is AggregateException;
        }
        finally { if (rollbackLock != null) rollbackLock.Dispose(); }
        Check(incomplete, "Incomplete rollback was not reported to the caller");
        string retained = null;
        foreach (string candidate in Directory.GetDirectories(root, "rollback-*"))
            if (Array.IndexOf(priorBackups, candidate) < 0) retained = candidate;
        Check(retained != null && File.ReadAllText(Path.Combine(retained, "a-existing.txt")) == "new a",
            "Incomplete rollback lost its recovery backup");
        File.Copy(Path.Combine(retained, "a-existing.txt"), Path.Combine(target, "a-existing.txt"), true);
        Check(File.ReadAllText(Path.Combine(target, "a-existing.txt")) == "new a", "Retained backup could not recover the blocked file");
        Check(File.ReadAllText(Path.Combine(target, "settings.json")) == "user settings", "Incomplete rollback touched user settings");
    }
    private static int Main(string[] args)
    {
        try
        {
            installer = Assembly.LoadFrom(Path.GetFullPath(args[0])).GetType("CodexRateMonitorNative.UpdateInstaller", true);
            string name = "CodexRateMonitor-update-" + Guid.NewGuid().ToString("N");
            string temp = Path.GetTempPath();
            Check(Allowed(Path.Combine(temp, name)), "Valid updater staging directory rejected");
            Check(!Allowed(temp), "Cleanup accepted the whole temporary directory");
            Check(!Allowed(Path.Combine(temp, "CodexRateMonitor-update-photos")), "Cleanup accepted an unrelated named folder");
            Check(!Allowed(Path.Combine(temp, "nested", name)), "Cleanup accepted a nested unrelated folder");
            Check(!Allowed(Path.Combine(temp.TrimEnd(Path.DirectorySeparatorChar) + "-other", name)), "Cleanup accepted a sibling temporary prefix");
            Check(!Allowed(null), "Cleanup accepted an empty directory");
            using (Process self = Process.GetCurrentProcess())
            {
                RejectParent(self.Id, self.MainModule.FileName, "Codex Rate Monitor did not exit in time.");
                RejectParent(self.Id, Path.Combine(temp, "different.exe"), "The update parent executable does not match.");
            }
            VerifyPayloadRollback();
            Console.WriteLine("PASS " + checks + " update safety assertions: parent identity/timeout, cleanup boundary, partial-write rollback, backup failure and settings preservation. Only isolated synthetic fixture files were modified.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("FAIL " + error.Message); return 1; }
    }
}
