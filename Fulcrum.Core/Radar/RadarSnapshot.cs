using System;

namespace Fulcrum.Core.Radar
{
    /// <summary>
    /// Dashboard-friendly radar snapshot.  The v4.1.47 native-distance fields
    /// reproduce the four longitudinal inputs used by Fulcrum Radar v0.6.21
    /// without depending on iRacingExtraProperties or any third-party plugin.
    /// </summary>
    public sealed class RadarSnapshot
    {
        public DateTime CapturedAt { get; set; }
        public int RawState { get; set; }
        public int StableState { get; set; }
        public bool IsHeld { get; set; }
        public double HoldRemainingMilliseconds { get; set; }
        public string State { get; set; }
        public bool IsActive { get; set; }
        public bool ShouldShow { get; set; }
        public bool ContextValid { get; set; }
        public bool IsOnTrack { get; set; }
        public bool IsReplayPlaying { get; set; }
        public bool IsOnPitRoad { get; set; }
        public bool HasCarLeft { get; set; }
        public bool HasCarRight { get; set; }
        public bool HasCarsBothSides { get; set; }
        public bool HasTwoCarsLeft { get; set; }
        public bool HasTwoCarsRight { get; set; }
        public int LeftCarCount { get; set; }
        public int RightCarCount { get; set; }
        public int TotalCarCount { get; set; }
        public int Severity { get; set; }
        public string Callout { get; set; }

        // Legacy/native diagnostic aliases retained from v4.1.47.
        public bool AheadDistanceAvailable { get; set; }
        public bool BehindDistanceAvailable { get; set; }
        public bool HasLongitudinalData { get; set; }
        public float AheadDistanceMeters { get; set; }
        public float BehindDistanceMeters { get; set; }

        // v0.6.21-compatible native proximity feed.
        public bool NativeDistanceReady { get; set; }
        public float TrackLengthMeters { get; set; }
        public int PlayerCarIndex { get; set; }
        public float PlayerLapDistancePercent { get; set; }

        public int Ahead00CarIndex { get; set; }
        public int Ahead01CarIndex { get; set; }
        public int Behind00CarIndex { get; set; }
        public int Behind01CarIndex { get; set; }

        public float Ahead00DistanceMeters { get; set; }
        public float Ahead01DistanceMeters { get; set; }
        public float Behind00DistanceMeters { get; set; }
        public float Behind01DistanceMeters { get; set; }

        public bool Ahead00IsInPit { get; set; }
        public bool Ahead01IsInPit { get; set; }
        public bool Behind00IsInPit { get; set; }
        public bool Behind01IsInPit { get; set; }

        public bool HasNearbyLongitudinalContact { get; set; }
        public string InputSource { get; set; }

        public RadarSnapshot()
        {
            Reset();
        }

        public void Reset()
        {
            CapturedAt = DateTime.MinValue;
            RawState = 0;
            StableState = 0;
            IsHeld = false;
            HoldRemainingMilliseconds = 0.0;
            State = "Off";
            IsActive = false;
            ShouldShow = false;
            ContextValid = false;
            IsOnTrack = false;
            IsReplayPlaying = false;
            IsOnPitRoad = false;
            HasCarLeft = false;
            HasCarRight = false;
            HasCarsBothSides = false;
            HasTwoCarsLeft = false;
            HasTwoCarsRight = false;
            LeftCarCount = 0;
            RightCarCount = 0;
            TotalCarCount = 0;
            Severity = 0;
            Callout = string.Empty;

            AheadDistanceAvailable = false;
            BehindDistanceAvailable = false;
            HasLongitudinalData = false;
            AheadDistanceMeters = 0.0f;
            BehindDistanceMeters = 0.0f;

            NativeDistanceReady = false;
            TrackLengthMeters = 0.0f;
            PlayerCarIndex = -1;
            PlayerLapDistancePercent = -1.0f;

            Ahead00CarIndex = -1;
            Ahead01CarIndex = -1;
            Behind00CarIndex = -1;
            Behind01CarIndex = -1;
            Ahead00DistanceMeters = 0.0f;
            Ahead01DistanceMeters = 0.0f;
            Behind00DistanceMeters = 0.0f;
            Behind01DistanceMeters = 0.0f;
            Ahead00IsInPit = false;
            Ahead01IsInPit = false;
            Behind00IsInPit = false;
            Behind01IsInPit = false;
            HasNearbyLongitudinalContact = false;
            InputSource = "None";
        }
    }
}
