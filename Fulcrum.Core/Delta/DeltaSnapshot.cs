using System;

namespace Fulcrum.Core.Delta
{
    /// <summary>
    /// Reusable state produced by the Fulcrum Delta engine.
    /// Negative delta means the player is improving; positive means time is being lost.
    /// </summary>
    public sealed class DeltaSnapshot
    {
        public DateTime CapturedAt { get; set; }
        public bool Ready { get; set; }
        public bool IsValid { get; set; }
        public string Reference { get; set; }
        public float RawDeltaSeconds { get; set; }
        public float DeltaSeconds { get; set; }
        public float DeltaRateSecondsPerSecond { get; set; }
        public float BarValue { get; set; }
        public string DeltaText { get; set; }
        public string Direction { get; set; }
        public string Trend { get; set; }
        public bool IsImproving { get; set; }
        public bool IsLosing { get; set; }
        public bool IsNeutral { get; set; }
        public float CurrentLapTimeSeconds { get; set; }
        public float LastLapTimeSeconds { get; set; }
        public float BestLapTimeSeconds { get; set; }
        public string CurrentLapTimeText { get; set; }
        public string LastLapTimeText { get; set; }
        public string BestLapTimeText { get; set; }
        public string Status { get; set; }

        public DeltaSnapshot()
        {
            Reset();
        }

        public void Reset()
        {
            CapturedAt = DateTime.MinValue;
            Ready = false;
            IsValid = false;
            Reference = "Unavailable";
            RawDeltaSeconds = 0.0f;
            DeltaSeconds = 0.0f;
            DeltaRateSecondsPerSecond = 0.0f;
            BarValue = 0.0f;
            DeltaText = "--.---";
            Direction = "Neutral";
            Trend = "Stable";
            IsImproving = false;
            IsLosing = false;
            IsNeutral = true;
            CurrentLapTimeSeconds = 0.0f;
            LastLapTimeSeconds = 0.0f;
            BestLapTimeSeconds = 0.0f;
            CurrentLapTimeText = "--:--.---";
            LastLapTimeText = "--:--.---";
            BestLapTimeText = "--:--.---";
            Status = "Unavailable";
        }
    }
}
