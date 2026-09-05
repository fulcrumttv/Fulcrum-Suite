using System;
using Fulcrum.Core.Strategy;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Publishing
{
    public sealed class StrategyPublisher
    {
        private const string Prefix = "Fulcrum.Strategy.";
        private readonly PluginManager pluginManager;
        private readonly Type pluginType;

        public StrategyPublisher(PluginManager pluginManager, Type pluginType)
        {
            this.pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
            this.pluginType = pluginType ?? throw new ArgumentNullException(nameof(pluginType));
            RegisterProperties();
        }

        public void Publish(StrategySnapshot value)
        {
            Set("Ready", value.Ready);
            Set("Status", value.Status);
            Set("Summary", value.Summary);
            Set("Recommendation", value.Recommendation);
            Set("RecommendationReason", value.RecommendationReason);
            Set("RiskLevel", value.RiskLevel);
            Set("RiskScore", value.RiskScore);
            Set("CanFinish", value.CanFinish);
            Set("NeedSplash", value.NeedSplash);
            Set("TargetFuelLiters", value.TargetFuelLiters);
            Set("FuelMarginLiters", value.FuelMarginLiters);
            Set("FuelMarginLaps", value.FuelMarginLaps);
            Set("PitWindow.OpenLap", value.PitWindowOpenLap);
            Set("PitWindow.CloseLap", value.PitWindowCloseLap);
            Set("PitWindow.IsOpen", value.PitWindowIsOpen);
            Set("PitWindow.LapsUntilOpen", value.LapsUntilPitWindowOpen);
            Set("PitWindow.LapsUntilClose", value.LapsUntilPitWindowClose);
            Set("PitWindow.MustPitThisLap", value.MustPitThisLap);
            Set("TrafficAhead", value.TrafficAhead);
            Set("FastClassIncoming", value.FastClassIncoming);
            Set("CleanAir", value.CleanAir);
            Set("AttackAvailable", value.AttackAvailable);
            Set("DefenseRequired", value.DefenseRequired);
            Set("Event.Name", value.EventName);
            Set("Event.Sequence", value.EventSequence);
        }

        private void RegisterProperties()
        {
            Add("Ready", false, "True when strategy inputs are valid");
            Add("Status", "Unavailable", "Collecting, CanFinish, FuelShort, PitWindowOpen or PitNow");
            Add("Summary", "Waiting for race data", "Dashboard-ready strategy summary");
            Add("Recommendation", "Monitor", "Current high-level race strategy recommendation");
            Add("RecommendationReason", string.Empty, "Reason for the current strategy recommendation");
            Add("RiskLevel", "Low", "Low, Medium, High or Critical");
            Add("RiskScore", 0, "Combined strategy risk score from 0 to 100");
            Add("CanFinish", false, "True when current fuel can reach the finish with reserve");
            Add("NeedSplash", false, "True when only a short splash of fuel is required");
            Add("TargetFuelLiters", 0.0, "Fuel target required to finish including reserve");
            Add("FuelMarginLiters", 0.0, "Fuel surplus or deficit relative to the finish target");
            Add("FuelMarginLaps", 0.0, "Fuel surplus or deficit expressed in laps");
            Add("PitWindow.OpenLap", 0, "Earliest estimated lap for the final fuel stop");
            Add("PitWindow.CloseLap", 0, "Latest estimated safe lap for the final fuel stop");
            Add("PitWindow.IsOpen", false, "True when the estimated final-stop window is open");
            Add("PitWindow.LapsUntilOpen", 0, "Estimated laps until the fuel pit window opens");
            Add("PitWindow.LapsUntilClose", 0, "Estimated laps until the fuel pit window closes");
            Add("PitWindow.MustPitThisLap", false, "True when this is the latest safe fuel lap");
            Add("TrafficAhead", false, "True when relevant traffic is close ahead");
            Add("FastClassIncoming", false, "True when faster-class traffic is approaching");
            Add("CleanAir", true, "True when no immediate traffic is detected");
            Add("AttackAvailable", false, "True when race intelligence detects an attack opportunity");
            Add("DefenseRequired", false, "True when race intelligence recommends defending");
            Add("Event.Name", "None", "Latest strategy event emitted by a state transition");
            Add("Event.Sequence", 0, "Incrementing identifier for strategy events");
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
