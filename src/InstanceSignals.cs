using System;
using System.Threading;

namespace CodexRateMonitorNative
{
    // Local, application-owned lifecycle signals. Development shutdown cannot
    // address the regular daily instance because its mutex namespace differs.
    internal sealed class InstanceSignals : IDisposable
    {
        private readonly EventWaitHandle settingsEvent;
        private readonly EventWaitHandle exitEvent;
        private readonly RegisteredWaitHandle settingsWait;
        private readonly RegisteredWaitHandle exitWait;

        public InstanceSignals(string instanceName, MonitorContext context, bool development)
        {
            settingsEvent = new EventWaitHandle(false, EventResetMode.AutoReset, instanceName + ".Settings");
            settingsWait = ThreadPool.RegisterWaitForSingleObject(settingsEvent,
                delegate(object state, bool timedOut) { context.RequestSettings(); }, null, Timeout.Infinite, false);
            if (development)
            {
                exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, instanceName + ".Exit");
                exitWait = ThreadPool.RegisterWaitForSingleObject(exitEvent,
                    delegate(object state, bool timedOut) { context.RequestDevelopmentExit(); }, null, Timeout.Infinite, false);
            }
        }
        public static bool Send(string instanceName, string action)
        {
            try
            {
                using (EventWaitHandle signal = EventWaitHandle.OpenExisting(instanceName + "." + action)) signal.Set();
                return true;
            }
            catch (WaitHandleCannotBeOpenedException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
        public void Dispose()
        {
            if (settingsWait != null) settingsWait.Unregister(null);
            if (exitWait != null) exitWait.Unregister(null);
            if (settingsEvent != null) settingsEvent.Dispose();
            if (exitEvent != null) exitEvent.Dispose();
        }
    }
}
