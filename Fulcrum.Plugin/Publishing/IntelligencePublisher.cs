using System;
using Fulcrum.Core.Intelligence;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Publishing
{
    public sealed class IntelligencePublisher
    {
        private const string Prefix = "Fulcrum.Intelligence.";
        private readonly PluginManager pluginManager;
        private readonly Type pluginType;

        public IntelligencePublisher(PluginManager pluginManager, Type pluginType)
        {
            this.pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
            this.pluginType = pluginType ?? throw new ArgumentNullException(nameof(pluginType));
            RegisterProperties();
        }

        public void Publish(RaceIntelligenceSnapshot value)
        {
            Set("Ready", value.Ready);
            Set("ThreatScore", value.ThreatScore);
            Set("ThreatLevel", value.ThreatLevel);
            Set("ThreatReason", value.ThreatReason);
            Set("AttackOpportunity", value.AttackOpportunity);
            Set("HasAttackOpportunity", value.HasAttackOpportunity);
            Set("DefenseRequired", value.DefenseRequired);
            Set("ClosingCarAhead", value.ClosingCarAhead);
            Set("ClosingCarBehind", value.ClosingCarBehind);
            Set("ClosingRateAhead", value.ClosingRateAhead);
            Set("ClosingRateBehind", value.ClosingRateBehind);
            Set("CarAheadInPits", value.CarAheadInPits);
            Set("CarBehindInPits", value.CarBehindInPits);
            Set("FasterClassApproaching", value.FasterClassApproaching);
            Set("SlowerClassAhead", value.SlowerClassAhead);
            Set("ClassTraffic", value.ClassTraffic);
            Set("SuggestedAction", value.SuggestedAction);
            Set("Summary", value.Summary);
        }

        private void RegisterProperties()
        {
            Add("Ready", false, "True when race intelligence has usable player data");
            Add("ThreatScore", 0, "Traffic threat score from 0 to 100");
            Add("ThreatLevel", "Safe", "Safe, Caution, Danger or Critical");
            Add("ThreatReason", string.Empty, "Reason for the current threat level");
            Add("AttackOpportunity", "None", "None, Possible, Good or Excellent");
            Add("HasAttackOpportunity", false, "True when a same-class attack may be possible");
            Add("DefenseRequired", false, "True when nearby traffic may require defense");
            Add("ClosingCarAhead", false, "True when the player is gaining on the nearest car ahead");
            Add("ClosingCarBehind", false, "True when the nearest car behind is gaining");
            Add("ClosingRateAhead", 0.0, "Approximate gap reduction toward the car ahead in seconds per second");
            Add("ClosingRateBehind", 0.0, "Approximate rear gap reduction in seconds per second");
            Add("CarAheadInPits", false, "True when the nearest car ahead is in the pit area");
            Add("CarBehindInPits", false, "True when the nearest car behind is in the pit area");
            Add("FasterClassApproaching", false, "True when different-class rear traffic is closing");
            Add("SlowerClassAhead", false, "True when the nearest car ahead is a different class");
            Add("ClassTraffic", "None", "Class name of the nearest relevant multiclass traffic");
            Add("SuggestedAction", "Maintain pace", "Short dashboard-ready traffic suggestion");
            Add("Summary", "No immediate traffic threat", "Compact intelligence summary");
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
