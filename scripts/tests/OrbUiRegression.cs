using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;

internal static class OrbUiRegression
{
    private static Assembly assembly;
    private static int checks;
    [DllImport("user32.dll")] private static extern uint GetGuiResources(IntPtr process, uint flags);
    private static object Get(object value, string key) { return value.GetType().GetProperty(key).GetValue(value, null); }
    private static void Set(object value, string key, object item) { value.GetType().GetProperty(key).SetValue(value, item, null); }
    private static object Field(object value, string key) { return value.GetType().GetField(key, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(value); }
    private static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    private static object Invoke(object value, string name, params object[] args)
    {
        return value.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(value, args);
    }
    private static void VerifyOverlayStateMachine()
    {
        object settings = Activator.CreateInstance(assembly.GetType("CodexRateMonitorNative.MonitorSettings"), true);
        Set(settings, "DesktopWidgetStyle", "orb");
        object orb = Get(settings, "Orb");
        Set(orb, "ExpandOnHover", false);
        Set(orb, "AnimationEnabled", false);
        Type overlayType = assembly.GetType("CodexRateMonitorNative.OverlayForm");
        using (Form form = (Form)Activator.CreateInstance(overlayType, new object[] { settings }))
        {
            form.Show();
            Application.DoEvents();
            Point compactAnchor = form.Location;
            Size compactSize = form.Size;
            Check((bool)Get(form, "IsOrb"), "Orb mode was not integrated into the real window");
            var frameTimes = new List<double>();
            uint startingGdi = 0;
            for (int cycle = 0; cycle < 200; cycle++)
            {
                if (cycle == 5) startingGdi = GetGuiResources(Process.GetCurrentProcess().Handle, 0);
                var frameTimer = Stopwatch.StartNew();
                Invoke(form, "RequestOrbTarget", true, false);
                Invoke(form, "OrbTick", null, EventArgs.Empty);
                frameTimes.Add(frameTimer.Elapsed.TotalMilliseconds);
                Check((bool)Get(form, "OrbExpanded") && form.Width > compactSize.Width, "Native orb failed to expand");
                Invoke(form, "SetTaskSnapshot", Activator.CreateInstance(assembly.GetType("CodexRateMonitorNative.TaskSnapshot"), true));
                Check((bool)Get(form, "OrbExpanded"), "Data refresh collapsed an expanded orb");
                frameTimer.Restart();
                Invoke(form, "RequestOrbTarget", false, false);
                Invoke(form, "OrbTick", null, EventArgs.Empty);
                frameTimes.Add(frameTimer.Elapsed.TotalMilliseconds);
                Check(form.Location == compactAnchor && form.Size == compactSize, "Repeated transitions moved the saved anchor");
            }
            double[] sortedFrames = frameTimes.Skip(10).OrderBy(value => value).ToArray();
            double p95 = sortedFrames[(int)Math.Ceiling(sortedFrames.Length * 0.95) - 1];
            long gdiGrowth = (long)GetGuiResources(Process.GetCurrentProcess().Handle, 0) - startingGdi;
            Console.WriteLine("METRIC native transition p95_ms=" + p95.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                " gdi_growth=" + gdiGrowth + " transitions=" + sortedFrames.Length);
            Check(p95 <= 33, "Native transition p95 exceeds 33 ms");
            Check(gdiGrowth <= 20, "GDI resources grew across repeated transitions");
            Invoke(form, "RequestOrbTarget", true, false);
            Invoke(form, "OrbTick", null, EventArgs.Empty);
            Invoke(form, "CollapseOrbForHide");
            Check(!(bool)Get(form, "OrbExpanded") && form.Location == compactAnchor, "Fullscreen-hide reset lost folded anchor");
            Set(settings, "DesktopWidgetStyle", "strip");
            Invoke(form, "ApplySettings", settings);
            Check(!(bool)Get(form, "IsOrb"), "Cannot return to legacy desktop strip");
            form.Close();
        }
    }
    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (Control nested in Descendants(child)) yield return nested;
        }
    }
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            VerifyOverlayStateMachine();
            string captures = Path.GetFullPath(args[1]);
            Directory.CreateDirectory(captures);
            Type settingsType = assembly.GetType("CodexRateMonitorNative.MonitorSettings", true);
            typeof(OrbUiRegression).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static)
                .MakeGenericMethod(settingsType).Invoke(null, new object[] { captures });
            if (args.Length > 2 && args[2] == "--resource-check")
                typeof(OrbUiRegression).GetMethod("Resources", BindingFlags.NonPublic | BindingFlags.Static)
                    .MakeGenericMethod(settingsType).Invoke(null, new object[] { captures });
        Console.WriteLine("PASS " + checks + " orb settings UI, seven-preset, preview/save/cancel and DPI containment assertions; rendered sample captures generated.");
            return 0;
        }
        catch (Exception error)
        {
            while (error.InnerException != null) error = error.InnerException;
            Console.Error.WriteLine("FAIL " + error.Message);
            return 1;
        }
    }
    private static void Run<T>(string captures)
    {
        Type formType = assembly.GetType("CodexRateMonitorNative.AppearanceSettingsForm", true);
        Type dpiType = assembly.GetType("CodexRateMonitorNative.DpiAwareDialog", true);
        Type i18n = assembly.GetType("CodexRateMonitorNative.I18n", true);
        foreach (string language in new[] { "zh-CN", "zh-TW", "en" })
        foreach (int dpi in new[] { 96, 120, 144, 168, 192 })
        {
            i18n.GetMethod("SetLanguage").Invoke(null, new object[] { language });
            object settings = Activator.CreateInstance(typeof(T), true);
            Set(settings, "Language", language);
            Set(settings, "DesktopWidgetStyle", "orb");
            int previews = 0, saves = 0, cancels = 0;
            object saved = null;
            Action<T> preview = delegate(T value) { previews++; };
            Action<T> save = delegate(T value) { saves++; saved = value; };
            Action cancel = delegate { cancels++; };
            using (Form form = (Form)Activator.CreateInstance(formType, new object[] { settings, preview, save, cancel }))
            {
                form.ShowInTaskbar = false;
                form.Opacity = 0;
                form.Show();
                form.MaximumSize = new Size(6000, 6000);
                dpiType.GetMethod("ApplyDpiLayout", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(form, new object[] { dpi });
                formType.GetMethod("SetPreviewDpi").Invoke(form, new object[] { dpi });
                formType.GetMethod("FitToWorkingArea", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(form, new object[] { new Rectangle(0, 0, 1366, 768) });
                var group = (GroupBox)Field(form, "orbGroup");
                var size = (ComboBox)Field(form, "orbSize");
                var skin = (ComboBox)Field(form, "orbSkin");
                Check(skin.Items.Count == 3 && skin.SelectedIndex == 0, "Skin choices/default missing");
                for (int choice = 0; choice < 3; choice++)
                {
                    skin.SelectedIndex = choice;
                    Check((string)Get(Get(Field(form, "working"), "Orb"), "Skin") == new[] { "cream", "glacier", "lavender" }[choice], "Skin preview editing copy not updated");
                }
                Check(group.Visible, "Orb settings group is hidden: " + language + "/" + dpi);
                Check(!((GroupBox)Field(form, "stripColorsGroup")).Visible, "Strip-only colors incorrectly presented as orb colors");
                Check(size.Items.Count == 7 && size.SelectedIndex == 3, "Seven presets or compact default missing");
                for (int preset = 0; preset < 7; preset++)
                {
                    int before = previews;
                    size.SelectedIndex = preset;
                    if (preset != 3 || before != previews) Check(previews >= before, "Preset preview callback regressed");
                    Check((string)Get(Get(Field(form, "working"), "Orb"), "SizePreset") ==
                        new[] { "tiny", "small", "mini", "compact", "standard", "large", "extraLarge" }[preset], "Preset not applied to editing copy");
                }
                size.SelectedIndex = 3;
                var expanded = (CheckBox)Field(form, "orbExpandedPreview");
                expanded.Checked = false;
                Check(!(bool)Get(Field(form, "preview"), "OrbExpanded"), "Compact preview cannot be selected");
                expanded.Checked = true;
                var content = form.Controls.OfType<Panel>().Single(p => p.AutoScroll);
                foreach (Control input in Descendants(group).Where(c => c.Visible &&
                    (c is ComboBox || c is CheckBox || c is NumericUpDown || c is Button)))
                {
                    content.ScrollControlIntoView(input);
                    Rectangle visible = content.RectangleToClient(input.RectangleToScreen(input.ClientRectangle));
                    Check(content.ClientRectangle.Contains(visible), "Orb control is unreachable: " + input.Name + "/" + language + "/" + dpi);
                }
                var footer = form.Controls.OfType<Panel>().Single(p => p.Dock == DockStyle.Bottom);
                Check(form.ClientRectangle.Contains(footer.Bounds), "Save/cancel footer escaped window");
                content.AutoScrollPosition = Point.Empty;
                if (dpi == 96)
                {
                    using (Bitmap rendered = new Bitmap(form.Width, form.Height))
                    {
                        form.DrawToBitmap(rendered, new Rectangle(Point.Empty, rendered.Size));
                        rendered.Save(Path.Combine(captures, "settings-" + language + "-sample.png"), ImageFormat.Png);
                    }
                }
                var hover = (CheckBox)Field(form, "orbHover");
                hover.Checked = false;
                ((NumericUpDown)Field(form, "orbExpandDelay")).Value = 300;
                size.SelectedIndex = 5;
                string saveText = (string)i18n.GetMethod("T").Invoke(null, new object[] { "SaveClose" });
                Descendants(form).OfType<Button>().Single(b => b.Text == saveText).PerformClick();
                Check(saves == 1 && cancels == 0, "Save committed/cancelled incorrectly");
                Check((string)Get(Get(saved, "Orb"), "SizePreset") == "large", "Save lost chosen size");
                Check((string)Get(Get(saved, "Orb"), "Skin") == "lavender", "Save lost chosen skin");
                Check(!(bool)Get(Get(saved, "Orb"), "ExpandOnHover") && (int)Get(Get(saved, "Orb"), "ExpandDelayMs") == 300,
                    "Save lost orb behavior settings");
            }
            Check((string)Get(Get(settings, "Orb"), "SizePreset") == "compact", "Editing mutated original configuration");
            Check((string)Get(Get(settings, "Orb"), "Skin") == "cream", "Skin preview mutated original configuration");
            using (Form form = (Form)Activator.CreateInstance(formType, new object[] { settings, preview, save, cancel }))
            {
                form.ShowInTaskbar = false; form.Opacity = 0; form.Show();
                ((ComboBox)Field(form, "orbSize")).SelectedIndex = 4;
                ((ComboBox)Field(form, "orbSkin")).SelectedIndex = 1;
                form.Close();
                Check(cancels == 1 && saves == 1, "Closing dirty settings did not cancel exactly once");
                Check((string)Get(Get(settings, "Orb"), "Skin") == "cream", "Cancelled skin preview changed original settings");
            }
        }
    }
    private static void Resources<T>(string captures)
    {
        object settings = Activator.CreateInstance(typeof(T), true);
        Set(settings, "DesktopWidgetStyle", "orb");
        object orb = Get(settings, "Orb");
        Set(orb, "ExpandOnHover", false);
        Set(orb, "AnimationEnabled", false);
        Type formType = assembly.GetType("CodexRateMonitorNative.AppearanceSettingsForm", true);
        Type overlayType = assembly.GetType("CodexRateMonitorNative.OverlayForm", true);
        Action<T> preview = delegate(T value) { };
        Action<T> save = delegate(T value) { };
        Action cancel = delegate { };
        using (Form overlay = (Form)Activator.CreateInstance(overlayType, new object[] { settings }))
        using (Process process = Process.GetCurrentProcess())
        {
            overlay.Show();
            Application.DoEvents();
            process.Refresh();
            long beforePrivate = process.PrivateMemorySize64;
            uint beforeGdi = GetGuiResources(process.Handle, 0);
            for (int cycle = 0; cycle < 200; cycle++)
            {
                Invoke(overlay, "RequestOrbTarget", true, false);
                Invoke(overlay, "OrbTick", null, EventArgs.Empty);
                Invoke(overlay, "RequestOrbTarget", false, false);
                Invoke(overlay, "OrbTick", null, EventArgs.Empty);
                Application.DoEvents();
            }
            for (int cycle = 0; cycle < 50; cycle++)
            {
                using (Form form = (Form)Activator.CreateInstance(formType, new object[] { settings, preview, save, cancel }))
                {
                    form.ShowInTaskbar = false;
                    form.Show();
                    Application.DoEvents();
                    form.Close();
                }
                Application.DoEvents();
            }
            string output = Path.Combine(captures, "resource-stability.csv");
            var rows = new List<string>();
            rows.Add("elapsedSeconds,privateBytes,gdiHandles");
            var idle = Stopwatch.StartNew();
            double next = 0;
            while (idle.Elapsed.TotalSeconds < 120)
            {
                Application.DoEvents();
                if (idle.Elapsed.TotalSeconds >= next)
                {
                    process.Refresh();
                    rows.Add(idle.Elapsed.TotalSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "," +
                        process.PrivateMemorySize64 + "," + GetGuiResources(process.Handle, 0));
                    next += 2;
                }
                System.Threading.Thread.Sleep(20);
            }
            process.Refresh();
            long growth = process.PrivateMemorySize64 - beforePrivate;
            long gdiGrowth = (long)GetGuiResources(process.Handle, 0) - beforeGdi;
            File.WriteAllLines(output, rows.ToArray(), new System.Text.UTF8Encoding(false));
            Console.WriteLine("METRIC resource cycles=200 settings=50 idle_seconds=120 private_growth_bytes=" + growth +
                " gdi_growth=" + gdiGrowth + " baseline_private_bytes=" + beforePrivate +
                " final_private_bytes=" + process.PrivateMemorySize64);
            Check(growth <= 10L * 1024 * 1024, "UI fixture Private Bytes grew more than 10 MiB after idle");
            Check(gdiGrowth <= 20, "UI fixture GDI handles grew more than 20 after idle");
            overlay.Close();
        }
    }
}
