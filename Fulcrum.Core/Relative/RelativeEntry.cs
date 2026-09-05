namespace Fulcrum.Core.Relative
{
    /// <summary>
    /// Represents one participant positioned relative to the player.
    ///
    /// Instances are created once and reused between updates.
    /// </summary>
    public sealed class RelativeEntry
    {
        public RelativeEntry()
        {
            Reset();
        }

        /// <summary>
        /// True when this slot contains a participant.
        /// </summary>
        public bool IsValid
        {
            get;
            set;
        }

        /// <summary>
        /// iRacing car index represented by this entry.
        /// </summary>
        public int CarIndex
        {
            get;
            set;
        }

        /// <summary>
        /// Signed circular distance around the circuit.
        ///
        /// Positive values represent cars ahead.
        /// Negative values represent cars behind.
        /// </summary>
        public float RelativeDistanceLaps
        {
            get;
            set;
        }

        /// <summary>
        /// Raw time gap calculated from telemetry.
        ///
        /// Positive values represent cars ahead.
        /// Negative values represent cars behind.
        ///
        /// This property will be populated by the future GapCalculator.
        /// </summary>
        public float RawGapSeconds
        {
            get;
            set;
        }

        /// <summary>
        /// Validated and smoothed time gap intended for publication.
        ///
        /// Positive values represent cars ahead.
        /// Negative values represent cars behind.
        ///
        /// This property will be populated by the future GapFilter.
        /// </summary>
        public float FilteredGapSeconds
        {
            get;
            set;
        }

        /// <summary>
        /// True when RawGapSeconds contains a usable time gap.
        /// </summary>
        public bool HasValidRawGap
        {
            get;
            set;
        }

        /// <summary>
        /// True when FilteredGapSeconds contains a usable time gap.
        /// </summary>
        public bool HasValidFilteredGap
        {
            get;
            set;
        }

        public float DiagnosticPlayerLapDistPct { get; set; }
        public float DiagnosticOtherLapDistPct { get; set; }
        public int DiagnosticPlayerLapCompleted { get; set; }
        public int DiagnosticOtherLapCompleted { get; set; }
        public float DiagnosticPlayerEstTime { get; set; }
        public float DiagnosticOtherEstTime { get; set; }
        public float DiagnosticPlayerF2Time { get; set; }
        public float DiagnosticOtherF2Time { get; set; }
        public float DiagnosticDirectEstDifference { get; set; }
        public float DiagnosticCandidateMinusLap { get; set; }
        public float DiagnosticCandidatePlusLap { get; set; }
        public float DiagnosticLapDuration { get; set; }
        public float DiagnosticPlayerMapTime { get; set; }
        public float DiagnosticOtherMapTime { get; set; }
        public string DiagnosticGapMethod { get; set; }

        /// <summary>
        /// Difference in completed laps relative to the player.
        ///
        /// Positive values mean the participant has completed
        /// more laps than the player.
        /// </summary>
        public int LapDifference
        {
            get;
            set;
        }

        /// <summary>
        /// Continuous race position:
        ///
        /// completed laps + normalized lap distance.
        /// </summary>
        public double ContinuousTrackPosition
        {
            get;
            set;
        }

        /// <summary>
        /// Overall race position reported by telemetry.
        /// </summary>
        public int OverallPosition
        {
            get;
            set;
        }

        /// <summary>
        /// Class position reported by telemetry.
        /// </summary>
        public int ClassPosition
        {
            get;
            set;
        }

        /// <summary>
        /// Raw iRacing track-surface state.
        /// </summary>
        public int TrackSurface
        {
            get;
            set;
        }

        /// <summary>
        /// Clears all dynamic values so this object can be reused.
        /// </summary>
        public void Reset()
        {
            IsValid = false;
            CarIndex = -1;

            RelativeDistanceLaps = 0.0f;

            RawGapSeconds = 0.0f;
            FilteredGapSeconds = 0.0f;

            HasValidRawGap = false;
            HasValidFilteredGap = false;
            DiagnosticPlayerLapDistPct = 0.0f;
            DiagnosticOtherLapDistPct = 0.0f;
            DiagnosticPlayerLapCompleted = 0;
            DiagnosticOtherLapCompleted = 0;
            DiagnosticPlayerEstTime = 0.0f;
            DiagnosticOtherEstTime = 0.0f;
            DiagnosticPlayerF2Time = 0.0f;
            DiagnosticOtherF2Time = 0.0f;
            DiagnosticDirectEstDifference = 0.0f;
            DiagnosticCandidateMinusLap = 0.0f;
            DiagnosticCandidatePlusLap = 0.0f;
            DiagnosticLapDuration = 0.0f;
            DiagnosticPlayerMapTime = 0.0f;
            DiagnosticOtherMapTime = 0.0f;
            DiagnosticGapMethod = string.Empty;

            LapDifference = 0;
            ContinuousTrackPosition = 0.0;

            OverallPosition = 0;
            ClassPosition = 0;
            TrackSurface = -1;
        }

        /// <summary>
        /// Copies all values from another reusable RelativeEntry.
        /// </summary>
        public void CopyFrom(
            RelativeEntry source)
        {
            if (source == null)
            {
                Reset();

                return;
            }

            IsValid =
                source.IsValid;

            CarIndex =
                source.CarIndex;

            RelativeDistanceLaps =
                source.RelativeDistanceLaps;

            RawGapSeconds =
                source.RawGapSeconds;

            FilteredGapSeconds =
                source.FilteredGapSeconds;

            HasValidRawGap =
                source.HasValidRawGap;

            HasValidFilteredGap =
                source.HasValidFilteredGap;
            DiagnosticPlayerLapDistPct = source.DiagnosticPlayerLapDistPct;
            DiagnosticOtherLapDistPct = source.DiagnosticOtherLapDistPct;
            DiagnosticPlayerLapCompleted = source.DiagnosticPlayerLapCompleted;
            DiagnosticOtherLapCompleted = source.DiagnosticOtherLapCompleted;
            DiagnosticPlayerEstTime = source.DiagnosticPlayerEstTime;
            DiagnosticOtherEstTime = source.DiagnosticOtherEstTime;
            DiagnosticPlayerF2Time = source.DiagnosticPlayerF2Time;
            DiagnosticOtherF2Time = source.DiagnosticOtherF2Time;
            DiagnosticDirectEstDifference = source.DiagnosticDirectEstDifference;
            DiagnosticCandidateMinusLap = source.DiagnosticCandidateMinusLap;
            DiagnosticCandidatePlusLap = source.DiagnosticCandidatePlusLap;
            DiagnosticLapDuration = source.DiagnosticLapDuration;
            DiagnosticPlayerMapTime = source.DiagnosticPlayerMapTime;
            DiagnosticOtherMapTime = source.DiagnosticOtherMapTime;
            DiagnosticGapMethod = source.DiagnosticGapMethod ?? string.Empty;

            LapDifference =
                source.LapDifference;

            ContinuousTrackPosition =
                source.ContinuousTrackPosition;

            OverallPosition =
                source.OverallPosition;

            ClassPosition =
                source.ClassPosition;

            TrackSurface =
                source.TrackSurface;
        }

        /// <summary>
        /// Initializes this Relative entry from participant telemetry.
        ///
        /// Gap values are reset because they will be calculated
        /// independently by the gap engine.
        /// </summary>
        public void SetFromParticipant(
            ParticipantSnapshot participant,
            float relativeDistanceLaps,
            int lapDifference,
            double continuousTrackPosition)
        {
            if (participant == null)
            {
                Reset();

                return;
            }

            IsValid = true;

            CarIndex =
                participant.CarIndex;

            RelativeDistanceLaps =
                relativeDistanceLaps;

            RawGapSeconds = 0.0f;
            FilteredGapSeconds = 0.0f;

            HasValidRawGap = false;
            HasValidFilteredGap = false;

            LapDifference =
                lapDifference;

            ContinuousTrackPosition =
                continuousTrackPosition;

            OverallPosition =
                participant.OverallPosition;

            ClassPosition =
                participant.ClassPosition;

            TrackSurface =
                participant.TrackSurface;
        }
    }
}