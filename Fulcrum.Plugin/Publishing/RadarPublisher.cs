using System;
using Fulcrum.Core.Radar;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Publishing
{
    public sealed class RadarPublisher
    {
        private readonly PluginManager pluginManager;
        private readonly Type pluginType;

        public RadarPublisher(PluginManager pluginManager, Type pluginType)
        {
            this.pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
            this.pluginType = pluginType ?? throw new ArgumentNullException(nameof(pluginType));
            RegisterProperties();
        }

        public void Publish(RadarReader reader, RadarSnapshot snapshot)
        {
            pluginManager.SetPropertyValue("Fulcrum.Radar.Ready", pluginType, reader != null && reader.HasTelemetry);
            pluginManager.SetPropertyValue("Fulcrum.Radar.Error", pluginType, reader == null ? string.Empty : reader.Error);
            pluginManager.SetPropertyValue("Fulcrum.Radar.RawState", pluginType, snapshot.RawState);
            pluginManager.SetPropertyValue("Fulcrum.Radar.StableState", pluginType, snapshot.StableState);
            pluginManager.SetPropertyValue("Fulcrum.Radar.IsHeld", pluginType, snapshot.IsHeld);
            pluginManager.SetPropertyValue("Fulcrum.Radar.HoldRemainingMilliseconds", pluginType, snapshot.HoldRemainingMilliseconds);
            pluginManager.SetPropertyValue("Fulcrum.Radar.State", pluginType, snapshot.State);
            pluginManager.SetPropertyValue("Fulcrum.Radar.IsActive", pluginType, snapshot.IsActive);
            pluginManager.SetPropertyValue("Fulcrum.Radar.ShouldShow", pluginType, snapshot.ShouldShow);
            pluginManager.SetPropertyValue("Fulcrum.Radar.ContextValid", pluginType, snapshot.ContextValid);
            pluginManager.SetPropertyValue("Fulcrum.Radar.IsOnTrack", pluginType, snapshot.IsOnTrack);
            pluginManager.SetPropertyValue("Fulcrum.Radar.IsReplayPlaying", pluginType, snapshot.IsReplayPlaying);
            pluginManager.SetPropertyValue("Fulcrum.Radar.IsOnPitRoad", pluginType, snapshot.IsOnPitRoad);
            pluginManager.SetPropertyValue("Fulcrum.Radar.HasCarLeft", pluginType, snapshot.HasCarLeft);
            pluginManager.SetPropertyValue("Fulcrum.Radar.HasCarRight", pluginType, snapshot.HasCarRight);
            pluginManager.SetPropertyValue("Fulcrum.Radar.HasCarsBothSides", pluginType, snapshot.HasCarsBothSides);
            pluginManager.SetPropertyValue("Fulcrum.Radar.HasTwoCarsLeft", pluginType, snapshot.HasTwoCarsLeft);
            pluginManager.SetPropertyValue("Fulcrum.Radar.HasTwoCarsRight", pluginType, snapshot.HasTwoCarsRight);
            pluginManager.SetPropertyValue("Fulcrum.Radar.LeftCarCount", pluginType, snapshot.LeftCarCount);
            pluginManager.SetPropertyValue("Fulcrum.Radar.RightCarCount", pluginType, snapshot.RightCarCount);
            pluginManager.SetPropertyValue("Fulcrum.Radar.TotalCarCount", pluginType, snapshot.TotalCarCount);
            pluginManager.SetPropertyValue("Fulcrum.Radar.Severity", pluginType, snapshot.Severity);
            pluginManager.SetPropertyValue("Fulcrum.Radar.Callout", pluginType, snapshot.Callout);
            pluginManager.SetPropertyValue("Fulcrum.Radar.AheadDistanceAvailable", pluginType, snapshot.AheadDistanceAvailable);
            pluginManager.SetPropertyValue("Fulcrum.Radar.BehindDistanceAvailable", pluginType, snapshot.BehindDistanceAvailable);
            pluginManager.SetPropertyValue("Fulcrum.Radar.HasLongitudinalData", pluginType, snapshot.HasLongitudinalData);
            pluginManager.SetPropertyValue("Fulcrum.Radar.AheadDistanceMeters", pluginType, snapshot.AheadDistanceMeters);
            pluginManager.SetPropertyValue("Fulcrum.Radar.BehindDistanceMeters", pluginType, snapshot.BehindDistanceMeters);
            pluginManager.SetPropertyValue("Fulcrum.Radar.InputSource", pluginType, snapshot.InputSource);
            pluginManager.SetPropertyValue("Fulcrum.Radar.NativeDistanceReady", pluginType, snapshot.NativeDistanceReady);
            pluginManager.SetPropertyValue("Fulcrum.Radar.TrackLengthMeters", pluginType, snapshot.TrackLengthMeters);
            pluginManager.SetPropertyValue("Fulcrum.Radar.PlayerCarIndex", pluginType, snapshot.PlayerCarIndex);
            pluginManager.SetPropertyValue("Fulcrum.Radar.PlayerLapDistancePercent", pluginType, snapshot.PlayerLapDistancePercent);
            pluginManager.SetPropertyValue("Fulcrum.Radar.Ahead00CarIndex", pluginType, snapshot.Ahead00CarIndex);
            pluginManager.SetPropertyValue("Fulcrum.Radar.Ahead01CarIndex", pluginType, snapshot.Ahead01CarIndex);
            pluginManager.SetPropertyValue("Fulcrum.Radar.Behind00CarIndex", pluginType, snapshot.Behind00CarIndex);
            pluginManager.SetPropertyValue("Fulcrum.Radar.Behind01CarIndex", pluginType, snapshot.Behind01CarIndex);
            pluginManager.SetPropertyValue("Fulcrum.Radar.Ahead00DistanceMeters", pluginType, snapshot.Ahead00DistanceMeters);
            pluginManager.SetPropertyValue("Fulcrum.Radar.Ahead01DistanceMeters", pluginType, snapshot.Ahead01DistanceMeters);
            pluginManager.SetPropertyValue("Fulcrum.Radar.Behind00DistanceMeters", pluginType, snapshot.Behind00DistanceMeters);
            pluginManager.SetPropertyValue("Fulcrum.Radar.Behind01DistanceMeters", pluginType, snapshot.Behind01DistanceMeters);
            pluginManager.SetPropertyValue("Fulcrum.Radar.Ahead00IsInPit", pluginType, snapshot.Ahead00IsInPit);
            pluginManager.SetPropertyValue("Fulcrum.Radar.Ahead01IsInPit", pluginType, snapshot.Ahead01IsInPit);
            pluginManager.SetPropertyValue("Fulcrum.Radar.Behind00IsInPit", pluginType, snapshot.Behind00IsInPit);
            pluginManager.SetPropertyValue("Fulcrum.Radar.Behind01IsInPit", pluginType, snapshot.Behind01IsInPit);
            pluginManager.SetPropertyValue("Fulcrum.Radar.HasNearbyLongitudinalContact", pluginType, snapshot.HasNearbyLongitudinalContact);
        }

        private void RegisterProperties()
        {
            pluginManager.AddProperty("Fulcrum.Radar.Ready", pluginType, false);
            pluginManager.AddProperty("Fulcrum.Radar.Error", pluginType, string.Empty);
            pluginManager.AddProperty("Fulcrum.Radar.RawState", pluginType, 0);
            pluginManager.AddProperty("Fulcrum.Radar.StableState", pluginType, 0);
            pluginManager.AddProperty("Fulcrum.Radar.IsHeld", pluginType, false);
            pluginManager.AddProperty("Fulcrum.Radar.HoldRemainingMilliseconds", pluginType, 0.0);
            pluginManager.AddProperty("Fulcrum.Radar.State", pluginType, "Off");
            pluginManager.AddProperty("Fulcrum.Radar.IsActive", pluginType, false);
            pluginManager.AddProperty("Fulcrum.Radar.ShouldShow", pluginType, false);
            pluginManager.AddProperty("Fulcrum.Radar.ContextValid", pluginType, false);
            pluginManager.AddProperty("Fulcrum.Radar.IsOnTrack", pluginType, false);
            pluginManager.AddProperty("Fulcrum.Radar.IsReplayPlaying", pluginType, false);
            pluginManager.AddProperty("Fulcrum.Radar.IsOnPitRoad", pluginType, false);
            pluginManager.AddProperty("Fulcrum.Radar.HasCarLeft", pluginType, false);
            pluginManager.AddProperty("Fulcrum.Radar.HasCarRight", pluginType, false);
            pluginManager.AddProperty("Fulcrum.Radar.HasCarsBothSides", pluginType, false);
            pluginManager.AddProperty("Fulcrum.Radar.HasTwoCarsLeft", pluginType, false);
            pluginManager.AddProperty("Fulcrum.Radar.HasTwoCarsRight", pluginType, false);
            pluginManager.AddProperty("Fulcrum.Radar.LeftCarCount", pluginType, 0);
            pluginManager.AddProperty("Fulcrum.Radar.RightCarCount", pluginType, 0);
            pluginManager.AddProperty("Fulcrum.Radar.TotalCarCount", pluginType, 0);
            pluginManager.AddProperty("Fulcrum.Radar.Severity", pluginType, 0);
            pluginManager.AddProperty("Fulcrum.Radar.Callout", pluginType, string.Empty);
            pluginManager.AddProperty("Fulcrum.Radar.AheadDistanceAvailable", pluginType, false);
            pluginManager.AddProperty("Fulcrum.Radar.BehindDistanceAvailable", pluginType, false);
            pluginManager.AddProperty("Fulcrum.Radar.HasLongitudinalData", pluginType, false);
            pluginManager.AddProperty("Fulcrum.Radar.AheadDistanceMeters", pluginType, 0.0f);
            pluginManager.AddProperty("Fulcrum.Radar.BehindDistanceMeters", pluginType, 0.0f);
            pluginManager.AddProperty("Fulcrum.Radar.InputSource", pluginType, "None");
            pluginManager.AddProperty("Fulcrum.Radar.NativeDistanceReady", pluginType, false);
            pluginManager.AddProperty("Fulcrum.Radar.TrackLengthMeters", pluginType, 0.0f);
            pluginManager.AddProperty("Fulcrum.Radar.PlayerCarIndex", pluginType, -1);
            pluginManager.AddProperty("Fulcrum.Radar.PlayerLapDistancePercent", pluginType, -1.0f);
            pluginManager.AddProperty("Fulcrum.Radar.Ahead00CarIndex", pluginType, -1);
            pluginManager.AddProperty("Fulcrum.Radar.Ahead01CarIndex", pluginType, -1);
            pluginManager.AddProperty("Fulcrum.Radar.Behind00CarIndex", pluginType, -1);
            pluginManager.AddProperty("Fulcrum.Radar.Behind01CarIndex", pluginType, -1);
            pluginManager.AddProperty("Fulcrum.Radar.Ahead00DistanceMeters", pluginType, 0.0f);
            pluginManager.AddProperty("Fulcrum.Radar.Ahead01DistanceMeters", pluginType, 0.0f);
            pluginManager.AddProperty("Fulcrum.Radar.Behind00DistanceMeters", pluginType, 0.0f);
            pluginManager.AddProperty("Fulcrum.Radar.Behind01DistanceMeters", pluginType, 0.0f);
            pluginManager.AddProperty("Fulcrum.Radar.Ahead00IsInPit", pluginType, false);
            pluginManager.AddProperty("Fulcrum.Radar.Ahead01IsInPit", pluginType, false);
            pluginManager.AddProperty("Fulcrum.Radar.Behind00IsInPit", pluginType, false);
            pluginManager.AddProperty("Fulcrum.Radar.Behind01IsInPit", pluginType, false);
            pluginManager.AddProperty("Fulcrum.Radar.HasNearbyLongitudinalContact", pluginType, false);
        }
    }
}
