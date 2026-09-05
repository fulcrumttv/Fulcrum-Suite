using System;

namespace Fulcrum.Core.Damage
{
    public sealed class DamageEngine
    {
        private int previousIncidentCount;
        private double previousRepairSeconds;
        private bool initialized;
        private DateTime suspectedUntilUtc;
        private bool previousConfirmedDamage;
        private bool previousRepairing;
        private bool previousFastRepairUsed;
        private bool previousMeatballFlag;
        private bool previousTow;
        private int eventSequence;

        public void Reset(DamageSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            initialized = false;
            previousIncidentCount = 0;
            previousRepairSeconds = 0.0;
            suspectedUntilUtc = DateTime.MinValue;
            previousConfirmedDamage = false;
            previousRepairing = false;
            previousFastRepairUsed = false;
            previousMeatballFlag = false;
            previousTow = false;
            eventSequence = 0;
            snapshot.Reset();
            snapshot.EventSequence = 0;
        }

        public void Update(DamageTelemetry telemetry, DamageSnapshot snapshot)
        {
            if (telemetry == null) throw new ArgumentNullException(nameof(telemetry));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            DateTime now = DateTime.UtcNow;
            snapshot.UpdatedAtUtc = now;
            snapshot.Ready = telemetry.Available;
            snapshot.Error = telemetry.Available ? string.Empty : "iRacing damage telemetry was not found";
            snapshot.EventName = "None";
            snapshot.IncidentDelta = 0;

            if (!telemetry.Available)
            {
                snapshot.Status = "Unavailable";
                snapshot.Summary = "Damage telemetry unavailable";
                snapshot.DiagnosticSummary = "No supported damage, incident or flag keys were found";
                return;
            }

            int currentIncident = Math.Max(telemetry.DriverIncidentCount, telemetry.MyIncidentCount);
            if (initialized && currentIncident > previousIncidentCount)
            {
                snapshot.IncidentDelta = currentIncident - previousIncidentCount;
                suspectedUntilUtc = now.AddSeconds(20.0);
            }

            snapshot.RequiredRepairSeconds = telemetry.RequiredRepairSeconds;
            snapshot.OptionalRepairSeconds = telemetry.OptionalRepairSeconds;
            snapshot.TotalRepairSeconds = telemetry.RequiredRepairSeconds + telemetry.OptionalRepairSeconds;
            snapshot.TowTimeSeconds = telemetry.TowTimeSeconds;
            snapshot.HasRequiredRepairs = telemetry.RequiredRepairSeconds > 0.05;
            snapshot.HasOptionalRepairs = telemetry.OptionalRepairSeconds > 0.05;
            snapshot.HasMeatballFlag = telemetry.HasRepairFlag;
            snapshot.HasBlackFlag = telemetry.HasBlackFlag;
            snapshot.IsDisqualified = telemetry.HasDisqualifyFlag;
            snapshot.HasConfirmedDamage = snapshot.HasRequiredRepairs || snapshot.HasOptionalRepairs || snapshot.HasMeatballFlag;
            snapshot.IsTowing = telemetry.TowTimeSeconds > 0.05;
            snapshot.IsRepairing = telemetry.IsInPitStall && previousRepairSeconds > snapshot.TotalRepairSeconds + 0.05;
            snapshot.HasSuspectedDamage = !snapshot.HasConfirmedDamage && now < suspectedUntilUtc;
            snapshot.DriverIncidentCount = telemetry.DriverIncidentCount;
            snapshot.MyIncidentCount = telemetry.MyIncidentCount;
            snapshot.TeamIncidentCount = telemetry.TeamIncidentCount;
            snapshot.FastRepairAvailable = telemetry.FastRepairAvailable;
            snapshot.FastRepairUsed = telemetry.FastRepairUsed;
            snapshot.FastRepairsUsed = telemetry.FastRepairsUsed;
            snapshot.PitServiceStatus = telemetry.PitServiceStatus;
            snapshot.SessionFlagsRaw = telemetry.SessionFlagsRaw;
            snapshot.RequiredRepairTelemetryFound = telemetry.RequiredRepairTelemetryFound;
            snapshot.OptionalRepairTelemetryFound = telemetry.OptionalRepairTelemetryFound;
            snapshot.TowTelemetryFound = telemetry.TowTelemetryFound;
            snapshot.SessionFlagsTelemetryFound = telemetry.SessionFlagsTelemetryFound;
            snapshot.AvailableTelemetryKeys = telemetry.AvailableTelemetryKeys ?? string.Empty;

            UpdateSeverity(snapshot);
            UpdateStatus(snapshot);
            UpdateDiagnostics(snapshot);
            UpdateEvent(snapshot);

            previousIncidentCount = currentIncident;
            previousRepairSeconds = snapshot.TotalRepairSeconds;
            previousConfirmedDamage = snapshot.HasConfirmedDamage;
            previousRepairing = snapshot.IsRepairing;
            previousFastRepairUsed = snapshot.FastRepairUsed;
            previousMeatballFlag = snapshot.HasMeatballFlag;
            previousTow = snapshot.IsTowing;
            initialized = true;
        }

        private static void UpdateSeverity(DamageSnapshot snapshot)
        {
            if (snapshot.IsTowing || snapshot.IsDisqualified) snapshot.Severity = "Critical";
            else if (snapshot.RequiredRepairSeconds >= 45.0) snapshot.Severity = "Severe";
            else if (snapshot.RequiredRepairSeconds >= 15.0 || snapshot.HasMeatballFlag) snapshot.Severity = "Moderate";
            else if (snapshot.HasRequiredRepairs) snapshot.Severity = "Minor";
            else if (snapshot.HasOptionalRepairs) snapshot.Severity = "Optional";
            else if (snapshot.HasSuspectedDamage) snapshot.Severity = "Suspected";
            else snapshot.Severity = "None";
        }

        private static void UpdateStatus(DamageSnapshot snapshot)
        {
            if (snapshot.IsDisqualified)
            {
                snapshot.Status = "Disqualified";
                snapshot.Summary = "Disqualified";
            }
            else if (snapshot.IsTowing)
            {
                snapshot.Status = "Towing";
                snapshot.Summary = string.Format("Towing | {0:0.0}s remaining", snapshot.TowTimeSeconds);
            }
            else if (snapshot.IsRepairing)
            {
                snapshot.Status = "Repairing";
                snapshot.Summary = string.Format("Repairing | {0:0.0}s remaining", snapshot.TotalRepairSeconds);
            }
            else if (snapshot.HasRequiredRepairs)
            {
                snapshot.Status = "DamageConfirmed";
                snapshot.Summary = string.Format("{0} damage | required repair {1:0.0}s", snapshot.Severity, snapshot.RequiredRepairSeconds);
            }
            else if (snapshot.HasMeatballFlag)
            {
                snapshot.Status = "MeatballFlag";
                snapshot.Summary = "Meatball flag | enter pits for required service";
            }
            else if (snapshot.HasOptionalRepairs)
            {
                snapshot.Status = "OptionalRepairs";
                snapshot.Summary = string.Format("Optional repairs {0:0.0}s", snapshot.OptionalRepairSeconds);
            }
            else if (snapshot.HasSuspectedDamage)
            {
                snapshot.Status = "DamageSuspected";
                snapshot.Summary = "New incident detected | assess vehicle behavior";
            }
            else if (snapshot.HasBlackFlag)
            {
                snapshot.Status = "BlackFlag";
                snapshot.Summary = "Black flag active";
            }
            else
            {
                snapshot.Status = "Clear";
                snapshot.Summary = "No repair telemetry detected";
            }
        }

        private static void UpdateDiagnostics(DamageSnapshot snapshot)
        {
            if (snapshot.HasMeatballFlag && !snapshot.HasRequiredRepairs && !snapshot.HasOptionalRepairs)
            {
                snapshot.DiagnosticSummary = "Repair flag detected; repair times may remain zero until the car reaches its pit stall";
            }
            else if (snapshot.HasRequiredRepairs || snapshot.HasOptionalRepairs)
            {
                snapshot.DiagnosticSummary = "Repair time telemetry is active";
            }
            else if (!snapshot.RequiredRepairTelemetryFound || !snapshot.OptionalRepairTelemetryFound)
            {
                snapshot.DiagnosticSummary = "One or more repair-time keys are not exposed in this session/car combination";
            }
            else
            {
                snapshot.DiagnosticSummary = "Damage-related telemetry keys are available; no active repair condition detected";
            }
        }

        private void UpdateEvent(DamageSnapshot snapshot)
        {
            string eventName = "None";
            if (snapshot.FastRepairUsed && !previousFastRepairUsed) eventName = "FastRepairUsed";
            else if (snapshot.IsTowing && !previousTow) eventName = "TowStarted";
            else if (snapshot.IsRepairing && !previousRepairing) eventName = "RepairStarted";
            else if (snapshot.HasMeatballFlag && !previousMeatballFlag) eventName = "MeatballFlag";
            else if (snapshot.HasConfirmedDamage && !previousConfirmedDamage) eventName = "DamageConfirmed";
            else if (snapshot.IncidentDelta > 0) eventName = "IncidentDetected";

            if (eventName != "None") eventSequence++;
            snapshot.EventName = eventName;
            snapshot.EventSequence = eventSequence;
        }
    }
}
