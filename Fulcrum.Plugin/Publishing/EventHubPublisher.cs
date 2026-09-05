using System;
using Fulcrum.Core.Events;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Publishing
{
    internal sealed class EventHubPublisher
    {
        private readonly PluginManager pluginManager;
        private readonly Type pluginType;

        public EventHubPublisher(PluginManager pluginManager, Type pluginType)
        {
            this.pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
            this.pluginType = pluginType ?? throw new ArgumentNullException(nameof(pluginType));
            RegisterProperties();
        }

        public void Publish(EventHubSnapshot snapshot)
        {
            pluginManager.SetPropertyValue("Fulcrum.Events.Ready", pluginType, snapshot.Ready);
            pluginManager.SetPropertyValue("Fulcrum.Events.Last.Name", pluginType, snapshot.LastEventName);
            pluginManager.SetPropertyValue("Fulcrum.Events.Last.Category", pluginType, snapshot.Category);
            pluginManager.SetPropertyValue("Fulcrum.Events.Last.Message", pluginType, snapshot.Message);
            pluginManager.SetPropertyValue("Fulcrum.Events.Last.Action", pluginType, snapshot.SuggestedAction);
            pluginManager.SetPropertyValue("Fulcrum.Events.Last.Priority", pluginType, snapshot.Priority);
            pluginManager.SetPropertyValue("Fulcrum.Events.Last.IsUrgent", pluginType, snapshot.IsUrgent);
            pluginManager.SetPropertyValue("Fulcrum.Events.Last.Sequence", pluginType, snapshot.Sequence);
            pluginManager.SetPropertyValue("Fulcrum.Events.Last.OccurredAtUtc", pluginType,
                snapshot.OccurredAtUtc == DateTime.MinValue ? string.Empty : snapshot.OccurredAtUtc.ToString("O"));
            pluginManager.SetPropertyValue("Fulcrum.Events.Active.Count", pluginType, snapshot.ActiveAlertCount);
            pluginManager.SetPropertyValue("Fulcrum.Events.Active.HighestPriority", pluginType, snapshot.HighestActivePriority);
            pluginManager.SetPropertyValue("Fulcrum.Events.Active.Names", pluginType, snapshot.ActiveAlerts);
        }

        private void RegisterProperties()
        {
            pluginManager.AddProperty("Fulcrum.Events.Ready", pluginType, false, "True when the consolidated event hub is active");
            pluginManager.AddProperty("Fulcrum.Events.Last.Name", pluginType, "None", "Most recent Fulcrum event name");
            pluginManager.AddProperty("Fulcrum.Events.Last.Category", pluginType, "None", "Source category of the most recent event");
            pluginManager.AddProperty("Fulcrum.Events.Last.Message", pluginType, string.Empty, "Display-ready message for the most recent event");
            pluginManager.AddProperty("Fulcrum.Events.Last.Action", pluginType, "Maintain pace", "Suggested action for the most recent event");
            pluginManager.AddProperty("Fulcrum.Events.Last.Priority", pluginType, 0, "Priority of the most recent event from 0 to 100");
            pluginManager.AddProperty("Fulcrum.Events.Last.IsUrgent", pluginType, false, "True when the most recent event is urgent");
            pluginManager.AddProperty("Fulcrum.Events.Last.Sequence", pluginType, 0, "Increments whenever a new consolidated event is emitted");
            pluginManager.AddProperty("Fulcrum.Events.Last.OccurredAtUtc", pluginType, string.Empty, "UTC timestamp of the most recent event");
            pluginManager.AddProperty("Fulcrum.Events.Active.Count", pluginType, 0, "Number of currently active alerts");
            pluginManager.AddProperty("Fulcrum.Events.Active.HighestPriority", pluginType, 0, "Highest priority among active alerts");
            pluginManager.AddProperty("Fulcrum.Events.Active.Names", pluginType, string.Empty, "Compact comma-separated list of active alerts");
        }
    }
}
