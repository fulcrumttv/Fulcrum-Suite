namespace Fulcrum.Core.Damage
{
    public sealed class DamageTelemetry
    {
        public bool Available { get; set; }
        public double RequiredRepairSeconds { get; set; }
        public double OptionalRepairSeconds { get; set; }
        public double TowTimeSeconds { get; set; }
        public bool IsInPitStall { get; set; }
        public int PitServiceStatus { get; set; }
        public bool FastRepairAvailable { get; set; }
        public bool FastRepairUsed { get; set; }
        public int FastRepairsUsed { get; set; }
        public int DriverIncidentCount { get; set; }
        public int MyIncidentCount { get; set; }
        public int TeamIncidentCount { get; set; }

        public long SessionFlagsRaw { get; set; }
        public bool HasRepairFlag { get; set; }
        public bool HasBlackFlag { get; set; }
        public bool HasDisqualifyFlag { get; set; }

        public bool RequiredRepairTelemetryFound { get; set; }
        public bool OptionalRepairTelemetryFound { get; set; }
        public bool TowTelemetryFound { get; set; }
        public bool SessionFlagsTelemetryFound { get; set; }
        public string AvailableTelemetryKeys { get; set; }
    }
}
