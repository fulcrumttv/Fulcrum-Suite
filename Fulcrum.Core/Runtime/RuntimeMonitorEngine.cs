using System;
using Fulcrum.Core.Telemetry;

namespace Fulcrum.Core.Runtime
{
    public sealed class RuntimeMonitorEngine
    {
        private const double StaleThresholdMilliseconds = 750.0;
        private const double LostThresholdMilliseconds = 2500.0;

        private DateTime gameStartedAtUtc;
        private DateTime lastFrameAtUtc;
        private DateTime rateWindowStartedAtUtc;
        private long frameSequence;
        private long sessionSequence;
        private long framesInRateWindow;
        private double frameRateHz;
        private int consecutiveMissingUpdates;
        private int lastSessionNumber;
        private string lastSessionType;
        private bool wasGameRunning;

        public RuntimeMonitorEngine()
        {
            Reset();
        }

        public void NotifyFrame(bool gameRunning, bool hasNewFrame, TelemetrySnapshot telemetry)
        {
            DateTime now = DateTime.UtcNow;

            if (!gameRunning)
            {
                wasGameRunning = false;
                consecutiveMissingUpdates = 0;
                return;
            }

            if (!wasGameRunning)
            {
                gameStartedAtUtc = now;
                rateWindowStartedAtUtc = now;
                framesInRateWindow = 0;
                frameRateHz = 0.0;
                lastSessionNumber = -1;
                lastSessionType = string.Empty;
                wasGameRunning = true;
            }

            if (!hasNewFrame || telemetry == null)
            {
                consecutiveMissingUpdates++;
                UpdateFrameRate(now);
                return;
            }

            consecutiveMissingUpdates = 0;
            lastFrameAtUtc = now;
            frameSequence++;
            framesInRateWindow++;

            string sessionType = telemetry.SessionType ?? string.Empty;
            if (lastSessionNumber >= 0 &&
                (telemetry.SessionNumber != lastSessionNumber ||
                 !string.Equals(sessionType, lastSessionType, StringComparison.Ordinal)))
            {
                sessionSequence++;
            }

            lastSessionNumber = telemetry.SessionNumber;
            lastSessionType = sessionType;
            UpdateFrameRate(now);
        }

        public void Update(bool gameRunning, TelemetrySnapshot telemetry, RuntimeMonitorSnapshot output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            DateTime now = DateTime.UtcNow;
            output.GameRunning = gameRunning;
            output.FrameSequence = frameSequence;
            output.SessionSequence = sessionSequence;
            output.FrameRateHz = frameRateHz;
            output.ConsecutiveMissingUpdates = consecutiveMissingUpdates;
            output.UpdatedAtUtc = now;

            if (!gameRunning)
            {
                SetOffline(output);
                return;
            }

            output.HasReceivedTelemetry = frameSequence > 0 && lastFrameAtUtc != DateTime.MinValue;
            output.UptimeSeconds = gameStartedAtUtc == DateTime.MinValue
                ? 0.0
                : Math.Max(0.0, (now - gameStartedAtUtc).TotalSeconds);

            if (!output.HasReceivedTelemetry)
            {
                output.Ready = false;
                output.HasRecentTelemetry = false;
                output.IsTelemetryStale = false;
                output.DataQuality = "Waiting";
                output.Mode = "Connected";
                output.Status = "Waiting for telemetry";
                output.Summary = "Game connected; no telemetry frame received";
                output.StaleReason = string.Empty;
                output.TelemetryAgeMilliseconds = 0.0;
                ApplyTelemetryState(telemetry, output);
                return;
            }

            double ageMs = Math.Max(0.0, (now - lastFrameAtUtc).TotalMilliseconds);
            output.TelemetryAgeMilliseconds = ageMs;
            output.HasRecentTelemetry = ageMs <= StaleThresholdMilliseconds;
            output.IsTelemetryStale = ageMs > StaleThresholdMilliseconds;

            ApplyTelemetryState(telemetry, output);

            if (ageMs > LostThresholdMilliseconds)
            {
                output.Ready = false;
                output.DataQuality = "Lost";
                output.Status = "Telemetry lost";
                output.StaleReason = "No telemetry frame for more than 2.5 seconds";
            }
            else if (output.IsTelemetryStale)
            {
                output.Ready = false;
                output.DataQuality = "Stale";
                output.Status = "Telemetry stale";
                output.StaleReason = "Telemetry age exceeded 750 ms";
            }
            else if (output.IsReplay)
            {
                output.Ready = true;
                output.DataQuality = "Replay";
                output.Status = "Replay telemetry";
                output.StaleReason = string.Empty;
            }
            else if (frameRateHz > 0.0 && frameRateHz < 20.0)
            {
                output.Ready = true;
                output.DataQuality = "Degraded";
                output.Status = "Low telemetry rate";
                output.StaleReason = string.Empty;
            }
            else
            {
                output.Ready = true;
                output.DataQuality = "Live";
                output.Status = "Telemetry active";
                output.StaleReason = string.Empty;
            }

            output.Summary = BuildSummary(output);
        }

        public void Reset()
        {
            gameStartedAtUtc = DateTime.MinValue;
            lastFrameAtUtc = DateTime.MinValue;
            rateWindowStartedAtUtc = DateTime.MinValue;
            frameSequence = 0L;
            sessionSequence = 0L;
            framesInRateWindow = 0L;
            frameRateHz = 0.0;
            consecutiveMissingUpdates = 0;
            lastSessionNumber = -1;
            lastSessionType = string.Empty;
            wasGameRunning = false;
        }

        private void UpdateFrameRate(DateTime now)
        {
            if (rateWindowStartedAtUtc == DateTime.MinValue)
            {
                rateWindowStartedAtUtc = now;
                return;
            }

            double elapsed = (now - rateWindowStartedAtUtc).TotalSeconds;
            if (elapsed < 1.0)
            {
                return;
            }

            frameRateHz = framesInRateWindow / elapsed;
            framesInRateWindow = 0L;
            rateWindowStartedAtUtc = now;
        }

        private static void ApplyTelemetryState(TelemetrySnapshot telemetry, RuntimeMonitorSnapshot output)
        {
            if (telemetry == null)
            {
                output.IsReplay = false;
                output.IsOnTrack = false;
                output.IsOnPitRoad = false;
                output.PlayerSpeedKph = 0.0;
                output.SessionNumber = -1;
                output.SessionType = string.Empty;
                output.Mode = "Connected";
                return;
            }

            output.IsReplay = telemetry.IsReplayPlaying;
            output.IsOnTrack = telemetry.IsOnTrack;
            output.IsOnPitRoad = telemetry.IsOnPitRoad;
            output.PlayerSpeedKph = Math.Max(0.0, telemetry.SpeedMetersPerSecond * 3.6);
            output.SessionNumber = telemetry.SessionNumber;
            output.SessionType = telemetry.SessionType ?? string.Empty;

            if (telemetry.IsReplayPlaying)
            {
                output.Mode = "Replay";
            }
            else if (telemetry.IsOnPitRoad)
            {
                output.Mode = "PitRoad";
            }
            else if (telemetry.IsOnTrack)
            {
                output.Mode = "OnTrack";
            }
            else
            {
                output.Mode = "Garage";
            }
        }

        private static string BuildSummary(RuntimeMonitorSnapshot value)
        {
            if (!value.GameRunning) return "No active game";
            if (!value.HasReceivedTelemetry) return "Waiting for first telemetry frame";
            if (value.IsTelemetryStale) return value.Status + " · " + Math.Round(value.TelemetryAgeMilliseconds) + " ms";
            return value.DataQuality + " · " + value.Mode + " · " + value.FrameRateHz.ToString("0.0") + " Hz";
        }

        private static void SetOffline(RuntimeMonitorSnapshot output)
        {
            output.Ready = false;
            output.GameRunning = false;
            output.HasReceivedTelemetry = false;
            output.HasRecentTelemetry = false;
            output.IsTelemetryStale = false;
            output.IsReplay = false;
            output.IsOnTrack = false;
            output.IsOnPitRoad = false;
            output.DataQuality = "Offline";
            output.Mode = "Offline";
            output.Status = "Waiting for game";
            output.Summary = "No active telemetry";
            output.StaleReason = string.Empty;
            output.TelemetryAgeMilliseconds = 0.0;
            output.FrameRateHz = 0.0;
            output.UptimeSeconds = 0.0;
            output.PlayerSpeedKph = 0.0;
            output.SessionNumber = -1;
            output.SessionType = string.Empty;
        }
    }
}
