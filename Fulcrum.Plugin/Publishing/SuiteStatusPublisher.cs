using System;
using Fulcrum.Core.Runtime;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Publishing
{
    public sealed class SuiteStatusPublisher
    {
        private const string Prefix = "Fulcrum.Suite.";
        private readonly PluginManager pluginManager;
        private readonly Type pluginType;

        public SuiteStatusPublisher(PluginManager pluginManager, Type pluginType)
        {
            this.pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
            this.pluginType = pluginType ?? throw new ArgumentNullException(nameof(pluginType));
            RegisterProperties();
        }

        public void Publish(SuiteStatusSnapshot value)
        {
            Set("Ready", value.Ready);
            Set("Health", value.Health);
            Set("Summary", value.Summary);
            Set("ReadyModuleCount", value.ReadyModuleCount);
            Set("TotalModuleCount", value.TotalModuleCount);
            Set("PrimaryAlert", value.PrimaryAlert);
            Set("PrimaryAlertPriority", value.PrimaryAlertPriority);
            Set("PrimaryAction", value.PrimaryAction);
            Set("MissingModules", value.MissingModules);
            Set("CoreStatus", value.CoreStatus);

            Set("Module.Relative.Ready", value.RelativeReady);
            Set("Module.Radar.Ready", value.RadarReady);
            Set("Module.Fuel.Ready", value.FuelReady);
            Set("Module.Damage.Ready", value.DamageReady);
            Set("Module.Delta.Ready", value.DeltaReady);
            Set("Module.Spotter.Ready", value.SpotterReady);
            Set("Module.PitWindow.Ready", value.PitWindowReady);
            Set("Module.Strategy.Ready", value.StrategyReady);
            Set("Module.Standings.Ready", value.StandingsReady);

            Set("Module.Relative.Status", value.RelativeStatus);
            Set("Module.Radar.Status", value.RadarStatus);
            Set("Module.Fuel.Status", value.FuelStatus);
            Set("Module.Damage.Status", value.DamageStatus);
            Set("Module.Delta.Status", value.DeltaStatus);
            Set("Module.Spotter.Status", value.SpotterStatus);
            Set("Module.PitWindow.Status", value.PitWindowStatus);
            Set("Module.Strategy.Status", value.StrategyStatus);
            Set("Module.Standings.Status", value.StandingsStatus);
        }

        private void RegisterProperties()
        {
            Add("Ready", false, "True when the core real-time modules are operational");
            Add("Health", "Offline", "Overall Fulcrum Suite runtime health");
            Add("Summary", "Waiting for game", "Short module readiness summary");
            Add("ReadyModuleCount", 0, "Number of currently ready user-facing modules");
            Add("TotalModuleCount", 9, "Total user-facing modules monitored");
            Add("PrimaryAlert", "None", "Highest-priority active alert across the suite");
            Add("PrimaryAlertPriority", 0, "Priority of the consolidated alert from 0 to 100");
            Add("PrimaryAction", "Wait", "Highest-priority recommended action");
            Add("MissingModules", "Relative,Radar,Fuel,Damage,Delta,Spotter,PitWindow,Strategy,Standings", "Comma-separated modules that are not ready");
            Add("CoreStatus", "Waiting for game", "Readiness explanation for the four core real-time modules");

            Add("Module.Relative.Ready", false, "Relative module readiness");
            Add("Module.Radar.Ready", false, "Radar module readiness");
            Add("Module.Fuel.Ready", false, "Fuel module readiness");
            Add("Module.Damage.Ready", false, "Damage module readiness");
            Add("Module.Delta.Ready", false, "Delta module readiness");
            Add("Module.Spotter.Ready", false, "Spotter module readiness");
            Add("Module.PitWindow.Ready", false, "Pit Window module readiness");
            Add("Module.Strategy.Ready", false, "Strategy module readiness");
            Add("Module.Standings.Ready", false, "Standings module readiness");

            Add("Module.Relative.Status", "Waiting", "Relative readiness detail");
            Add("Module.Radar.Status", "Waiting", "Radar readiness detail");
            Add("Module.Fuel.Status", "Waiting", "Fuel readiness detail");
            Add("Module.Damage.Status", "Waiting", "Damage readiness detail");
            Add("Module.Delta.Status", "Waiting", "Delta readiness detail");
            Add("Module.Spotter.Status", "Waiting", "Spotter readiness detail");
            Add("Module.PitWindow.Status", "Waiting", "Pit Window readiness detail");
            Add("Module.Strategy.Status", "Waiting", "Strategy readiness detail");
            Add("Module.Standings.Status", "Waiting", "Standings readiness detail");
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
