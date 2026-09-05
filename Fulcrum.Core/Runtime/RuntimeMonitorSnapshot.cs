using System;

namespace Fulcrum.Core.Runtime
{
    public sealed class RuntimeMonitorSnapshot
    {
        public bool Ready { get; set; }
        public bool GameRunning { get; set; }
        public bool HasReceivedTelemetry { get; set; }
        public bool HasRecentTelemetry { get; set; }
        public bool IsTelemetryStale { get; set; }
        public bool IsReplay { get; set; }
        public bool IsOnTrack { get; set; }
        public bool IsOnPitRoad { get; set; }

        public string DataQuality { get; set; }
        public string Mode { get; set; }
        public string Status { get; set; }
        public string Summary { get; set; }
        public string StaleReason { get; set; }

        public double TelemetryAgeMilliseconds { get; set; }
        public double FrameRateHz { get; set; }
        public double UptimeSeconds { get; set; }
        public double PlayerSpeedKph { get; set; }

        public long FrameSequence { get; set; }
        public long SessionSequence { get; set; }
        public int ConsecutiveMissingUpdates { get; set; }
        public int SessionNumber { get; set; }
        public string SessionType { get; set; }
        public DateTime UpdatedAtUtc { get; set; }

        public RuntimeMonitorSnapshot()
        {
            Reset();
        }

        public void Reset()
        {
            Ready = false;
            GameRunning = false;
            HasReceivedTelemetry = false;
            HasRecentTelemetry = false;
            IsTelemetryStale = false;
            IsReplay = false;
            IsOnTrack = false;
            IsOnPitRoad = false;

            DataQuality = "Offline";
            Mode = "Offline";
            Status = "Waiting for game";
            Summary = "No active telemetry";
            StaleReason = string.Empty;

            TelemetryAgeMilliseconds = 0.0;
            FrameRateHz = 0.0;
            UptimeSeconds = 0.0;
            PlayerSpeedKph = 0.0;

            FrameSequence = 0L;
            SessionSequence = 0L;
            ConsecutiveMissingUpdates = 0;
            SessionNumber = -1;
            SessionType = string.Empty;
            UpdatedAtUtc = DateTime.MinValue;
        }
    }
}
