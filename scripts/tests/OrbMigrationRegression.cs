using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

internal static class OrbMigrationRegression
{
    private static object Get(object target, string name) { return target.GetType().GetProperty(name).GetValue(target, null); }
    private static void Set(object target, string name, object value) { target.GetType().GetProperty(name).SetValue(target, value, null); }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static string LegacyJson(object settings)
    {
        var serializer = new JavaScriptSerializer();
        var values = serializer.Deserialize<Dictionary<string, object>>(serializer.Serialize(settings));
        values.Remove("SettingsSchemaVersion"); values.Remove("DesktopWidgetStyle"); values.Remove("Orb");
        // Compare semantic object values, not dictionary insertion order.
        return Canonical(values);
    }
    private static string Canonical(object value)
    {
        var map = value as Dictionary<string, object>;
        if (map == null) return new JavaScriptSerializer().Serialize(value);
        var keys = new List<string>(map.Keys); keys.Sort(StringComparer.Ordinal);
        var result = new List<string>();
        foreach (string key in keys) result.Add(new JavaScriptSerializer().Serialize(key) + ":" + Canonical(map[key]));
        return "{" + string.Join(",", result.ToArray()) + "}";
    }
    private static int Main(string[] args)
    {
        try
        {
            Assembly assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            Type type = assembly.GetType("CodexRateMonitorNative.MonitorSettings", true);
            object settings = type.GetMethod("Load").Invoke(null, null);
            string root = AppDomain.CurrentDomain.BaseDirectory;
            string snapshot = Path.Combine(root, "legacy-normalized.json");
            if (args[1] == "capture")
                File.WriteAllText(snapshot, LegacyJson(settings), new UTF8Encoding(false));
            else
            {
                Check(LegacyJson(settings) == File.ReadAllText(snapshot, Encoding.UTF8), "Legacy settings changed during migration/rollback");
                if (args[1] == "migrate")
                {
                    Check((string)Get(settings, "DesktopWidgetStyle") == "strip", "Upgrade forced orb mode");
                    Check((string)Get(Get(settings, "Orb"), "SizePreset") == "compact", "Upgrade lost compact default");
                    Set(settings, "DesktopWidgetStyle", "orb");
                    Set(Get(settings, "Orb"), "SizePreset", "large");
                    type.GetMethod("SaveTo", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(settings,
                        new object[] { Path.Combine(root, "settings.json") });
                    object reloaded = type.GetMethod("Load").Invoke(null, null);
                    Check((string)Get(reloaded, "DesktopWidgetStyle") == "orb" &&
                        (string)Get(Get(reloaded, "Orb"), "SizePreset") == "large", "Saved orb settings did not persist");
                    Check(LegacyJson(reloaded) == File.ReadAllText(snapshot, Encoding.UTF8), "Orb save changed legacy preferences");
                }
            }
            Console.WriteLine("PASS configuration " + args[1] + " using " + assembly.GetName().Version);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("FAIL " + error); return 1; }
    }
}
