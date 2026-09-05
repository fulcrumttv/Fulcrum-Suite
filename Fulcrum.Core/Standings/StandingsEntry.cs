namespace Fulcrum.Core.Standings
{
    public sealed class StandingsEntry
    {
        public bool HasData { get; private set; }
        public bool IsPlayer { get; private set; }
        public bool IsSameClass { get; private set; }
        public int CarIndex { get; private set; }
        public int OverallPosition { get; private set; }
        public int ClassPosition { get; private set; }
        public int Lap { get; private set; }
        public int LapCompleted { get; private set; }
        public int LapDifferenceToLeader { get; private set; }
        public float GapToLeaderSeconds { get; private set; }
        public bool HasGapToLeader { get; private set; }
        public float LastLapTime { get; private set; }
        public float BestLapTime { get; private set; }
        public int TrackSurface { get; private set; }
        public string TrackStatus { get; private set; }
        public bool IsInPits { get; private set; }
        public string DriverName { get; private set; }
        public string CarNumber { get; private set; }
        public string TeamName { get; private set; }
        public string ClassName { get; private set; }
        public string Manufacturer { get; private set; }
        public int IRating { get; private set; }
        public string License { get; private set; }

        public StandingsEntry() { Reset(); }

        public void Reset()
        {
            HasData = false;
            IsPlayer = false;
            IsSameClass = false;
            CarIndex = -1;
            OverallPosition = 0;
            ClassPosition = 0;
            Lap = 0;
            LapCompleted = 0;
            LapDifferenceToLeader = 0;
            GapToLeaderSeconds = 0.0f;
            HasGapToLeader = false;
            LastLapTime = 0.0f;
            BestLapTime = 0.0f;
            TrackSurface = -1;
            TrackStatus = "NotInWorld";
            IsInPits = false;
            DriverName = string.Empty;
            CarNumber = string.Empty;
            TeamName = string.Empty;
            ClassName = string.Empty;
            Manufacturer = string.Empty;
            IRating = 0;
            License = string.Empty;
        }

        internal void SetTelemetry(
            int carIndex,
            bool isPlayer,
            bool isSameClass,
            int overallPosition,
            int classPosition,
            int lap,
            int lapCompleted,
            int lapDifferenceToLeader,
            float gapToLeaderSeconds,
            bool hasGapToLeader,
            float lastLapTime,
            float bestLapTime,
            int trackSurface,
            string trackStatus,
            bool isInPits)
        {
            HasData = true;
            CarIndex = carIndex;
            IsPlayer = isPlayer;
            IsSameClass = isSameClass;
            OverallPosition = overallPosition;
            ClassPosition = classPosition;
            Lap = lap;
            LapCompleted = lapCompleted;
            LapDifferenceToLeader = lapDifferenceToLeader;
            GapToLeaderSeconds = gapToLeaderSeconds;
            HasGapToLeader = hasGapToLeader;
            LastLapTime = lastLapTime;
            BestLapTime = bestLapTime;
            TrackSurface = trackSurface;
            TrackStatus = trackStatus ?? string.Empty;
            IsInPits = isInPits;
        }

        internal void SetIdentity(
            string driverName,
            string carNumber,
            string teamName,
            string className,
            string manufacturer,
            int iRating,
            string license)
        {
            DriverName = driverName ?? string.Empty;
            CarNumber = carNumber ?? string.Empty;
            TeamName = teamName ?? string.Empty;
            ClassName = className ?? string.Empty;
            Manufacturer = manufacturer ?? string.Empty;
            IRating = iRating > 0 ? iRating : 0;
            License = license ?? string.Empty;
        }
    }
}
