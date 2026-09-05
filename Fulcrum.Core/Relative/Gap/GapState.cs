namespace Fulcrum.Core.Relative.Gap
{
    /// <summary>
    /// Stores player-centred timing and motion history for one participant.
    /// </summary>
    public sealed class GapState
    {
        public GapState()
        {
            Reset();
        }

        public bool IsInitialized { get; set; }
        public float LastRawGapSeconds { get; set; }
        public float FilteredGapSeconds { get; set; }
        public float TargetGapSeconds { get; set; }
        public double LastUpdateSessionTime { get; set; }

        public bool HasPendingJump { get; set; }
        public float PendingJumpSeconds { get; set; }
        public int PendingJumpConfirmationCount { get; set; }
        public int MissingUpdateCount { get; set; }

        public float RawSample0 { get; set; }
        public float RawSample1 { get; set; }
        public float RawSample2 { get; set; }
        public int RawSampleCount { get; set; }
        public int RawSampleIndex { get; set; }

        public bool HasTrackPositions { get; set; }
        public float LastPlayerLapDistancePercent { get; set; }
        public float LastOtherLapDistancePercent { get; set; }
        public float LastRelativeDistanceLaps { get; set; }
        public float SmoothedPlayerProgressRate { get; set; }
        public float SmoothedOtherProgressRate { get; set; }

        public void Reset()
        {
            IsInitialized = false;
            LastRawGapSeconds = 0.0f;
            FilteredGapSeconds = 0.0f;
            TargetGapSeconds = 0.0f;
            LastUpdateSessionTime = -1.0;

            HasPendingJump = false;
            PendingJumpSeconds = 0.0f;
            PendingJumpConfirmationCount = 0;
            MissingUpdateCount = 0;

            RawSample0 = 0.0f;
            RawSample1 = 0.0f;
            RawSample2 = 0.0f;
            RawSampleCount = 0;
            RawSampleIndex = 0;

            HasTrackPositions = false;
            LastPlayerLapDistancePercent = 0.0f;
            LastOtherLapDistancePercent = 0.0f;
            LastRelativeDistanceLaps = 0.0f;
            SmoothedPlayerProgressRate = 0.0f;
            SmoothedOtherProgressRate = 0.0f;
        }
    }
}
