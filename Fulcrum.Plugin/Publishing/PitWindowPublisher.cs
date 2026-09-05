using System;
using Fulcrum.Core.PitWindow;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Publishing
{
    public sealed class PitWindowPublisher
    {
        private const string Prefix = "Fulcrum.PitWindow.";
        private readonly PluginManager pluginManager;
        private readonly Type pluginType;

        public PitWindowPublisher(PluginManager pluginManager, Type pluginType)
        {
            this.pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
            this.pluginType = pluginType ?? throw new ArgumentNullException(nameof(pluginType));
            RegisterProperties();
        }

        public void Publish(PitWindowSnapshot value)
        {
            Set("Ready", value.Ready);
            Set("HasWindow", value.HasWindow);
            Set("Status", value.Status);
            Set("Summary", value.Summary);
            Set("Recommendation", value.Recommendation);
            Set("CurrentLap", value.CurrentLap);
            Set("OpenLap", value.OpenLap);
            Set("CloseLap", value.CloseLap);
            Set("IsOpen", value.IsOpen);
            Set("MustPitThisLap", value.MustPitThisLap);
            Set("CanReachWindow", value.CanReachWindow);
            Set("LapsUntilOpen", value.LapsUntilOpen);
            Set("LapsUntilClose", value.LapsUntilClose);
            Set("WindowText", value.WindowText);
            Set("CountdownText", value.CountdownText);
            Set("IsOnPitRoad", value.IsOnPitRoad);
            Set("JustEnteredPits", value.JustEnteredPits);
            Set("JustExitedPits", value.JustExitedPits);
            Set("PitStopCount", value.PitStopCount);
            Set("LastPitEntryLap", value.LastPitEntryLap);
            Set("LastPitExitLap", value.LastPitExitLap);
            Set("CurrentStintLap", value.CurrentStintLap);
            Set("FuelLapsRemaining", value.FuelLapsRemaining);
            Set("FullTankStintLaps", value.FullTankStintLaps);
            Set("EstimatedSessionLapsRemaining", value.EstimatedSessionLapsRemaining);
            Set("EstimatedStopsRemaining", value.EstimatedStopsRemaining);
            Set("RecommendedFuelToAddLiters", value.RecommendedFuelToAddLiters);
            Set("MaximumFuelToAddLiters", value.MaximumFuelToAddLiters);
            Set("CanFinishWithoutStop", value.CanFinishWithoutStop);
            Set("Event.Name", value.EventName);
            Set("Event.Sequence", value.EventSequence);
        }

        private void RegisterProperties()
        {
            Add("Ready", false, "True when a valid fuel-based pit window is available");
            Add("HasWindow", false, "True when opening and closing laps are available");
            Add("Status", "Unavailable", "Current pit-window state");
            Add("Summary", "Waiting for fuel and session data", "Dashboard-ready pit-window summary");
            Add("Recommendation", "Monitor", "Current pit-stop recommendation");
            Add("CurrentLap", 0, "Current completed lap");
            Add("OpenLap", 0, "Estimated pit-window opening lap");
            Add("CloseLap", 0, "Estimated latest safe pit lap");
            Add("IsOpen", false, "True while the estimated pit window is open");
            Add("MustPitThisLap", false, "True when this is the latest safe fuel lap");
            Add("CanReachWindow", false, "True when current fuel can reach the opening lap");
            Add("LapsUntilOpen", 0, "Laps until the pit window opens");
            Add("LapsUntilClose", 0, "Laps until the pit window closes");
            Add("WindowText", "--", "Formatted opening and closing lap range");
            Add("CountdownText", "--", "Formatted pit-window countdown");
            Add("IsOnPitRoad", false, "True while the player is on pit road");
            Add("JustEnteredPits", false, "True for one update after pit entry");
            Add("JustExitedPits", false, "True for one update after pit exit");
            Add("PitStopCount", 0, "Pit-road entries detected in the current session");
            Add("LastPitEntryLap", -1, "Lap of the latest detected pit entry");
            Add("LastPitExitLap", -1, "Lap of the latest detected pit exit");
            Add("CurrentStintLap", 0, "Completed laps since the latest pit exit");
            Add("FuelLapsRemaining", 0.0, "Estimated laps available in the current tank");
            Add("FullTankStintLaps", 0.0, "Estimated safe laps from a full tank");
            Add("EstimatedSessionLapsRemaining", 0.0, "Estimated race distance remaining");
            Add("EstimatedStopsRemaining", 0, "Estimated additional fuel stops required");
            Add("RecommendedFuelToAddLiters", 0.0, "Recommended fuel amount for the next stop");
            Add("MaximumFuelToAddLiters", 0.0, "Maximum fuel that currently fits in the tank");
            Add("CanFinishWithoutStop", false, "True when no additional fuel stop is required");
            Add("Event.Name", "None", "Latest pit-window state transition");
            Add("Event.Sequence", 0, "Incrementing pit-window event identifier");
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
