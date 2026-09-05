using System;
using Fulcrum.Core.Runtime;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Publishing
{
    public sealed class RuntimeMonitorPublisher
    {
        private const string Prefix = "Fulcrum.Runtime.";
        private readonly PluginManager pluginManager;
        private readonly Type pluginType;

        public RuntimeMonitorPublisher(PluginManager pluginManager, Type pluginType)
        {
            this.pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
            this.pluginType = pluginType ?? throw new ArgumentNullException(nameof(pluginType));
            RegisterProperties();
        }

        public void Publish(RuntimeMonitorSnapshot value)
        {
            Set("Ready", value.Ready);
            Set("DataQuality", value.DataQuality);
            Set("Mode", value.Mode);
            Set("Status", value.Status);
            Set("Summary", value.Summary);
            Set("StaleReason", value.StaleReason);
            Set("GameRunning", value.GameRunning);
            Set("HasReceivedTelemetry", value.HasReceivedTelemetry);
            Set("HasRecentTelemetry", value.HasRecentTelemetry);
            Set("IsTelemetryStale", value.IsTelemetryStale);
            Set("IsReplay", value.IsReplay);
            Set("IsOnTrack", value.IsOnTrack);
            Set("IsOnPitRoad", value.IsOnPitRoad);
            Set("TelemetryAgeMs", value.TelemetryAgeMilliseconds);
            Set("FrameRateHz", value.FrameRateHz);
            Set("UptimeSeconds", value.UptimeSeconds);
            Set("PlayerSpeedKph", value.PlayerSpeedKph);
            Set("FrameSequence", value.FrameSequence);
            Set("SessionSequence", value.SessionSequence);
            Set("ConsecutiveMissingUpdates", value.ConsecutiveMissingUpdates);
            Set("SessionNumber", value.SessionNumber);
            Set("SessionType", value.SessionType);
        }

        private void RegisterProperties()
        {
            Add("Ready", false, "True when fresh telemetry is available");
            Add("DataQuality", "Offline", "Offline, Waiting, Live, Replay, Degraded, Stale or Lost");
            Add("Mode", "Offline", "Offline, Connected, Garage, OnTrack, PitRoad or Replay");
            Add("Status", "Waiting for game", "Current runtime telemetry status");
            Add("Summary", "No active telemetry", "Short runtime summary");
            Add("StaleReason", string.Empty, "Reason telemetry is considered stale");
            Add("GameRunning", false, "True while the game is connected");
            Add("HasReceivedTelemetry", false, "True after at least one telemetry frame");
            Add("HasRecentTelemetry", false, "True when telemetry age is within the live threshold");
            Add("IsTelemetryStale", false, "True when no recent telemetry frame is available");
            Add("IsReplay", false, "True while replay telemetry is active");
            Add("IsOnTrack", false, "True while the player is on track");
            Add("IsOnPitRoad", false, "True while the player is on pit road");
            Add("TelemetryAgeMs", 0.0, "Milliseconds since the latest telemetry frame");
            Add("FrameRateHz", 0.0, "Observed raw telemetry frame rate");
            Add("UptimeSeconds", 0.0, "Seconds since the current game connection started");
            Add("PlayerSpeedKph", 0.0, "Player speed in kilometres per hour");
            Add("FrameSequence", 0L, "Monotonic telemetry frame counter");
            Add("SessionSequence", 0L, "Increases when the active session changes");
            Add("ConsecutiveMissingUpdates", 0, "Consecutive plugin updates without new telemetry");
            Add("SessionNumber", -1, "Current iRacing session number");
            Add("SessionType", string.Empty, "Current session type");
        }

        private void Add(string name, object defaultValue, string description)
        {
            pluginManager.AddProperty(Prefix + name, pluginType, defaultValue, description);
        }

        private void Set(string name, object value)
        {
            pluginManager.SetPropertyValue(Prefix + name, pluginType, value);
        }
    }
}
