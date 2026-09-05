using System;

namespace Fulcrum.Core.Damage
{
    public sealed class DamageSnapshot
    {
        public bool Ready { get; set; }
        public bool HasConfirmedDamage { get; set; }
        public bool HasSuspectedDamage { get; set; }
        public bool HasRequiredRepairs { get; set; }
        public bool HasOptionalRepairs { get; set; }
        public bool HasMeatballFlag { get; set; }
        public bool HasBlackFlag { get; set; }
        public bool IsDisqualified { get; set; }
        public bool IsRepairing { get; set; }
        public bool IsTowing { get; set; }
        public double RequiredRepairSeconds { get; set; }
        public double OptionalRepairSeconds { get; set; }
        public double TotalRepairSeconds { get; set; }
        public double TowTimeSeconds { get; set; }
        public int IncidentDelta { get; set; }
        public int DriverIncidentCount { get; set; }
        public int MyIncidentCount { get; set; }
        public int TeamIncidentCount { get; set; }
        public bool FastRepairAvailable { get; set; }
        public bool FastRepairUsed { get; set; }
        public int FastRepairsUsed { get; set; }
        public int PitServiceStatus { get; set; }
        public long SessionFlagsRaw { get; set; }
        public string Severity { get; set; }
        public string Status { get; set; }
        public string Summary { get; set; }
        public string EventName { get; set; }
        public int EventSequence { get; set; }
        public string Error { get; set; }
        public DateTime UpdatedAtUtc { get; set; }

        public bool RequiredRepairTelemetryFound { get; set; }
        public bool OptionalRepairTelemetryFound { get; set; }
        public bool TowTelemetryFound { get; set; }
        public bool SessionFlagsTelemetryFound { get; set; }
        public string AvailableTelemetryKeys { get; set; }
        public string DiagnosticSummary { get; set; }

        public DamageSnapshot() { Reset(); }

        public void Reset()
        {
            Ready = false;
            HasConfirmedDamage = false;
            HasSuspectedDamage = false;
            HasRequiredRepairs = false;
            HasOptionalRepairs = false;
            HasMeatballFlag = false;
            HasBlackFlag = false;
            IsDisqualified = false;
            IsRepairing = false;
            IsTowing = false;
            RequiredRepairSeconds = 0.0;
            OptionalRepairSeconds = 0.0;
            TotalRepairSeconds = 0.0;
            TowTimeSeconds = 0.0;
            IncidentDelta = 0;
            DriverIncidentCount = 0;
            MyIncidentCount = 0;
            TeamIncidentCount = 0;
            FastRepairAvailable = false;
            FastRepairUsed = false;
            FastRepairsUsed = 0;
            PitServiceStatus = 0;
            SessionFlagsRaw = 0L;
            Severity = "None";
            Status = "Unavailable";
            Summary = "Damage telemetry unavailable";
            EventName = "None";
            EventSequence = 0;
            Error = string.Empty;
            UpdatedAtUtc = DateTime.MinValue;
            RequiredRepairTelemetryFound = false;
            OptionalRepairTelemetryFound = false;
            TowTelemetryFound = false;
            SessionFlagsTelemetryFound = false;
            AvailableTelemetryKeys = string.Empty;
            DiagnosticSummary = "No damage telemetry read yet";
        }
    }
}
