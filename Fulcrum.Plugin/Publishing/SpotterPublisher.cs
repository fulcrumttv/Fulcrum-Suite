using System;
using Fulcrum.Core.Spotter;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Publishing
{
    public sealed class SpotterPublisher
    {
        private const string Prefix = "Fulcrum.Spotter.";
        private readonly PluginManager pluginManager;
        private readonly Type pluginType;

        public SpotterPublisher(PluginManager pluginManager, Type pluginType)
        {
            this.pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
            this.pluginType = pluginType ?? throw new ArgumentNullException(nameof(pluginType));
            RegisterProperties();
        }

        public void Publish(SpotterSnapshot value)
        {
            Set("Ready", value.Ready);
            Set("State", value.State);
            Set("Callout", value.Callout);
            Set("CalloutCode", value.CalloutCode);
            Set("Priority", value.Priority);
            Set("IsUrgent", value.IsUrgent);
            Set("HasActiveCallout", value.HasActiveCallout);
            Set("HasCarLeft", value.HasCarLeft);
            Set("HasCarRight", value.HasCarRight);
            Set("HasCarsBothSides", value.HasCarsBothSides);
            Set("IsClear", value.IsClear);
            Set("BlueFlag", value.BlueFlag);
            Set("YellowFlag", value.YellowFlag);
            Set("MeatballFlag", value.MeatballFlag);
            Set("FasterClassApproaching", value.FasterClassApproaching);
            Set("DefenseRequired", value.DefenseRequired);
            Set("SuggestedAction", value.SuggestedAction);
            Set("Event.Name", value.EventName);
            Set("Event.Sequence", value.EventSequence);
        }

        private void RegisterProperties()
        {
            Add("Ready", false, "True while the Spotter Engine has valid inputs");
            Add("State", "Unavailable", "Current spotter state");
            Add("Callout", string.Empty, "Dashboard-ready spotter text");
            Add("CalloutCode", "NONE", "Stable code for audio or visual integrations");
            Add("Priority", 0, "Callout priority from 0 to 100");
            Add("IsUrgent", false, "True for immediate safety-related callouts");
            Add("HasActiveCallout", false, "True only when a new callout event is emitted");
            Add("HasCarLeft", false, "Car overlapping on the left");
            Add("HasCarRight", false, "Car overlapping on the right");
            Add("HasCarsBothSides", false, "Cars overlapping on both sides");
            Add("IsClear", true, "No current lateral overlap");
            Add("BlueFlag", false, "Blue flag currently active");
            Add("YellowFlag", false, "Yellow or caution flag currently active");
            Add("MeatballFlag", false, "Repair flag currently active");
            Add("FasterClassApproaching", false, "Faster class approaching from behind");
            Add("DefenseRequired", false, "Immediate same-class rear pressure detected");
            Add("SuggestedAction", "Maintain pace", "Current Race Intelligence action");
            Add("Event.Name", "None", "Latest new spotter event");
            Add("Event.Sequence", 0, "Increments once for each new spotter event");
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
