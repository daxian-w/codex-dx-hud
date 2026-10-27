using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Windows.Forms;

internal static class TaskPollingRegression
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Assembly assembly;
    private static object provider, context;
    private static Form overlay;
    private static int checks, received, applied, expectedEpoch = 1;
    private static Exception dispatchError;
    private static readonly List<double> latencies = new List<double>();
    private static int faultedCallbacks;
    private static void FaultedSubscriber<T>(T snapshot)
    {
        Interlocked.Increment(ref faultedCallbacks);
        throw new InvalidOperationException("Synthetic subscriber failure");
    }
    private static object Get(object item, string name) { return item.GetType().GetProperty(name).GetValue(item, null); }
    private static void Set(object item, string name, object value) { item.GetType().GetProperty(name).SetValue(item, value, null); }
    private static object Field(object item, string name) { return item.GetType().GetField(name, Instance).GetValue(item); }
    private static object Call(object item, string name, params object[] args) { return item.GetType().GetMethod(name, Instance).Invoke(item, args); }
    private static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    private static void Deliver<T>(T snapshot)
    {
        Interlocked.Increment(ref received);
        long parsedAt = Stopwatch.GetTimestamp();
        try
        {
            // Exercise the product's real UI marshalling method without starting
            // a full monitor, authenticating, or connecting any app-server.
            Call(context, "Ui", (Action)delegate
            {
                if ((int)Get(snapshot, "AccountEpoch") != expectedEpoch) return;
                Call(overlay, "SetTaskSnapshot", snapshot);
                latencies.Add((Stopwatch.GetTimestamp() - parsedAt) * 1000d / Stopwatch.Frequency);
                applied++;
            });
        }
        catch (Exception error) { dispatchError = error; }
    }
    private static void PumpUntil(Func<bool> done, int timeoutMilliseconds)
    {
        var watch = Stopwatch.StartNew();
        while (!done() && watch.ElapsedMilliseconds < timeoutMilliseconds)
        {
            Call(provider, "Poll", true);
            Application.DoEvents();
            Thread.Sleep(20);
            if (dispatchError != null) throw dispatchError;
        }
        Check(done(), "Timed out waiting for asynchronous provider/UI propagation");
    }
    private static string Event(string type, DateTime timestamp)
    {
        return "{\"timestamp\":\"" + timestamp.ToString("o") + "\",\"type\":\"event_msg\",\"payload\":{\"type\":\"" +
            type + "\",\"turn_id\":\"fixture-turn\"}}\n";
    }
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            string directory = Path.Combine(Path.GetFullPath(args[1]), DateTime.UtcNow.ToString("yyyy/MM/dd"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "fixture.jsonl");
            File.WriteAllText(path, "{\"type\":\"session_meta\",\"payload\":{\"id\":\"fixture\",\"originator\":\"Codex Desktop\",\"cli_version\":\"fixture\",\"source\":\"vscode\"}}\n" +
                Event("task_started", DateTime.UtcNow.AddMinutes(-10)), new UTF8Encoding(false));
            object settings = Activator.CreateInstance(assembly.GetType("CodexRateMonitorNative.MonitorSettings"), true);
            Set(settings, "DesktopWidgetStyle", "orb");
            Set(Get(settings, "Orb"), "ExpandOnHover", false);
            overlay = (Form)Activator.CreateInstance(assembly.GetType("CodexRateMonitorNative.OverlayForm"), new object[] { settings });
            overlay.Text = "Task polling fixture";
            overlay.Show();
            context = FormatterServices.GetUninitializedObject(assembly.GetType("CodexRateMonitorNative.MonitorContext"));
            context.GetType().GetField("overlay", Instance).SetValue(context, overlay);
            Type providerType = assembly.GetType("CodexRateMonitorNative.TaskStatusProvider");
            provider = Activator.CreateInstance(providerType, new object[] { Path.GetFullPath(args[1]) });
            Type snapshotType = assembly.GetType("CodexRateMonitorNative.TaskSnapshot");
            MethodInfo deliver = typeof(TaskPollingRegression).GetMethod("Deliver", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(snapshotType);
            EventInfo receivedEvent = providerType.GetEvent("SnapshotReceived");
            receivedEvent.AddEventHandler(provider, Delegate.CreateDelegate(receivedEvent.EventHandlerType, deliver));
            Call(provider, "Reset", expectedEpoch, false);
            PumpUntil(delegate { return applied >= 1; }, 3000);
            Check((bool)Get(Field(overlay, "taskSnapshot"), "IsStale"), "Old lifecycle was not visibly marked stale");
            int before = received;
            File.AppendAllText(path, Event("item_completed", DateTime.UtcNow), new UTF8Encoding(false));
            for (int request = 0; request < 100; request++) Call(provider, "Poll", true);
            Application.DoEvents();
            Check(received == before, "Burst polling bypassed provider cadence");
            PumpUntil(delegate { return applied >= 2; }, 7000);
            Check(!(bool)Get(Field(overlay, "taskSnapshot"), "IsStale"), "Same-turn activity did not clear UI stale state");
            Check((string)Get(Field(overlay, "taskSnapshot"), "State") == "running", "Recovered UI did not show running state");
            File.AppendAllText(path, Event("task_complete", DateTime.UtcNow), new UTF8Encoding(false));
            PumpUntil(delegate { return applied >= 3; }, 7000);
            Check((string)Get(Field(overlay, "taskSnapshot"), "State") == "ended", "Task completion did not propagate to UI");
            expectedEpoch = 2;
            Call(provider, "Reset", expectedEpoch, true);
            PumpUntil(delegate { return applied >= 4; }, 3000);
            Check((string)Get(Field(overlay, "taskSnapshot"), "Coverage") == "unavailable", "Account reset retained old task state");
            ((IDisposable)provider).Dispose();
            int disposedCount = received;
            for (int request = 0; request < 20; request++) Call(provider, "Poll", true);
            Application.DoEvents(); Thread.Sleep(100); Application.DoEvents();
            Check(received == disposedCount, "Disposed provider published further snapshots");
            latencies.Sort();
            double p95 = latencies[(int)Math.Ceiling(latencies.Count * 0.95) - 1];
            Check(p95 <= 500, "Parsed snapshot to UI p95 exceeded 500 ms");
            using (var source = (IDisposable)Activator.CreateInstance(providerType, new object[] { Path.GetFullPath(args[1]) }))
            {
                MethodInfo fault = typeof(TaskPollingRegression).GetMethod("FaultedSubscriber", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(snapshotType);
                receivedEvent.AddEventHandler(source, Delegate.CreateDelegate(receivedEvent.EventHandlerType, fault));
                Call(source, "Poll", true);
                var faultWatch = Stopwatch.StartNew();
                while (((int)Field(source, "busy") != 0 || faultedCallbacks == 0) && faultWatch.ElapsedMilliseconds < 3000)
                    Thread.Sleep(10);
                Check(faultedCallbacks == 1, "Subscriber failure was republished as a second task-source error");
                Check((int)Field(source, "busy") == 0, "Subscriber failure left polling permanently busy");
            }
            int lateCallbacks = 0;
            var queued = new Thread(delegate()
            {
                Call(context, "Ui", (Action)delegate { lateCallbacks++; });
            });
            queued.Start();
            Check(queued.Join(2000), "Late-callback fixture failed to queue");
            context.GetType().GetField("disposed", Instance).SetValue(context, true);
            Application.DoEvents();
            Check(lateCallbacks == 0, "Queued UI callback ran after MonitorContext disposal");
            Console.WriteLine("PASS " + checks + " asynchronous polling/UI bridge assertions; p95_ms=" +
                p95.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
            Console.WriteLine("Scope: synthetic source, actual Poll/Ui/SetTaskSnapshot; excludes constructor subscription, auth gate, tray refresh and existing daily process.");
            return 0;
        }
        catch (Exception error) { while (error.InnerException != null) error = error.InnerException; Console.Error.WriteLine("FAIL " + error); return 1; }
        finally
        {
            if (provider != null) ((IDisposable)provider).Dispose();
            if (overlay != null) overlay.Dispose();
        }
    }
}
