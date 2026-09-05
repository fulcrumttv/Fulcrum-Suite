using System;

namespace Fulcrum.Core.Telemetry
{
    public class TelemetrySnapshot
    {
        public DateTime CapturedAt
        {
            get;
            set;
        }

        public bool GameRunning
        {
            get;
            set;
        }

        public string GameName
        {
            get;
            set;
        }

        public string SessionType
        {
            get;
            set;
        }

        public double SessionTime
        {
            get;
            set;
        }

        public double SessionTimeRemaining
        {
            get;
            set;
        }

        public int SessionNumber
        {
            get;
            set;
        }

        public int SessionState
        {
            get;
            set;
        }

        public long SessionFlags
        {
            get;
            set;
        }

        public int SessionLapsRemaining
        {
            get;
            set;
        }

        public int SessionLapsTotal
        {
            get;
            set;
        }

        public int PlayerCarIndex
        {
            get;
            set;
        }

        public int PlayerPosition
        {
            get;
            set;
        }

        public int PlayerClassPosition
        {
            get;
            set;
        }

        public int PlayerClassId
        {
            get;
            set;
        }

        public int Lap
        {
            get;
            set;
        }

        public int LapCompleted
        {
            get;
            set;
        }

        public float LapDistancePercent
        {
            get;
            set;
        }

        public float SpeedMetersPerSecond
        {
            get;
            set;
        }

        public float Throttle
        {
            get;
            set;
        }

        public float Brake
        {
            get;
            set;
        }

        public float Clutch
        {
            get;
            set;
        }

        public int Gear
        {
            get;
            set;
        }

        public float Rpm
        {
            get;
            set;
        }

        public bool IsOnTrack
        {
            get;
            set;
        }

        public bool IsOnPitRoad
        {
            get;
            set;
        }

        public bool IsReplayPlaying
        {
            get;
            set;
        }

        public float TrackTemperatureCelsius
        {
            get;
            set;
        }

        public float AirTemperatureCelsius
        {
            get;
            set;
        }

        public double FuelLevelLiters
        {
            get;
            set;
        }

        public double FuelLevelPercent
        {
            get;
            set;
        }

        public TelemetrySnapshot()
        {
            CapturedAt = DateTime.MinValue;
            GameName = string.Empty;
            SessionType = string.Empty;
            PlayerCarIndex = -1;
            PlayerPosition = -1;
            PlayerClassPosition = -1;
            PlayerClassId = -1;
            Gear = 0;
        }
    }
}