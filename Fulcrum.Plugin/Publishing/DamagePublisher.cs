using System;
using Fulcrum.Core.Damage;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Publishing
{
    public sealed class DamagePublisher
    {
        private const string Prefix = "Fulcrum.Damage.";
        private readonly PluginManager pluginManager;
        private readonly Type pluginType;

        public DamagePublisher(PluginManager pluginManager, Type pluginType)
        {
            this.pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
            this.pluginType = pluginType ?? throw new ArgumentNullException(nameof(pluginType));
            RegisterProperties();
        }

        public void Publish(DamageSnapshot value)
        {
            Set("Ready", value.Ready);
            Set("HasConfirmedDamage", value.HasConfirmedDamage);
            Set("HasSuspectedDamage", value.HasSuspectedDamage);
            Set("HasRequiredRepairs", value.HasRequiredRepairs);
            Set("HasOptionalRepairs", value.HasOptionalRepairs);
            Set("HasMeatballFlag", value.HasMeatballFlag);
            Set("HasBlackFlag", value.HasBlackFlag);
            Set("IsDisqualified", value.IsDisqualified);
            Set("IsRepairing", value.IsRepairing);
            Set("IsTowing", value.IsTowing);
            Set("RequiredRepairSeconds", value.RequiredRepairSeconds);
            Set("OptionalRepairSeconds", value.OptionalRepairSeconds);
            Set("TotalRepairSeconds", value.TotalRepairSeconds);
            Set("TowTimeSeconds", value.TowTimeSeconds);
            Set("IncidentDelta", value.IncidentDelta);
            Set("DriverIncidentCount", value.DriverIncidentCount);
            Set("MyIncidentCount", value.MyIncidentCount);
            Set("TeamIncidentCount", value.TeamIncidentCount);
            Set("FastRepairAvailable", value.FastRepairAvailable);
            Set("FastRepairUsed", value.FastRepairUsed);
            Set("FastRepairsUsed", value.FastRepairsUsed);
            Set("PitServiceStatus", value.PitServiceStatus);
            Set("SessionFlagsRaw", value.SessionFlagsRaw);
            Set("Severity", value.Severity);
            Set("Status", value.Status);
            Set("Summary", value.Summary);
            Set("Event.Name", value.EventName);
            Set("Event.Sequence", value.EventSequence);
            Set("Error", value.Error);

            Set("Diagnostics.RequiredRepairTelemetryFound", value.RequiredRepairTelemetryFound);
            Set("Diagnostics.OptionalRepairTelemetryFound", value.OptionalRepairTelemetryFound);
            Set("Diagnostics.TowTelemetryFound", value.TowTelemetryFound);
            Set("Diagnostics.SessionFlagsTelemetryFound", value.SessionFlagsTelemetryFound);
            Set("Diagnostics.DetectedFields", BuildDetectedFields(value));
            Set("Diagnostics.Summary", value.DiagnosticSummary);
        }

        private void RegisterProperties()
        {
            Add("Ready", false, "True when supported iRacing damage, repair, incident or flag telemetry is available");
            Add("HasConfirmedDamage", false, "True when repair time or the iRacing repair flag confirms damage");
            Add("HasSuspectedDamage", false, "True briefly after a new incident before damage is confirmed");
            Add("HasRequiredRepairs", false, "True when mandatory repair time is currently reported");
            Add("HasOptionalRepairs", false, "True when optional repair time is currently reported");
            Add("HasMeatballFlag", false, "True when the iRacing repair/meatball flag is active");
            Add("HasBlackFlag", false, "True when a black flag bit is active");
            Add("IsDisqualified", false, "True when the disqualification flag bit is active");
            Add("IsRepairing", false, "True while repair time is decreasing in the pit stall");
            Add("IsTowing", false, "True while tow time is active");
            Add("RequiredRepairSeconds", 0.0, "Mandatory repair time remaining; may stay zero until the pit stall");
            Add("OptionalRepairSeconds", 0.0, "Optional repair time remaining; may stay zero until the pit stall");
            Add("TotalRepairSeconds", 0.0, "Required plus optional repair time");
            Add("TowTimeSeconds", 0.0, "Tow time remaining");
            Add("IncidentDelta", 0, "Increase in incident count detected on this update");
            Add("DriverIncidentCount", 0, "Current driver incident count");
            Add("MyIncidentCount", 0, "Current personal incident count");
            Add("TeamIncidentCount", 0, "Current team incident count");
            Add("FastRepairAvailable", false, "True when a fast repair is available according to telemetry");
            Add("FastRepairUsed", false, "Current fast repair used flag");
            Add("FastRepairsUsed", 0, "Number of fast repairs used");
            Add("PitServiceStatus", 0, "Raw iRacing pit service status");
            Add("SessionFlagsRaw", 0L, "Raw iRacing session flag bit mask used for repair and black-flag detection");
            Add("Severity", "None", "None, Suspected, Optional, Minor, Moderate, Severe or Critical");
            Add("Status", "Unavailable", "Clear, DamageSuspected, MeatballFlag, DamageConfirmed, OptionalRepairs, Repairing, Towing, BlackFlag or Disqualified");
            Add("Summary", "Damage telemetry unavailable", "Dashboard-ready vehicle health summary");
            Add("Event.Name", "None", "Latest damage event");
            Add("Event.Sequence", 0, "Increments when a new damage event occurs");
            Add("Error", string.Empty, "Damage module diagnostic error");

            Add("Diagnostics.RequiredRepairTelemetryFound", false, "True when PitRepairLeft exists in the telemetry source");
            Add("Diagnostics.OptionalRepairTelemetryFound", false, "True when PitOptRepairLeft exists in the telemetry source");
            Add("Diagnostics.TowTelemetryFound", false, "True when PlayerCarTowTime exists in the telemetry source");
            Add("Diagnostics.SessionFlagsTelemetryFound", false, "True when SessionFlags exists in the telemetry source");
            Add("Diagnostics.DetectedFields", string.Empty, "Short list of supported damage telemetry groups found at runtime");
            Add("Diagnostics.Summary", "No damage telemetry read yet", "Explanation of the current damage telemetry state");
        }


        private static string BuildDetectedFields(DamageSnapshot value)
        {
            string result = string.Empty;
            if (value.RequiredRepairTelemetryFound || value.OptionalRepairTelemetryFound) result = AppendField(result, "Repair");
            if (value.TowTelemetryFound) result = AppendField(result, "Tow");
            if (value.FastRepairAvailable || value.FastRepairUsed || value.FastRepairsUsed > 0) result = AppendField(result, "FastRepair");
            if (value.SessionFlagsTelemetryFound) result = AppendField(result, "Flags");
            return result;
        }

        private static string AppendField(string current, string value)
        {
            return string.IsNullOrEmpty(current) ? value : current + "," + value;
        }

        private void Add(string name, object defaultValue, string description) { pluginManager.AddProperty(Prefix + name, pluginType, defaultValue, description); }
        private void Set(string name, object value) { pluginManager.SetPropertyValue(Prefix + name, pluginType, value); }
    }
}
