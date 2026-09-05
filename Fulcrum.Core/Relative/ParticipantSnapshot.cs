namespace Fulcrum.Core.Relative
{
    /// <summary>
    /// Stores the latest known telemetry state for one participant.
    ///
    /// Instances are reused between telemetry updates to avoid
    /// allocating new objects while a race is running.
    /// </summary>
    public sealed class ParticipantSnapshot
    {
        public ParticipantSnapshot(
            int carIndex)
        {
            CarIndex = carIndex;

            Reset();
        }

        /// <summary>
        /// Permanent iRacing car index represented by this object.
        /// </summary>
        public int CarIndex
        {
            get;
            private set;
        }

        /// <summary>Static iRacing vehicle model identifier from SessionInfo.</summary>
        public int CarId { get; set; }

        /// <summary>Estimated lap time for this car class from SessionInfo.</summary>
        public float CarClassEstimatedLapTime { get; set; }

        /// <summary>
        /// True when this participant currently contains usable data.
        /// </summary>
        public bool IsValid
        {
            get;
            set;
        }

        /// <summary>
        /// True when this participant represents the local player.
        /// </summary>
        public bool IsPlayer
        {
            get;
            set;
        }

        /// <summary>
        /// Current lap reported by telemetry.
        /// </summary>
        public int Lap
        {
            get;
            set;
        }

        /// <summary>
        /// Number of completed laps.
        /// </summary>
        public int LapCompleted
        {
            get;
            set;
        }

        /// <summary>
        /// Normalized position around the current lap.
        ///
        /// Typical values range from 0.0 to 1.0.
        /// </summary>
        public float LapDistancePercent
        {
            get;
            set;
        }

        /// <summary>
        /// Raw iRacing class identifier from CarIdxClass.
        /// </summary>
        public int ClassId
        {
            get;
            set;
        }

        /// <summary>
        /// Overall race position.
        /// </summary>
        public int OverallPosition
        {
            get;
            set;
        }

        /// <summary>
        /// Position within the participant's vehicle class.
        /// </summary>
        public bool IsClassifiedParticipant { get; set; }
        public int ClassSize { get; set; }
        public int PositionGainLoss { get; set; }
        public bool PositionGainLossAvailable { get; set; }

        public int ClassPosition
        {
            get;
            set;
        }

        /// <summary>
        /// Raw track-surface state reported by iRacing.
        ///
        /// We keep the raw integer for now and will translate it
        /// into a strongly typed status after validating its values.
        /// </summary>
        public int TrackSurface
        {
            get;
            set;
        }

        /// <summary>
        /// True when iRacing reports this participant on pit road.
        /// </summary>
        public bool IsOnPitRoad
        {
            get;
            set;
        }

        /// <summary>
        /// Current engine speed reported for the participant.
        /// </summary>
        public float Rpm
        {
            get;
            set;
        }

        /// <summary>
        /// Current gear reported for the participant.
        /// </summary>
        public int Gear
        {
            get;
            set;
        }

        /// <summary>
        /// Estimated time associated with the participant's
        /// current track position.
        /// </summary>
        public float EstimatedTime
        {
            get;
            set;
        }

        /// <summary>
        /// Native iRacing F2 timing value for this participant.
        /// In race sessions this is time behind the leader; in other
        /// sessions it may represent fastest-lap timing, so it must not
        /// be treated as a universal physical Relative gap.
        /// </summary>
        public float F2Time
        {
            get;
            set;
        }

        /// <summary>
        /// Latest completed lap time.
        /// </summary>
        public float LastLapTime
        {
            get;
            set;
        }

        /// <summary>
        /// Best lap time recorded for this participant.
        /// </summary>
        public float BestLapTime
        {
            get;
            set;
        }

        /// <summary>Raw iRacing tire compound identifier for this car.</summary>
        public int TireCompound { get; set; }


        /// <summary>Raw per-car Push-to-Pass counter from iRacing telemetry.</summary>
        public int RawPushToPassCount { get; set; }

        /// <summary>Raw per-car Push-to-Pass status from iRacing telemetry.</summary>
        public int RawPushToPassStatus { get; set; }

        /// <summary>True when P2P telemetry is present for this participant.</summary>
        public bool HasPushToPassTelemetry { get; set; }

        /// <summary>True when this car uses a supported overtake/P2P system.</summary>
        public bool OvertakeSupported { get; set; }

        /// <summary>True while the overtake/P2P system is actively being used.</summary>
        public bool OvertakeActive { get; set; }

        /// <summary>Remaining overtake/P2P time/count presented to the Relative.</summary>
        public int OvertakeRemaining { get; set; }

        /// <summary>
        /// Per-driver iRacing session flags from CarIdxSessionFlags.
        /// Includes individual black, repair/meatball and disqualification flags.
        /// </summary>
        public long SessionFlags { get; set; }

        /// <summary>
        /// Clears all dynamic participant data while preserving
        /// the permanent car index.
        /// </summary>
        public void Reset()
        {
            IsValid = false;
            IsPlayer = false;
            CarId = 0;
            CarClassEstimatedLapTime = 0.0f;

            Lap = 0;
            LapCompleted = 0;
            LapDistancePercent = 0.0f;

            ClassId = -1;
            OverallPosition = 0;
            ClassPosition = 0;
            IsClassifiedParticipant = false;
            ClassSize = 0;
            PositionGainLoss = 0;
            PositionGainLossAvailable = false;
            TrackSurface = 0;
            IsOnPitRoad = false;

            Rpm = 0.0f;
            Gear = 0;

            EstimatedTime = 0.0f;
            F2Time = 0.0f;
            LastLapTime = 0.0f;
            BestLapTime = 0.0f;
            TireCompound = -1;
            RawPushToPassCount = 0;
            RawPushToPassStatus = 0;
            HasPushToPassTelemetry = false;
            OvertakeSupported = false;
            OvertakeActive = false;
            OvertakeRemaining = 0;
            SessionFlags = 0L;
        }
    }
}
