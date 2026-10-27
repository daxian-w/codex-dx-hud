using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Collections.Generic;

internal static class TaskStateProbe
{
    private static object Get(object value, string name) { return value.GetType().GetProperty(name).GetValue(value, null); }
    private static int Main(string[] args)
    {
        try
        {
            Assembly assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            Type type = assembly.GetType("CodexRateMonitorNative.TaskStatusProvider", true);
            string root = (string)type.GetProperty("DefaultRoot").GetValue(null, null);
            object provider = Activator.CreateInstance(type, new object[] { root });
            MethodInfo read = type.GetMethod("ReadSnapshot", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                for (int sample = 0; sample < 3; sample++)
                {
                    object snapshot = read.Invoke(provider, new object[] { DateTime.UtcNow, 5 });
                    for (int pass = 0; pass < 32 && (string)Get(snapshot, "ErrorCode") == "bootstrap-pending"; pass++)
                        snapshot = read.Invoke(provider, new object[] { DateTime.UtcNow, 5 });
                    string fingerprint;
                    using (SHA256 hash = SHA256.Create())
                        fingerprint = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(
                            (string)Get(snapshot, "ThreadId") ?? ""))).Replace("-", "").ToLowerInvariant().Substring(0, 12);
                    var result = new Dictionary<string, object>();
                    foreach (string name in new[] { "SourceKind", "SourceVersion", "Coverage", "State", "ActiveCount",
                        "IsStale", "ErrorCode", "LastConfirmedAtUtc", "ObservedAtUtc" }) result[name] = Get(snapshot, name);
                    result["TargetFingerprint"] = fingerprint;
                    result["ProbeSample"] = sample;
                    result["ReadMode"] = "fresh read-only provider, not the existing monitor process";
                    Console.WriteLine(new JavaScriptSerializer().Serialize(result));
                    if (sample < 2) Thread.Sleep(5000);
                }
            }
            finally { ((IDisposable)provider).Dispose(); }
            return 0;
        }
        catch (Exception error)
        {
            while (error.InnerException != null) error = error.InnerException;
            Console.Error.WriteLine("Probe failed: " + error.GetType().Name);
            return 1;
        }
    }
}
