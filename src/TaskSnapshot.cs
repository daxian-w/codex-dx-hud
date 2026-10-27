using System;
using System.Globalization;

namespace CodexRateMonitorNative
{
    // A monitor-owned presentation model, not an upstream protocol response.
    internal sealed class TaskSnapshot
    {
        public string SourceKind { get; set; }
        public string SourceVersion { get; set; }
        public string Coverage { get; set; }
        public string ThreadId { get; set; }
        public string TurnId { get; set; }
        public string State { get; set; }
        public int? ActiveCount { get; set; }
        public int? CompletedSteps { get; set; }
        public int? TotalSteps { get; set; }
        public DateTime ObservedAtUtc { get; set; }
        public DateTime LastConfirmedAtUtc { get; set; }
        public bool IsStale { get; set; }
        public string ErrorCode { get; set; }
        public int AccountEpoch { get; set; }
        public int ConnectionGeneration { get; set; }

        public TaskSnapshot()
        {
            State = "unavailable";
            Coverage = "unavailable";
            ObservedAtUtc = DateTime.UtcNow;
        }

        public TaskSnapshot Clone() { return (TaskSnapshot)MemberwiseClone(); }

        public bool HasProgress
        {
            get
            {
                return !IsStale && Coverage != "unavailable" && CompletedSteps.HasValue && TotalSteps.HasValue &&
                    TotalSteps.Value > 0 && CompletedSteps.Value >= 0 && CompletedSteps.Value <= TotalSteps.Value;
            }
        }

        public string ProgressText
        {
            get { return HasProgress ? CompletedSteps.Value.ToString(CultureInfo.InvariantCulture) + "/" +
                TotalSteps.Value.ToString(CultureInfo.InvariantCulture) : "—"; }
        }

        public string StatusText(string language)
        {
            if (IsStale) return I18n.Translate("TaskStale", language);
            string key;
            switch (State)
            {
                case "loading": key = "TaskLoading"; break;
                case "idle": key = Coverage == "full" ? "TaskIdle" : "TaskNoRecentActivity"; break;
                case "running": key = "TaskRunning"; break;
                case "waiting": key = "TaskWaiting"; break;
                case "waitingInput": key = "TaskWaitingInput"; break;
                case "completed": key = "TaskCompleted"; break;
                case "ended": key = "TaskEnded"; break;
                case "failed": key = "TaskFailed"; break;
                case "interrupted": key = "TaskInterrupted"; break;
                default: key = "TaskUnavailable"; break;
            }
            string text = I18n.Translate(key, language);
            if (Coverage == "partial" && ActiveCount.HasValue && State == "running")
                text = string.Format(CultureInfo.CurrentCulture, I18n.Translate("TaskObserved", language), ActiveCount.Value);
            return text;
        }

        public string DetailText(string language)
        {
            if (IsStale) return I18n.Translate("CachedDataStale", language);
            if (ErrorCode == "account-verifying") return I18n.Translate("TaskAccountVerifying", language);
            if (ErrorCode == "no-verifiable-lifecycle") return I18n.Translate("TaskLifecycleMissing", language);
            if (Coverage == "unavailable") return I18n.Translate("TaskSourceUnavailable", language);
            if (HasProgress) return I18n.Translate("TaskSteps", language) + " " + ProgressText;
            return I18n.Translate(State == "running" ? "TaskNoSteps" : "TaskPartialScope", language);
        }
    }
}
