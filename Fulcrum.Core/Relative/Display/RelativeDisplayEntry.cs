namespace Fulcrum.Core.Relative.Display
{
    public sealed class RelativeDisplayEntry
    {
        public bool HasData { get; private set; }
        public bool IsPlayer { get; private set; }
        public int CarIndex { get; private set; }
        public int OverallPosition { get; private set; }
        public int ClassPosition { get; private set; }
        public int ClassSize { get; private set; }
        public int PositionGainLoss { get; private set; }
        public bool PositionGainLossAvailable { get; private set; }
        public int Lap { get; private set; }
        public float LapDistancePercent { get; private set; }
        public int LapDifference { get; private set; }
        public float RelativeDistanceLaps { get; private set; }
        public float GapSeconds { get; private set; }
        public float GapLiveSeconds { get; private set; }
        public bool HasGap { get; private set; }
        public bool HasLiveGap { get; private set; }
        public float LastLapTimeSeconds { get; private set; }
        public int StintLap { get; private set; }
        public bool IsOutLap { get; private set; }
        public bool IsTowing { get; private set; }
        public int TireCompound { get; private set; }
        public bool OvertakeSupported { get; private set; }
        public bool OvertakeActive { get; private set; }
        public int OvertakeRemaining { get; private set; }
        public long SessionFlags { get; private set; }
        public bool HasBlackFlag { get; private set; }
        public bool HasMeatballFlag { get; private set; }
        public bool IsDisqualified { get; private set; }

        public int TrackSurface { get; private set; }
        public string TrackStatus { get; private set; }
        public bool IsOnTrack { get; private set; }
        public bool IsOffTrack { get; private set; }
        public bool IsInPitStall { get; private set; }
        public bool IsApproachingPits { get; private set; }
        public bool IsInPits { get; private set; }
        public bool IsOnPitRoad { get; private set; }

        public string DriverName { get; private set; }
        public string CarNumber { get; private set; }
        public string TeamName { get; private set; }
        public string ClassName { get; private set; }
        public string Manufacturer { get; private set; }
        public int IRating { get; private set; }
        public string License { get; private set; }
        public string ClubName { get; private set; }
        public string FlagText { get; private set; }
        public int UserId { get; private set; }
        public int CarId { get; private set; }
        public int ClassId { get; private set; }
        public float CarClassEstimatedLapTime { get; private set; }
        public string CarPath { get; private set; }
        public string CarScreenName { get; private set; }
        public string CarName { get; private set; }
        public string DriverInfoRaw { get; private set; }
        public float DiagnosticPlayerLapDistPct { get; private set; }
        public float DiagnosticOtherLapDistPct { get; private set; }
        public int DiagnosticPlayerLapCompleted { get; private set; }
        public int DiagnosticOtherLapCompleted { get; private set; }
        public float DiagnosticPlayerEstTime { get; private set; }
        public float DiagnosticOtherEstTime { get; private set; }
        public float DiagnosticPlayerF2Time { get; private set; }
        public float DiagnosticOtherF2Time { get; private set; }
        public float DiagnosticDirectEstDifference { get; private set; }
        public float DiagnosticCandidateMinusLap { get; private set; }
        public float DiagnosticCandidatePlusLap { get; private set; }
        public float DiagnosticLapDuration { get; private set; }
        public float DiagnosticPlayerMapTime { get; private set; }
        public float DiagnosticOtherMapTime { get; private set; }
        public string DiagnosticGapMethod { get; private set; }

        public string ManufacturerAlias { get; private set; }
        public string LogoResourceKey { get; private set; }
        public string CountryAlias { get; private set; }
        public string FlagResourceKey { get; private set; }

        public RelativeDisplayEntry() { Reset(); }

        public void Reset()
        {
            HasData = false; IsPlayer = false; CarIndex = -1;
            OverallPosition = 0; ClassPosition = 0; Lap = 0; LapDistancePercent = 0.0f;
            LapDifference = 0; RelativeDistanceLaps = 0.0f;
            ClassSize = 0; PositionGainLoss = 0; PositionGainLossAvailable = false;
            GapSeconds = 0.0f; GapLiveSeconds = 0.0f; HasGap = false; HasLiveGap = false;
            LastLapTimeSeconds = 0.0f; StintLap = 0; IsOutLap = false; IsTowing = false; TireCompound = -1;
            OvertakeSupported = false; OvertakeActive = false; OvertakeRemaining = 0;
            SessionFlags = 0L; HasBlackFlag = false; HasMeatballFlag = false; IsDisqualified = false;
            TrackSurface = RelativeTrackStatus.NotInWorld; TrackStatus = RelativeTrackStatus.GetName(TrackSurface);
            IsOnTrack = false; IsOffTrack = false; IsInPitStall = false; IsApproachingPits = false;
            IsInPits = false; IsOnPitRoad = false;
            DriverName = string.Empty; CarNumber = string.Empty; TeamName = string.Empty;
            ClassName = string.Empty; Manufacturer = string.Empty; IRating = 0; License = string.Empty;
            ClubName = string.Empty; FlagText = string.Empty;
            UserId = 0; CarId = 0; ClassId = -1; CarClassEstimatedLapTime = 0.0f; CarPath = string.Empty; CarScreenName = string.Empty; CarName = string.Empty; DriverInfoRaw = string.Empty;
            DiagnosticPlayerLapDistPct = 0.0f; DiagnosticOtherLapDistPct = 0.0f;
            DiagnosticPlayerLapCompleted = 0; DiagnosticOtherLapCompleted = 0;
            DiagnosticPlayerEstTime = 0.0f; DiagnosticOtherEstTime = 0.0f;
            DiagnosticPlayerF2Time = 0.0f; DiagnosticOtherF2Time = 0.0f;
            DiagnosticDirectEstDifference = 0.0f; DiagnosticCandidateMinusLap = 0.0f;
            DiagnosticCandidatePlusLap = 0.0f; DiagnosticLapDuration = 0.0f;
            DiagnosticPlayerMapTime = 0.0f; DiagnosticOtherMapTime = 0.0f;
            DiagnosticGapMethod = string.Empty;
            ManufacturerAlias = string.Empty; LogoResourceKey = string.Empty;
            CountryAlias = string.Empty; FlagResourceKey = string.Empty;
        }


        public void SetDiagnosticData(
            int userId,
            int carId,
            float carClassEstimatedLapTime,
            string carPath,
            string carScreenName,
            string carName,
            string driverInfoRaw)
        {
            UserId = userId > 0 ? userId : 0;
            CarId = carId > 0 ? carId : 0;
            CarClassEstimatedLapTime =
                carClassEstimatedLapTime > 0.0f
                    ? carClassEstimatedLapTime
                    : 0.0f;
            CarPath = carPath ?? string.Empty;
            CarScreenName = carScreenName ?? string.Empty;
            CarName = carName ?? string.Empty;
            DriverInfoRaw = driverInfoRaw ?? string.Empty;
        }

        public void SetResourceData(
            string manufacturerAlias,
            string logoResourceKey,
            string countryAlias,
            string flagResourceKey)
        {
            ManufacturerAlias = manufacturerAlias ?? string.Empty;
            LogoResourceKey = logoResourceKey ?? string.Empty;
            CountryAlias = countryAlias ?? string.Empty;
            FlagResourceKey = flagResourceKey ?? string.Empty;
        }

        public void SetPlayer(ParticipantSnapshot participant, StintTracker stintTracker)
        {
            Reset();
            if (participant == null || !participant.IsValid) return;
            HasData = true; IsPlayer = true; CarIndex = participant.CarIndex;
            OverallPosition = participant.OverallPosition; ClassPosition = participant.ClassPosition;
            ClassSize = participant.ClassSize; PositionGainLoss = participant.PositionGainLoss;
            PositionGainLossAvailable = participant.PositionGainLossAvailable;
            ClassId = participant.ClassId;
            Lap = participant.Lap; LapDistancePercent = participant.LapDistancePercent;
            HasGap = true; HasLiveGap = true;
            LastLapTimeSeconds = participant.LastLapTime;
            TireCompound = participant.TireCompound;
            SetOvertakeState(participant);
            SetDriverFlags(participant);
            SetTrackState(participant, stintTracker);
        }

        public void SetRelative(RelativeEntry relativeEntry, ParticipantSnapshot participant, StintTracker stintTracker)
        {
            Reset();
            if (relativeEntry == null || !relativeEntry.IsValid) return;
            HasData = true; CarIndex = relativeEntry.CarIndex; OverallPosition = relativeEntry.OverallPosition;
            LapDifference = relativeEntry.LapDifference; RelativeDistanceLaps = relativeEntry.RelativeDistanceLaps;
            if (relativeEntry.HasValidFilteredGap) { GapSeconds = relativeEntry.FilteredGapSeconds; HasGap = true; }
            if (relativeEntry.HasValidRawGap) { GapLiveSeconds = relativeEntry.RawGapSeconds; HasLiveGap = true; }
            DiagnosticPlayerLapDistPct = relativeEntry.DiagnosticPlayerLapDistPct;
            DiagnosticOtherLapDistPct = relativeEntry.DiagnosticOtherLapDistPct;
            DiagnosticPlayerLapCompleted = relativeEntry.DiagnosticPlayerLapCompleted;
            DiagnosticOtherLapCompleted = relativeEntry.DiagnosticOtherLapCompleted;
            DiagnosticPlayerEstTime = relativeEntry.DiagnosticPlayerEstTime;
            DiagnosticOtherEstTime = relativeEntry.DiagnosticOtherEstTime;
            DiagnosticPlayerF2Time = relativeEntry.DiagnosticPlayerF2Time;
            DiagnosticOtherF2Time = relativeEntry.DiagnosticOtherF2Time;
            DiagnosticDirectEstDifference = relativeEntry.DiagnosticDirectEstDifference;
            DiagnosticCandidateMinusLap = relativeEntry.DiagnosticCandidateMinusLap;
            DiagnosticCandidatePlusLap = relativeEntry.DiagnosticCandidatePlusLap;
            DiagnosticLapDuration = relativeEntry.DiagnosticLapDuration;
            DiagnosticPlayerMapTime = relativeEntry.DiagnosticPlayerMapTime;
            DiagnosticOtherMapTime = relativeEntry.DiagnosticOtherMapTime;
            DiagnosticGapMethod = relativeEntry.DiagnosticGapMethod ?? string.Empty;
            if (participant == null) return;
            ClassPosition = participant.ClassPosition;
            ClassSize = participant.ClassSize; PositionGainLoss = participant.PositionGainLoss;
            PositionGainLossAvailable = participant.PositionGainLossAvailable; ClassId = participant.ClassId; Lap = participant.Lap;
            LapDistancePercent = participant.LapDistancePercent; LastLapTimeSeconds = participant.LastLapTime; TireCompound = participant.TireCompound;
            SetOvertakeState(participant);
            SetDriverFlags(participant);
            SetTrackState(participant, stintTracker);
        }

        public void SetIdentity(string driverName, string carNumber, string teamName, string className, string manufacturer, int iRating, string license, string clubName, string flagText)
        {
            DriverName = driverName ?? string.Empty; CarNumber = carNumber ?? string.Empty;
            TeamName = teamName ?? string.Empty; ClassName = className ?? string.Empty;
            Manufacturer = manufacturer ?? string.Empty; IRating = iRating > 0 ? iRating : 0;
            License = license ?? string.Empty;
            ClubName = clubName ?? string.Empty; FlagText = flagText ?? string.Empty;
        }


        private void SetOvertakeState(ParticipantSnapshot participant)
        {
            OvertakeSupported = participant != null && participant.OvertakeSupported;
            OvertakeActive = participant != null && participant.OvertakeActive;
            OvertakeRemaining = participant != null && participant.OvertakeRemaining > 0
                ? participant.OvertakeRemaining
                : 0;
        }

        private void SetDriverFlags(ParticipantSnapshot participant)
        {
            SessionFlags = participant != null ? participant.SessionFlags : 0L;
            HasBlackFlag = Telemetry.SessionStateInterpreter.HasBlack(SessionFlags);
            HasMeatballFlag = Telemetry.SessionStateInterpreter.HasRepair(SessionFlags);
            IsDisqualified = Telemetry.SessionStateInterpreter.HasDisqualify(SessionFlags);
        }

        private void SetTrackState(ParticipantSnapshot participant, StintTracker stintTracker)
        {
            TrackSurface = participant.TrackSurface; TrackStatus = RelativeTrackStatus.GetName(TrackSurface);
            IsOnTrack = TrackSurface == RelativeTrackStatus.OnTrack;
            IsOffTrack = TrackSurface == RelativeTrackStatus.OffTrack;
            IsInPitStall = TrackSurface == RelativeTrackStatus.InPitStall;
            IsApproachingPits = TrackSurface == RelativeTrackStatus.ApproachingPits;
            IsOnPitRoad = participant.IsOnPitRoad;
            IsInPits = IsOnPitRoad || RelativeTrackStatus.IsInPits(TrackSurface);
            if (stintTracker != null)
            {
                StintLap = stintTracker.GetStintLap(CarIndex);
                IsOutLap = stintTracker.IsOutLap(CarIndex);
                IsTowing = stintTracker.IsTowing(CarIndex);
            }
        }
    }
}
