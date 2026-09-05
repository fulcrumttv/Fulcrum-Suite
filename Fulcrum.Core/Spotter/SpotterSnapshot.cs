using System;

namespace Fulcrum.Core.Spotter
{
    /// <summary>
    /// Stable, dashboard-ready spotter state. EventSequence changes only
    /// when a new spoken/visual callout should be consumed by an overlay.
    /// </summary>
    public sealed class SpotterSnapshot
    {
        public bool Ready { get; set; }
        public string State { get; set; }
        public string Callout { get; set; }
        public string CalloutCode { get; set; }
        public int Priority { get; set; }
        public bool IsUrgent { get; set; }
        public bool HasActiveCallout { get; set; }

        public bool HasCarLeft { get; set; }
        public bool HasCarRight { get; set; }
        public bool HasCarsBothSides { get; set; }
        public bool IsClear { get; set; }
        public bool BlueFlag { get; set; }
        public bool YellowFlag { get; set; }
        public bool MeatballFlag { get; set; }
        public bool FasterClassApproaching { get; set; }
        public bool DefenseRequired { get; set; }

        public string SuggestedAction { get; set; }
        public string EventName { get; set; }
        public int EventSequence { get; set; }
        public DateTime UpdatedAtUtc { get; set; }

        public SpotterSnapshot()
        {
            Reset();
        }

        public void Reset()
        {
            Ready = false;
            State = "Unavailable";
            Callout = string.Empty;
            CalloutCode = "NONE";
            Priority = 0;
            IsUrgent = false;
            HasActiveCallout = false;

            HasCarLeft = false;
            HasCarRight = false;
            HasCarsBothSides = false;
            IsClear = true;
            BlueFlag = false;
            YellowFlag = false;
            MeatballFlag = false;
            FasterClassApproaching = false;
            DefenseRequired = false;

            SuggestedAction = "Maintain pace";
            EventName = "None";
            EventSequence = 0;
            UpdatedAtUtc = DateTime.MinValue;
        }
    }
}
