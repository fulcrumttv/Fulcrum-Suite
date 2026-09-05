using System;

namespace Fulcrum.Core.Events
{
    /// <summary>
    /// Consolidated event state produced from the active Fulcrum modules.
    /// </summary>
    public sealed class EventHubSnapshot
    {
        public bool Ready { get; set; }
        public string LastEventName { get; set; }
        public string Category { get; set; }
        public string Message { get; set; }
        public string SuggestedAction { get; set; }
        public int Priority { get; set; }
        public bool IsUrgent { get; set; }
        public int Sequence { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public int ActiveAlertCount { get; set; }
        public int HighestActivePriority { get; set; }
        public string ActiveAlerts { get; set; }

        public EventHubSnapshot()
        {
            Reset();
        }

        public void Reset()
        {
            Ready = false;
            LastEventName = "None";
            Category = "None";
            Message = string.Empty;
            SuggestedAction = "Maintain pace";
            Priority = 0;
            IsUrgent = false;
            Sequence = 0;
            OccurredAtUtc = DateTime.MinValue;
            ActiveAlertCount = 0;
            HighestActivePriority = 0;
            ActiveAlerts = string.Empty;
        }
    }
}
