using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Diagnostics;

namespace CodexRateMonitorNative
{
    // Read-only observation of structured local lifecycle events. This is a
    // partial view of local sessions, never a global task inventory or account proof.
    internal sealed class TaskStatusProvider : IDisposable
    {
        private readonly string root;
        private readonly object gate = new object();
        private readonly Dictionary<string, Cursor> files = new Dictionary<string, Cursor>(StringComparer.OrdinalIgnoreCase);
        private readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
        private DateTime acceptAfterUtc = DateTime.MinValue;
        private double nextDiscoveryAt;
        private double nextReadAt;
        private DateTime priorWallUtc;
        private double priorMonotonic;
        private int epoch;
        private volatile bool disposed;
        private int nextFileIndex;
        private int busy;
        private int failures;
        private int resetRequested;
        private long resetAfterTicks;
        private static double Monotonic { get { return Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency; } }

        public event Action<TaskSnapshot> SnapshotReceived;
        public TaskStatusProvider(string sessionsRoot) { root = sessionsRoot; }
        public static string DefaultRoot
        {
            get
            {
                string directory = Environment.GetEnvironmentVariable("CODEX_HOME");
                if (string.IsNullOrWhiteSpace(directory))
                    directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
                return Path.Combine(directory, "sessions");
            }
        }
        public void Reset(int accountEpoch, bool discardEarlierEvents)
        {
            // Never make the UI wait for a file bootstrap scan. A running read
            // keeps its starting epoch and will be ignored after this reset.
            Interlocked.Exchange(ref epoch, accountEpoch);
            if (discardEarlierEvents)
            {
                Interlocked.Exchange(ref resetAfterTicks, DateTime.UtcNow.Ticks);
                Interlocked.Exchange(ref resetRequested, 1);
            }
            nextReadAt = 0;
        }
        public void Poll(bool expanded)
        {
            if (disposed || Monotonic < nextReadAt || Interlocked.CompareExchange(ref busy, 1, 0) != 0) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    TaskSnapshot snapshot;
                    int startedEpoch = Interlocked.CompareExchange(ref epoch, 0, 0);
                    lock (gate)
                    {
                        if (disposed) return;
                        int interval = expanded ? 5 : 15;
                        snapshot = ReadSnapshot(DateTime.UtcNow, interval);
                        snapshot.AccountEpoch = startedEpoch;
                        failures = snapshot.Coverage == "unavailable" ? Math.Min(3, failures + 1) : 0;
                        int retry = failures == 0 ? interval : failures == 1 ? 15 : failures == 2 ? 30 : 60;
                        nextReadAt = startedEpoch == epoch ? Monotonic + retry : 0;
                        if (snapshot.ErrorCode == "bootstrap-pending") nextReadAt = Monotonic + 0.25;
                    }
                    Publish(snapshot);
                }
                catch (Exception ex)
                {
                    DiagnosticLog.Write("task-source-failed", "type=" + ex.GetType().Name);
                    nextReadAt = Monotonic + 30;
                    Publish(new TaskSnapshot { AccountEpoch = epoch, ErrorCode = "read-failed" });
                }
                finally
                {
                    if (disposed) lock (gate) { files.Clear(); }
                    Interlocked.Exchange(ref busy, 0);
                }
            });
        }
        private void Publish(TaskSnapshot snapshot)
        {
            var handler = SnapshotReceived;
            if (disposed || handler == null) return;
            try { handler(snapshot); }
            catch (Exception ex)
            {
                // A failed UI subscriber must not become a second publication
                // from the read-error path or escape a ThreadPool callback.
                DiagnosticLog.Write("task-publish-failed", "type=" + ex.GetType().Name);
            }
        }
        // Synchronous deterministic entry point also exercised by fixture tests.
        internal TaskSnapshot ReadSnapshot(DateTime nowUtc, int intervalSeconds)
        {
            if (Interlocked.Exchange(ref resetRequested, 0) != 0)
            {
                acceptAfterUtc = new DateTime(Interlocked.Read(ref resetAfterTicks), DateTimeKind.Utc);
                files.Clear();
                nextFileIndex = 0;
                nextDiscoveryAt = 0;
            }
            double mono = Monotonic;
            if (priorWallUtc != DateTime.MinValue && Math.Abs((nowUtc - priorWallUtc).TotalSeconds - (mono - priorMonotonic)) > 5)
                foreach (Cursor file in files.Values)
                {
                    file.ClockUncertain = true;
                    // Only already accepted cursors retain their epoch eligibility.
                    // A reset clears these cursors, so older-account history is not re-admitted.
                    file.ClockAdjustedEpoch = file.TurnId != null;
                }
            priorWallUtc = nowUtc;
            priorMonotonic = mono;
            if (!Directory.Exists(root)) return new TaskSnapshot { ErrorCode = "source-missing" };
            if (mono >= nextDiscoveryAt)
            {
                // Bound discovery to recent date folders. Never recursively scan a
                // full account history every five seconds.
                for (int day = 0; day < 7; day++)
                {
                    DateTime date = nowUtc.Date.AddDays(-day);
                    string directory = Path.Combine(root, date.ToString("yyyy", CultureInfo.InvariantCulture),
                        date.ToString("MM", CultureInfo.InvariantCulture), date.ToString("dd", CultureInfo.InvariantCulture));
                    if (!Directory.Exists(directory)) continue;
                    foreach (FileInfo file in new DirectoryInfo(directory).GetFiles("*.jsonl")
                        .Where(f => f.LastWriteTimeUtc >= nowUtc.AddHours(-12)).OrderByDescending(f => f.LastWriteTimeUtc).Take(32))
                    {
                        if (!files.ContainsKey(file.FullName)) files[file.FullName] = new Cursor();
                    }
                }
                nextDiscoveryAt = mono + intervalSeconds;
            }
            // One budget across all files, with a rotating start to prevent a
            // growing rollout from starving later sessions. Deadlines are
            // cooperative; a single OS file read itself may take longer.
            long remainingBytes = 8L * 1024 * 1024;
            double deadline = Monotonic + 0.25;
            var entries = files.OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase).ToArray();
            int start = entries.Length == 0 ? 0 : nextFileIndex % entries.Length;
            for (int visited = 0; visited < entries.Length; visited++)
            {
                if (disposed || remainingBytes <= 0 || Monotonic >= deadline) break;
                int index = (start + visited) % entries.Length;
                var entry = entries[index];
                nextFileIndex = (index + 1) % entries.Length;
                try { ReadFile(entry.Key, entry.Value, nowUtc, ref remainingBytes, deadline); entry.Value.ReadFailed = false; }
                catch (IOException) { entry.Value.ReadFailed = true; }
                catch (UnauthorizedAccessException) { entry.Value.ReadFailed = true; }
            }
            foreach (var item in files.OrderByDescending(f => f.Value.ConfirmedAtUtc).Skip(64).ToArray()) files.Remove(item.Key);
            double staleSeconds = Math.Max(30, intervalSeconds * 2 + 10);
            Cursor[] observed = files.Values.Where(f => f.IsDesktop && f.ThreadId != null && f.TurnId != null && !f.ReadFailed && f.CaughtUp)
                .GroupBy(f => f.ThreadId).Select(g => g.OrderByDescending(f => f.ConfirmedAtUtc).First()).ToArray();
            Cursor[] active = observed.Where(f => f.State == "running" || f.State == "waiting" || f.State == "waitingInput").ToArray();
            if (!active.Any(f => Fresh(f, nowUtc, staleSeconds)) &&
                files.Values.Any(f => f.IsDesktop && !f.CaughtUp && !f.ReadFailed))
                return new TaskSnapshot { SourceKind = "local-structured-rollout", Coverage = "partial",
                    State = "loading", ErrorCode = "bootstrap-pending" };
            Cursor selected = active.Where(f => Fresh(f, nowUtc, staleSeconds))
                .OrderByDescending(f => f.State == "waiting" || f.State == "waitingInput")
                .ThenBy(f => Math.Abs((nowUtc - f.ConfirmedAtUtc).TotalSeconds)).FirstOrDefault() ??
                observed.OrderByDescending(f => Fresh(f, nowUtc, staleSeconds)).ThenByDescending(f => f.ConfirmedAtUtc).FirstOrDefault();
            if (selected == null) return new TaskSnapshot { ErrorCode = "no-verifiable-lifecycle" };
            bool stale = !Fresh(selected, nowUtc, staleSeconds);
            string state = selected.State;
            bool terminal = state == "completed" || state == "ended" || state == "failed" || state == "interrupted";
            if (terminal && Monotonic - selected.TerminalObservedAt > 5) state = "idle";
            return new TaskSnapshot
            {
                SourceKind = "local-structured-rollout", SourceVersion = selected.Version, Coverage = "partial",
                State = state, ThreadId = selected.ThreadId, TurnId = selected.TurnId,
                ActiveCount = active.Any(f => Fresh(f, nowUtc, staleSeconds))
                    ? (int?)active.Count(f => Fresh(f, nowUtc, staleSeconds)) : null,
                CompletedSteps = selected.CompletedSteps, TotalSteps = selected.TotalSteps,
                LastConfirmedAtUtc = selected.ConfirmedAtUtc, ObservedAtUtc = nowUtc, IsStale = stale,
                AccountEpoch = epoch
            };
        }
        private static bool Fresh(Cursor cursor, DateTime nowUtc, double maximumAge)
        {
            return !cursor.ClockUncertain && Math.Abs((nowUtc - cursor.ConfirmedAtUtc).TotalSeconds) <= maximumAge;
        }
        private void ReadFile(string path, Cursor cursor, DateTime nowUtc, ref long remainingBytes, double deadline)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (stream.Length < cursor.Offset) cursor.Reset();
                if (cursor.Offset > 0 && cursor.LastTail != null)
                {
                    stream.Position = cursor.Offset - cursor.LastTail.Length;
                    byte[] prior = new byte[cursor.LastTail.Length];
                    int priorCount = stream.Read(prior, 0, prior.Length);
                    if (priorCount != prior.Length || !prior.SequenceEqual(cursor.LastTail)) cursor.Reset();
                }
                // Parse metadata through the same bounded streaming path as
                // events. ReadLine would allocate an unbounded first line.
                stream.Position = cursor.Offset;
                byte[] buffer = new byte[8192];
                char[] characters = new char[Encoding.UTF8.GetMaxCharCount(buffer.Length)];
                int count;
                while (!disposed && remainingBytes > 0 && Monotonic < deadline &&
                    (count = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remainingBytes))) > 0)
                {
                    cursor.Offset += count;
                    remainingBytes -= count;
                    int charCount = cursor.Decoder.GetChars(buffer, 0, count, characters, 0, false);
                    for (int index = 0; index < charCount; index++)
                    {
                        char value = characters[index];
                        if (value == '\n')
                        {
                            if (!cursor.DiscardLine && cursor.Pending.Length > 0) ApplyLine(cursor.Pending.ToString(), cursor, nowUtc);
                            cursor.Pending.Length = 0;
                            if (cursor.Pending.Capacity > 65536) cursor.Pending.Capacity = 65536;
                            cursor.DiscardLine = false;
                        }
                        else if (!cursor.DiscardLine)
                        {
                            if (cursor.Pending.Length < 2 * 1024 * 1024) cursor.Pending.Append(value);
                            else { cursor.Pending.Length = 0; cursor.DiscardLine = true; }
                        }
                    }
                }
                cursor.CaughtUp = cursor.Offset == stream.Length;
                int tailLength = (int)Math.Min(64, cursor.Offset);
                cursor.LastTail = new byte[tailLength];
                stream.Position = cursor.Offset - tailLength;
                stream.Read(cursor.LastTail, 0, tailLength);
            }
        }
        private void ApplyLine(string line, Cursor cursor, DateTime nowUtc)
        {
            if (line.Length > 0 && line[0] == '\uFEFF') line = line.Substring(1);
            Dictionary<string, object> item;
            try { item = json.DeserializeObject(line) as Dictionary<string, object>; }
            catch (Exception) { return; }
            Dictionary<string, object> payload = Dictionary(item, "payload");
            string kind = Text(item, "type");
            if (kind == "session_meta")
            {
                cursor.ThreadId = Text(payload, "id");
                cursor.Version = Text(payload, "cli_version");
                string source = Text(payload, "source") ?? "";
                cursor.IsDesktop = Text(payload, "originator") == "Codex Desktop" &&
                    !source.StartsWith("subagent", StringComparison.OrdinalIgnoreCase) &&
                    !(payload != null && payload.ContainsKey("source") && payload["source"] is Dictionary<string, object>);
                return;
            }
            if (!cursor.IsDesktop || kind != "event_msg") return;
            DateTime timestamp;
            if (!DateTime.TryParse(Text(item, "timestamp"), CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out timestamp) ||
                (timestamp < acceptAfterUtc && !cursor.ClockAdjustedEpoch)) return;
            string eventType = Text(payload, "type");
            string turnId = Text(payload, "turn_id");
            if (eventType == "task_started")
            {
                // Physical append order is stable within this one rollout file.
                // Repeated identities cannot resurrect older turns, including
                // when wall time moves backwards between two legitimate turns.
                if (turnId == null || cursor.SeenTurns.Contains(turnId)) return;
                if (cursor.SeenTurns.Count >= 512) cursor.SeenTurns.Clear();
                cursor.SeenTurns.Add(turnId);
                if (cursor.TurnId != turnId) { cursor.CompletedSteps = null; cursor.TotalSteps = null; }
                cursor.TurnId = turnId;
                cursor.State = "running";
                cursor.LifecycleAtUtc = timestamp;
                cursor.ConfirmedAtUtc = timestamp;
                cursor.ClockUncertain = Math.Abs((nowUtc - timestamp).TotalSeconds) > 40;
            }
            else if (turnId == cursor.TurnId && turnId != null)
            {
                if (eventType == "task_complete" || eventType == "turn_aborted")
                {
                    if (cursor.State != "running" && cursor.State != "waiting" && cursor.State != "waitingInput") return;
                    // A local task_complete record proves a lifecycle end, not
                    // necessarily successful execution. Keep success distinct.
                    cursor.State = eventType == "turn_aborted" ? "interrupted" : "ended";
                    cursor.LifecycleAtUtc = timestamp;
                    cursor.ConfirmedAtUtc = timestamp;
                    cursor.ClockUncertain = Math.Abs((nowUtc - timestamp).TotalSeconds) > 40;
                    cursor.TerminalObservedAt = Monotonic;
                }
                else if (eventType == "item_completed" && cursor.State == "running")
                {
                    cursor.ConfirmedAtUtc = timestamp;
                    cursor.ClockUncertain = Math.Abs((nowUtc - timestamp).TotalSeconds) > 40;
                }
                // No plan parser until M0 proves its exact structured local format.
                // Token/command counts never become plan progress.
            }
        }
        private static Dictionary<string, object> Dictionary(Dictionary<string, object> values, string key)
        {
            object result;
            return values != null && values.TryGetValue(key, out result) ? result as Dictionary<string, object> : null;
        }
        private static string Text(Dictionary<string, object> values, string key)
        {
            object result;
            return values != null && values.TryGetValue(key, out result) ? result as string : null;
        }
        public void Dispose()
        {
            disposed = true;
            // Never make UI shutdown wait for disk I/O. The active worker
            // observes disposed and clears managed cursors in its finally.
            if (Monitor.TryEnter(gate))
            {
                try { files.Clear(); }
                finally { Monitor.Exit(gate); }
            }
        }
        private sealed class Cursor
        {
            public long Offset;
            public readonly StringBuilder Pending = new StringBuilder();
            public Decoder Decoder = Encoding.UTF8.GetDecoder();
            public byte[] LastTail;
            public bool DiscardLine, IsDesktop, ReadFailed, CaughtUp, ClockUncertain, ClockAdjustedEpoch;
            public readonly HashSet<string> SeenTurns = new HashSet<string>();
            public double TerminalObservedAt;
            public string ThreadId, TurnId, State, Version;
            public DateTime ConfirmedAtUtc, LifecycleAtUtc;
            public int? CompletedSteps, TotalSteps;
            public void Reset()
            {
                Offset = 0; Pending.Length = 0; Decoder = Encoding.UTF8.GetDecoder(); DiscardLine = false;
                LastTail = null;
                IsDesktop = false; ThreadId = null; TurnId = null; State = null;
                CaughtUp = false;
                ClockUncertain = false; ClockAdjustedEpoch = false; SeenTurns.Clear(); TerminalObservedAt = 0;
                ConfirmedAtUtc = LifecycleAtUtc = DateTime.MinValue;
                CompletedSteps = TotalSteps = null;
            }
        }
    }
}
