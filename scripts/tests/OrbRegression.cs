using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Threading;
using System.Diagnostics;
using System.Collections;

internal static class OrbRegression
{
    private static Assembly assembly;
    private static int checks;
    private static object New(string name) { return Activator.CreateInstance(assembly.GetType("CodexRateMonitorNative." + name, true), true); }
    private static object Get(object target, string name) { return target.GetType().GetProperty(name).GetValue(target, null); }
    private static void Set(object target, string name, object value) { target.GetType().GetProperty(name).SetValue(target, value, null); }
    private static object Call(object target, string name, params object[] args)
    {
        try { return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }
    private static object Static(string type, string method, params object[] args)
    {
        return assembly.GetType("CodexRateMonitorNative." + type, true).GetMethod(method,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, args);
    }
    private static object Parse(string text) { return Static("MonitorSettings", "Parse", text); }
    private static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    private static double Luminance(Color color)
    {
        double[] channels = { color.R / 255d, color.G / 255d, color.B / 255d };
        for (int i = 0; i < 3; i++) channels[i] = channels[i] <= 0.04045 ? channels[i] / 12.92 : Math.Pow((channels[i] + 0.055) / 1.055, 2.4);
        return channels[0] * 0.2126 + channels[1] * 0.7152 + channels[2] * 0.0722;
    }
    private static double Contrast(Color a, Color b)
    {
        double first = Luminance(a), second = Luminance(b);
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }
    private static Color OverBlack(Color color) { return Color.FromArgb(color.R * color.A / 255, color.G * color.A / 255, color.B * color.A / 255); }
    private static string EventLine(string type, string turn, DateTime timestamp)
    {
        return new JavaScriptSerializer().Serialize(new Dictionary<string, object> {
            { "timestamp", timestamp.ToString("o") }, { "type", "event_msg" },
            { "payload", new Dictionary<string, object> { { "type", type }, { "turn_id", turn }, { "note", "UTF8-\u4e2d\u6587" } } }
        }) + "\n";
    }
    private static void VerifyTaskSource(string captures)
    {
        string root = Path.Combine(captures, "sessions-" + Guid.NewGuid().ToString("N"));
        DateTime now = DateTime.UtcNow;
        string directory = Path.Combine(root, now.ToString("yyyy"), now.ToString("MM"), now.ToString("dd"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "fixture.jsonl");
        string metadata = "{\"type\":\"session_meta\",\"payload\":{\"id\":\"fixture\",\"originator\":\"Codex Desktop\",\"cli_version\":\"fixture\",\"source\":\"vscode\"}}\n";
        File.WriteAllText(path, metadata + EventLine("task_started", "turn-one", now.AddSeconds(-3)), new UTF8Encoding(false));
        object provider = Activator.CreateInstance(assembly.GetType("CodexRateMonitorNative.TaskStatusProvider"), new object[] { root });
        try
        {
            object snapshot = Call(provider, "ReadSnapshot", now, 5);
            Check((string)Get(snapshot, "State") == "running", "Structured task start was not observed");
            Check((string)Get(snapshot, "Coverage") == "partial" && (int?)Get(snapshot, "ActiveCount") == 1, "Local observation incorrectly claims global coverage");
            Check(!(bool)Get(snapshot, "HasProgress"), "Task source invented plan progress");
            byte[] complete = Encoding.UTF8.GetBytes(EventLine("task_complete", "turn-one", now.AddSeconds(-1)));
            using (FileStream append = new FileStream(path, FileMode.Append)) append.Write(complete, 0, complete.Length - 2);
            snapshot = Call(provider, "ReadSnapshot", now, 5);
            Check((string)Get(snapshot, "State") == "running", "Unterminated event line was applied");
            using (FileStream append = new FileStream(path, FileMode.Append)) append.Write(complete, complete.Length - 2, 2);
            snapshot = Call(provider, "ReadSnapshot", now, 5);
            Check((string)Get(snapshot, "State") == "ended", "Lifecycle end not observed or misrepresented as success");
            File.AppendAllText(path, EventLine("task_started", "turn-one", now.AddSeconds(-2)) +
                EventLine("item_completed", "turn-one", now), new UTF8Encoding(false));
            snapshot = Call(provider, "ReadSnapshot", now, 5);
            Check((string)Get(snapshot, "State") == "ended", "Late event resurrected ended task");
            File.AppendAllText(path, EventLine("task_started", "turn-two", now), new UTF8Encoding(false));
            snapshot = Call(provider, "ReadSnapshot", now, 5);
            Check((string)Get(snapshot, "TurnId") == "turn-two" && (string)Get(snapshot, "State") == "running", "New turn was not isolated");
            DateTime earlierClock = now.AddHours(-1);
            snapshot = Call(provider, "ReadSnapshot", earlierClock, 5);
            Check((bool)Get(snapshot, "IsStale"), "Backwards wall-clock jump kept old task data current");
            File.AppendAllText(path, EventLine("task_started", "turn-clock", earlierClock), new UTF8Encoding(false));
            snapshot = Call(provider, "ReadSnapshot", earlierClock, 5);
            Check((string)Get(snapshot, "TurnId") == "turn-clock" && !(bool)Get(snapshot, "IsStale"),
                "New lifecycle data could not recover after a backwards clock jump");
            File.AppendAllText(path, EventLine("task_complete", "turn-clock", earlierClock.AddSeconds(1)), new UTF8Encoding(false));
            snapshot = Call(provider, "ReadSnapshot", earlierClock.AddSeconds(2), 5);
            Check((string)Get(snapshot, "State") == "ended", "Clock adjustment prevented a real task end");
            snapshot = Call(provider, "ReadSnapshot", now.AddSeconds(45), 5);
            Check((bool)Get(snapshot, "IsStale"), "Unconfirmed activity was kept current forever");
            Check(Get(snapshot, "ActiveCount") == null, "Unknown activity represented as zero");
            File.AppendAllText(path, EventLine("task_started", "turn-stale-recovery", now.AddSeconds(45)), new UTF8Encoding(false));
            snapshot = Call(provider, "ReadSnapshot", now.AddSeconds(45), 5);
            Check(!(bool)Get(snapshot, "IsStale") && (string)Get(snapshot, "State") == "running",
                "New real lifecycle event did not recover stale state");
            snapshot = Call(provider, "ReadSnapshot", now.AddSeconds(90), 5);
            Check((bool)Get(snapshot, "IsStale"), "Successful file polling alone refreshed task confirmation");
            File.AppendAllText(path, EventLine("item_completed", "turn-stale-recovery", now.AddSeconds(90)), new UTF8Encoding(false));
            snapshot = Call(provider, "ReadSnapshot", now.AddSeconds(90), 5);
            Check(!(bool)Get(snapshot, "IsStale") && (int?)Get(snapshot, "ActiveCount") == 1,
                "Correlated activity could not recover the same stale turn");
            Check((DateTime)Get(snapshot, "LastConfirmedAtUtc") == now.AddSeconds(90),
                "Task confirmation time was not advanced by correlated activity");
            File.WriteAllText(path, metadata + EventLine("task_started", "turn-new-file", now), new UTF8Encoding(false));
            snapshot = Call(provider, "ReadSnapshot", now, 5);
            Check((string)Get(snapshot, "TurnId") == "turn-new-file", "File replacement/truncation not detected");
            string childMetadata = "{\"type\":\"session_meta\",\"payload\":{\"id\":\"child-fixture\",\"originator\":\"Codex Desktop\",\"source\":\"subAgent\"}}\n";
            File.WriteAllText(Path.Combine(directory, "child.jsonl"), childMetadata + EventLine("task_started", "child-turn", now), new UTF8Encoding(false));
            object independent = Activator.CreateInstance(assembly.GetType("CodexRateMonitorNative.TaskStatusProvider"), new object[] { root });
            try
            {
                object combined = Call(independent, "ReadSnapshot", now, 5);
                Check((int?)Get(combined, "ActiveCount") == 1, "Child agent was double counted as a top-level task");
            }
            finally { ((IDisposable)independent).Dispose(); }
            Call(provider, "Reset", 1, true);
            snapshot = Call(provider, "ReadSnapshot", DateTime.UtcNow, 5);
            Check((string)Get(snapshot, "Coverage") == "unavailable", "Account reset reused old task lifecycle");
        }
        finally { ((IDisposable)provider).Dispose(); }
        object missing = Activator.CreateInstance(assembly.GetType("CodexRateMonitorNative.TaskStatusProvider"), new object[] { Path.Combine(root, "missing") });
        try
        {
            object snapshot = Call(missing, "ReadSnapshot", now, 5);
            Check((string)Get(snapshot, "State") == "unavailable" && Get(snapshot, "ActiveCount") == null, "Missing source became fake idle zero");
        }
        finally { ((IDisposable)missing).Dispose(); }
    }

    [STAThread]
    private static void Main(string[] args)
    {
        try { Run(args); }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL " + error.GetType().Name + ": " + error.Message);
            Environment.ExitCode = 1;
        }
    }

    private static void Run(string[] args)
    {
        assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
        string captures = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(captures);
        VerifyTaskSource(captures);
        VerifyBoundedTaskReads(captures);
        VerifyNonblockingDispose(captures);
        object settings = Parse("{\"OverlayMode\":\"attach\",\"Position\":\"bottom-right\",\"DesktopX\":42,\"Style\":{\"Scale\":1.1,\"ScaleBasisVersion\":2,\"Text\":\"#112233\"}}");
        Check((string)Get(settings, "DesktopWidgetStyle") == "strip", "Legacy mode was replaced by orb");
        Check((int)Get(settings, "DesktopX") == 42, "Legacy position lost");
        Check((string)Get(settings, "DisplayLines") == "2", "Corner default migration regressed");
        Check((string)Get(Get(settings, "Style"), "Text") == "#112233", "Legacy custom colors lost");
        Check(Math.Abs((double)Get(Get(settings, "Style"), "Scale") - 1.1) < 0.0001, "Orb changed legacy scale");
        object orb = Get(settings, "Orb");
        Check((string)Get(orb, "SizePreset") == "compact", "Default size is not compact");
        Check((string)Get(orb, "Skin") == "cream", "Legacy settings did not keep cream skin");
        foreach (string skin in new[] { "cream", "glacier", "lavender", "invalid" })
        {
            object parsed = Parse("{\"Orb\":{\"Skin\":\"" + skin + "\"}}");
            Check((string)Get(Get(parsed, "Orb"), "Skin") == (skin == "invalid" ? "cream" : skin), "Skin parsing/fallback failed");
        }
        object clone = Call(settings, "Clone");
        Set(Get(clone, "Orb"), "SizePreset", "large");
        Set(Get(Get(clone, "Orb"), "Position"), "OffsetXDip", 75d);
        Check((string)Get(orb, "SizePreset") == "compact", "Settings preview shares mutable orb settings");
        Check((double)Get(Get(orb, "Position"), "OffsetXDip") == 0, "Preview shares position reference");

        object invalid = Parse("{\"DesktopX\":84,\"Orb\":{\"SizePreset\":\"wrong\",\"ExpandDelayMs\":\"bad\",\"CollapseDelayMs\":999999,\"AnimationEnabled\":1,\"GlassOpacity\":0.1,\"GlowStrength\":\"bad\",\"CornerRadiusScale\":9}}");
        object invalidOrb = Get(invalid, "Orb");
        Check((int)Get(invalid, "DesktopX") == 84, "Bad orb field discarded valid legacy data");
        Check((string)Get(invalidOrb, "SizePreset") == "compact", "Invalid size was not normalized");
        Check((int)Get(invalidOrb, "ExpandDelayMs") == 120, "Invalid type did not use default");
        Check((int)Get(invalidOrb, "CollapseDelayMs") == 2000, "Delay was not clamped");
        Check((bool)Get(invalidOrb, "AnimationEnabled"), "Wrong boolean type did not use default");
        Check((double)Get(invalidOrb, "GlassOpacity") == 0.85, "Opacity was not clamped");
        Check((double)Get(invalidOrb, "GlowStrength") == 0.75, "Invalid glow default lost");
        Set(orb, "GlassOpacity", double.NaN);
        Set(orb, "GlowStrength", double.PositiveInfinity);
        Call(orb, "Normalize");
        Check((double)Get(orb, "GlassOpacity") == 0.94 && (double)Get(orb, "GlowStrength") == 0.75, "Non-finite values not normalized");

        string runtime = Path.Combine(captures, "settings-" + Guid.NewGuid().ToString("N") + ".json");
        Call(settings, "SaveTo", runtime);
        string previous = File.ReadAllText(runtime);
        Set(orb, "SizePreset", "large");
        Set(orb, "Skin", "lavender");
        Call(settings, "SaveTo", runtime);
        Check(File.ReadAllText(runtime + ".bak") == previous, "Previous settings backup missing");
        Check((string)Get(Get(Parse(File.ReadAllText(runtime)), "Orb"), "SizePreset") == "large", "Save/reload lost preset");
        Check((string)Get(Get(Parse(File.ReadAllText(runtime)), "Orb"), "Skin") == "lavender", "Save/reload lost skin");
        Set(orb, "Skin", "cream");
        string beforeFailure = File.ReadAllText(runtime);
        bool failed = false;
        using (FileStream locked = File.Open(runtime, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            try { Call(settings, "SaveTo", runtime); } catch (IOException) { failed = true; }
        }
        Check(failed && File.ReadAllText(runtime) == beforeFailure, "Failed save damaged original or suppressed error");
        Check(Directory.GetFiles(captures, "*.tmp").Length == 0, "Failed save leaked temporary file");

        string[] presets = { "tiny", "small", "mini", "compact", "standard", "large", "extraLarge" };
        foreach (string preset in presets)
        foreach (int dpi in new[] { 96, 120, 144, 168, 192 })
        {
            Set(orb, "SizePreset", preset);
            float scale = (float)Static("OrbGeometry", "Scale", orb, dpi, new Rectangle(-1920, 0, 1920, 1080));
            Size compact = (Size)Static("OrbGeometry", "PixelSize", false, true, scale);
            Size expanded = (Size)Static("OrbGeometry", "PixelSize", true, true, scale);
            foreach (Point anchor in new[] { new Point(-1912, 8), new Point(-250, 8), new Point(-1912, 850), new Point(-250, 850) })
            {
                Rectangle bounds = (Rectangle)Static("OrbGeometry", "ExpandedBounds", anchor, compact, expanded, new Rectangle(-1920, 0, 1920, 1080), 8);
                Check(new Rectangle(-1920, 0, 1920, 1080).Contains(bounds), "Expanded orb escaped negative-coordinate monitor");
            }
            float fit = (float)Static("OrbGeometry", "Scale", orb, dpi, new Rectangle(0, 0, 500, 320));
            Size small = (Size)Static("OrbGeometry", "PixelSize", true, true, fit);
            Check(small.Width <= 484 && small.Height <= 304, "Small-screen fitting failed");
        }
        Set(orb, "SizePreset", "compact");
        Check((Size)Static("OrbGeometry", "PixelSize", false, true, 1f) == new Size(240, 168), "Compact geometry incorrect");
        Check((Size)Static("OrbGeometry", "PixelSize", false, true,
            (float)(double)Static("OrbGeometry", "PresetScale", "tiny")) == new Size(120, 84), "Tiny is not half compact size");
        Check((Size)Static("OrbGeometry", "PixelSize", false, true,
            (float)(double)Static("OrbGeometry", "PresetScale", "small")) == new Size(180, 126), "Small is not three-quarter compact size");
        foreach (string preset in new[] { "tiny", "small" })
        {
            Set(orb, "SizePreset", preset);
            Call(settings, "SaveTo", runtime);
            Check((string)Get(Get(Parse(File.ReadAllText(runtime)), "Orb"), "SizePreset") == preset,
                "New size did not survive normalization and save/reload: " + preset);
        }
        Set(orb, "SizePreset", "compact");
        Check((Size)Static("OrbGeometry", "PixelSize", true, false, 1f) == new Size(456, 184), "Task-hidden geometry incorrect");
        Rectangle frozen = (Rectangle)Static("OrbGeometry", "ExpandedBoundsFixed", new Point(1000, 100),
            new Size(240, 168), new Size(456, 304), new Rectangle(0, 0, 1920, 1080), 8, true, true);
        Check(frozen.Location == new Point(1000, 100), "A fixed expansion direction flipped at the monitor midpoint");

        object task = New("TaskSnapshot");
        Check(!(bool)Get(task, "HasProgress") && (string)Get(task, "ProgressText") == "—", "Unknown task displayed zero progress");
        Set(task, "State", "running"); Set(task, "Coverage", "partial");
        Set(task, "ActiveCount", 1); Set(task, "CompletedSteps", 2); Set(task, "TotalSteps", 5);
        Check((bool)Get(task, "HasProgress") && (string)Get(task, "ProgressText") == "2/5", "Structured plan not displayed");
        Set(task, "TotalSteps", 7);
        Check((string)Get(task, "ProgressText") == "2/7", "Plan revision was not reflected");
        Set(task, "CompletedSteps", 8);
        Check(!(bool)Get(task, "HasProgress"), "Invalid plan numerator accepted");
        Set(task, "CompletedSteps", 2); Set(task, "TotalSteps", 5); Set(task, "IsStale", true);
        Check(!(bool)Get(task, "HasProgress"), "Stale task displayed current progress");
        Set(task, "IsStale", false);

        object quota = New("RateSnapshot");
        object primary = New("WindowUsage"); Set(primary, "UsedPercent", 0d);
        object secondary = New("WindowUsage"); Set(secondary, "UsedPercent", 16d);
        Set(quota, "Primary", primary); Set(quota, "Secondary", secondary);
        object credits = New("ResetCreditsInfo"); Set(credits, "AvailableCount", 3);
        Set(credits, "EarliestExpiry", DateTime.Today.AddDays(5));
        object renderer = Activator.CreateInstance(assembly.GetType("CodexRateMonitorNative.OrbRenderer"), new object[] { Path.GetDirectoryName(Path.GetFullPath(args[0])) });
        try
        {
            Set(primary, "UsedPercent", 46d);
            foreach (string skin in new[] { "cream", "glacier", "lavender" })
            foreach (float scale in new[] { 0.5f, 0.75f, 1f, 2f })
            {
                Set(orb, "Skin", skin);
                Color start = skin == "glacier" ? Color.FromArgb(58, 190, 217) : skin == "lavender" ? Color.FromArgb(161, 121, 227) : Color.FromArgb(255, 154, 98);
                Color end = skin == "glacier" ? Color.FromArgb(100, 139, 237) : skin == "lavender" ? Color.FromArgb(230, 143, 191) : Color.FromArgb(181, 122, 230);
                using (Bitmap bitmap = (Bitmap)Call(renderer, "CreateBitmap", settings, quota, credits, task, "live", "live", false, scale))
                {
                    foreach (double t in new[] { 0.1, 0.5, 0.9 })
                    {
                        double angle = (-90 + 360 * 0.54 * t) * Math.PI / 180;
                        Color actual = bitmap.GetPixel((int)Math.Round((173 + 35 * Math.Cos(angle)) * scale), (int)Math.Round((67 + 35 * Math.Sin(angle)) * scale));
                        Check(Math.Abs(actual.R - (start.R + (end.R - start.R) * t)) < 16 &&
                            Math.Abs(actual.G - (start.G + (end.G - start.G) * t)) < 16 &&
                            Math.Abs(actual.B - (start.B + (end.B - start.B) * t)) < 16,
                            "Ring gradient does not follow arc: " + skin + "/" + scale + "/" + t);
                    }
                    Color track = bitmap.GetPixel((int)Math.Round(138 * scale), (int)Math.Round(67 * scale));
                    Check(Math.Abs(track.R - 220) < 8 && Math.Abs(track.G - 209) < 8 && Math.Abs(track.B - 189) < 8, "54% ring incorrectly fills unconsumed track");
                    if (scale == 1f) bitmap.Save(Path.Combine(captures, skin + "-ring-54.png"), ImageFormat.Png);
                }
            }
            Set(primary, "UsedPercent", 0d);
            Set(orb, "Skin", "cream");
            foreach (bool expanded in new[] { false, true })
            {
                var colors = new HashSet<int>();
                foreach (string skin in new[] { "cream", "glacier", "lavender" })
                {
                    Set(orb, "Skin", skin);
                    Set(settings, "Language", "zh-CN");
                    using (Bitmap bitmap = (Bitmap)Call(renderer, "CreateBitmap", settings, quota, credits, task, "live", "live", expanded, 1f))
                    {
                        Check(colors.Add(bitmap.GetPixel(112, expanded ? 287 : 150).ToArgb()), "Skin background is not distinct");
                        Check(bitmap.GetPixel(0, 0).A == 0, "Skin lost transparent corners");
                        if (expanded) Check(bitmap.GetPixel(135, 36).ToArgb() == Color.White.ToArgb(), "Skin tinted pure white task header");
                        bitmap.Save(Path.Combine(captures, skin + (expanded ? "-expanded.png" : "-compact.png")), ImageFormat.Png);
                    }
                    Set(orb, "GlassOpacity", 0.85d);
                    using (Bitmap bitmap = (Bitmap)Call(renderer, "CreateBitmap", settings, quota, credits, task, "live", "live", false, 1f))
                    {
                        Color background = OverBlack(bitmap.GetPixel(112, 150));
                        Type theme = assembly.GetType("CodexRateMonitorNative.CreamTheme");
                        foreach (string name in new[] { "Text", "GlassText", "Warning", "Danger" })
                            Check(Contrast((Color)theme.GetField(name).GetValue(null), background) >= 4.5, "Skin text contrast below 4.5: " + skin + "/" + name);
                    }
                    Set(orb, "GlassOpacity", 0.94d);
                }
            }
            Set(orb, "Skin", "cream");
            foreach (string language in new[] { "zh-CN", "zh-TW", "en" })
            foreach (string preset in presets)
            foreach (int dpi in new[] { 96, 120, 144, 168, 192 })
            foreach (bool expanded in new[] { false, true })
            {
                Set(settings, "Language", language); Set(orb, "SizePreset", preset);
                float scale = (float)(double)Static("OrbGeometry", "PresetScale", preset) * dpi / 96f;
                using (Bitmap bitmap = (Bitmap)Call(renderer, "CreateBitmap", settings, quota, credits, task, "live", "live", expanded, scale))
                {
                    Check(bitmap.GetPixel(0, 0).A == 0, "Transparent corner is opaque");
                    Check(bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2).A > 0, "Rendered content is empty");
                    if (expanded)
                        foreach (Point sample in new[] { new Point(135, 36), new Point(405, 116) })
                            Check(bitmap.GetPixel((int)Math.Round(sample.X * scale), (int)Math.Round(sample.Y * scale)).ToArgb() == Color.White.ToArgb(),
                                "Task status card is tinted or transparent: " + preset + "/" + dpi);
                    if (language == "zh-CN" && preset == "compact" && dpi == 96)
                        bitmap.Save(Path.Combine(captures, expanded ? "expanded-sample.png" : "collapsed-sample.png"), ImageFormat.Png);
                    if (language == "zh-CN" && (preset == "tiny" || preset == "small") && dpi == 96)
                        bitmap.Save(Path.Combine(captures, preset + (expanded ? "-expanded-sample.png" : "-collapsed-sample.png")), ImageFormat.Png);
                }
            }
            Set(settings, "Language", "zh-CN"); Set(orb, "SizePreset", "compact");
            using (Bitmap error = (Bitmap)Call(renderer, "CreateBitmap", settings, null, null, New("TaskSnapshot"), "error", "error", true, 1f))
                error.Save(Path.Combine(captures, "unavailable-sample.png"), ImageFormat.Png);
            Set(orb, "GlassOpacity", 0.85d);
            using (Bitmap dark = (Bitmap)Call(renderer, "CreateBitmap", settings, quota, credits, task, "live", "live", false, 1f))
            {
                Color outerBackground = OverBlack(dark.GetPixel(112, 150));
                Type theme = assembly.GetType("CodexRateMonitorNative.CreamTheme");
                foreach (string colorName in new[] { "Text", "GlassText", "Warning", "Danger" })
                {
                    Color color = (Color)theme.GetField(colorName).GetValue(null);
                    Check(Contrast(color, outerBackground) >= 4.5, "Text contrast below 4.5 on dark desktop at minimum opacity: " + colorName);
                }
                Color capBackground = OverBlack(dark.GetPixel(110, 99));
                Check(Contrast((Color)theme.GetField("Muted").GetValue(null), capBackground) >= 4.5,
                    "Secondary text contrast below 4.5 in compact glass cap");
            }
            Set(orb, "GlassOpacity", 0.94d);
        }
        finally { ((IDisposable)renderer).Dispose(); }
        if (Array.IndexOf(args, "--live-source") >= 0)
        {
            Type providerType = assembly.GetType("CodexRateMonitorNative.TaskStatusProvider");
            string root = (string)providerType.GetProperty("DefaultRoot").GetValue(null, null);
            object provider = Activator.CreateInstance(providerType, new object[] { root });
            try
            {
                object observed = Call(provider, "ReadSnapshot", DateTime.UtcNow, 5);
                for (int pass = 0; pass < 16 && (string)Get(observed, "ErrorCode") == "bootstrap-pending"; pass++)
                    observed = Call(provider, "ReadSnapshot", DateTime.UtcNow, 5);
                string fingerprint;
                using (var hash = System.Security.Cryptography.SHA256.Create())
                    fingerprint = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes((string)Get(observed, "ThreadId") ?? ""))).Replace("-", "").ToLowerInvariant().Substring(0, 12);
                Console.WriteLine("LIVE source=" + Get(observed, "SourceKind") + " version=" + Get(observed, "SourceVersion") +
                    " coverage=" + Get(observed, "Coverage") + " state=" + Get(observed, "State") + " active=" + Get(observed, "ActiveCount") +
                    " stale=" + Get(observed, "IsStale") + " target=" + fingerprint);
            }
            finally { ((IDisposable)provider).Dispose(); }
        }
        Console.WriteLine("PASS " + checks + " orb configuration, atomic-save, geometry, task-truth and alpha-render assertions; sample captures generated.");
    }

    private static void VerifyBoundedTaskReads(string captures)
    {
        string root = Path.Combine(captures, "bounded-source-" + Guid.NewGuid().ToString("N"));
        DateTime now = DateTime.UtcNow;
        string directory = Path.Combine(root, now.ToString("yyyy/MM/dd"));
        Directory.CreateDirectory(directory);
        string ignored = "{\"type\":\"ignored\",\"payload\":\"" + new string('x', 8192) + "\"}";
        for (int file = 0; file < 2; file++)
        {
            using (var writer = new StreamWriter(Path.Combine(directory, file + ".jsonl"), false, new UTF8Encoding(true)))
            {
                writer.WriteLine("{\"type\":\"session_meta\",\"payload\":{\"id\":\"bounded-" + file + "\",\"originator\":\"Codex Desktop\"}}");
                for (int line = 0; line < 1152; line++) writer.WriteLine(ignored);
                writer.Write(EventLine("task_started", "bounded-turn-" + file, now));
            }
        }
        object source = Activator.CreateInstance(assembly.GetType("CodexRateMonitorNative.TaskStatusProvider"), new object[] { root });
        try
        {
            object snapshot = Call(source, "ReadSnapshot", now, 5);
            var cursors = (IDictionary)source.GetType().GetField("files", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(source);
            long read = 0;
            foreach (object cursor in cursors.Values) read += (long)cursor.GetType().GetField("Offset").GetValue(cursor);
            Check(read <= 8L * 1024 * 1024, "Task bootstrap exceeded one global 8 MiB read budget");
            for (int pass = 0; pass < 40 && (int?)Get(snapshot, "ActiveCount") != 2; pass++)
                snapshot = Call(source, "ReadSnapshot", now, 5);
            Check((int?)Get(snapshot, "ActiveCount") == 2, "Bounded streaming starved a rollout or failed to read BOM metadata");
        }
        finally { ((IDisposable)source).Dispose(); }
    }

    private static void VerifyNonblockingDispose(string captures)
    {
        object source = Activator.CreateInstance(assembly.GetType("CodexRateMonitorNative.TaskStatusProvider"), new object[] { captures });
        object gate = source.GetType().GetField("gate", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(source);
        using (var locked = new ManualResetEvent(false))
        {
            var reader = new Thread(delegate()
            {
                lock (gate) { locked.Set(); Thread.Sleep(750); }
            });
            reader.IsBackground = true;
            reader.Start();
            Check(locked.WaitOne(2000), "Read-lock fixture did not start");
            var watch = Stopwatch.StartNew();
            ((IDisposable)source).Dispose();
            Check(watch.ElapsedMilliseconds < 300, "Task provider shutdown blocked behind a background read lock");
            Check(reader.Join(2000), "Read-lock fixture did not finish");
        }
    }
}
