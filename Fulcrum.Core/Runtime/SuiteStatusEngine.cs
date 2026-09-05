using System;
using Fulcrum.Core.Damage;
using Fulcrum.Core.Delta;
using Fulcrum.Core.Fuel;
using Fulcrum.Core.PitWindow;
using Fulcrum.Core.Radar;
using Fulcrum.Core.Relative.Display;
using Fulcrum.Core.Spotter;
using Fulcrum.Core.Standings;
using Fulcrum.Core.Strategy;

namespace Fulcrum.Core.Runtime
{
    public sealed class SuiteStatusEngine
    {
        public void Update(
            bool gameRunning,
            RelativeDisplaySnapshot relative,
            RadarSnapshot radar,
            FuelSnapshot fuel,
            DamageSnapshot damage,
            DeltaSnapshot delta,
            SpotterSnapshot spotter,
            PitWindowSnapshot pitWindow,
            StrategySnapshot strategy,
            StandingsSnapshot standings,
            SuiteStatusSnapshot output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            output.Reset();

            if (!gameRunning)
            {
                return;
            }

            output.RelativeReady = relative != null && relative.Player != null && relative.Player.HasData;
            output.RadarReady = radar != null && !string.Equals(radar.State, "Off", StringComparison.OrdinalIgnoreCase);
            output.FuelReady = fuel != null && fuel.Ready;
            output.DamageReady = damage != null && damage.Ready;
            output.DeltaReady = delta != null && delta.Ready;
            output.SpotterReady = spotter != null && spotter.Ready;
            output.PitWindowReady = pitWindow != null && pitWindow.Ready;
            output.StrategyReady = strategy != null && strategy.Ready;
            output.StandingsReady = standings != null && standings.Ready;

            output.RelativeStatus = output.RelativeReady ? "Ready" : "Waiting for player data";
            output.RadarStatus = output.RadarReady ? "Ready" : "Waiting for on-track radar";
            output.FuelStatus = output.FuelReady ? "Ready" : GetFuelStatus(fuel);
            output.DamageStatus = output.DamageReady ? "Ready" : "Waiting for damage telemetry";
            output.DeltaStatus = output.DeltaReady ? "Ready" : GetDeltaStatus(delta);
            output.SpotterStatus = output.SpotterReady ? "Ready" : "Waiting for radar/session data";
            output.PitWindowStatus = output.PitWindowReady ? "Ready" : GetPitWindowStatus(pitWindow);
            output.StrategyStatus = output.StrategyReady ? "Ready" : GetStrategyStatus(strategy);
            output.StandingsStatus = output.StandingsReady ? "Ready" : "Waiting for valid standings";

            int readyCount = 0;
            if (output.RelativeReady) readyCount++;
            if (output.RadarReady) readyCount++;
            if (output.FuelReady) readyCount++;
            if (output.DamageReady) readyCount++;
            if (output.DeltaReady) readyCount++;
            if (output.SpotterReady) readyCount++;
            if (output.PitWindowReady) readyCount++;
            if (output.StrategyReady) readyCount++;
            if (output.StandingsReady) readyCount++;

            output.ReadyModuleCount = readyCount;
            output.MissingModules = BuildMissingModules(output);
            output.Ready = output.RelativeReady && output.RadarReady && output.DamageReady && output.SpotterReady;

            if (output.Ready)
            {
                output.Health = readyCount >= 8 ? "Excellent" : readyCount >= 6 ? "Good" : "Partial";
            }
            else
            {
                output.Health = readyCount >= 4 ? "Starting" : "Limited";
            }

            output.Summary = readyCount.ToString() + "/" + output.TotalModuleCount.ToString() + " modules ready";
            output.CoreStatus = output.Ready
                ? "Core real-time modules ready"
                : "Waiting for Relative, Radar, Damage and Spotter";

            SelectPrimaryAlert(fuel, damage, spotter, pitWindow, strategy, output);
            output.UpdatedAtUtc = DateTime.UtcNow;
        }

        public void Reset(SuiteStatusSnapshot output)
        {
            if (output != null)
            {
                output.Reset();
            }
        }

        private static string BuildMissingModules(SuiteStatusSnapshot output)
        {
            string value = string.Empty;
            AppendMissing(ref value, "Relative", output.RelativeReady);
            AppendMissing(ref value, "Radar", output.RadarReady);
            AppendMissing(ref value, "Fuel", output.FuelReady);
            AppendMissing(ref value, "Damage", output.DamageReady);
            AppendMissing(ref value, "Delta", output.DeltaReady);
            AppendMissing(ref value, "Spotter", output.SpotterReady);
            AppendMissing(ref value, "PitWindow", output.PitWindowReady);
            AppendMissing(ref value, "Strategy", output.StrategyReady);
            AppendMissing(ref value, "Standings", output.StandingsReady);
            return string.IsNullOrEmpty(value) ? "None" : value;
        }

        private static void AppendMissing(ref string value, string name, bool ready)
        {
            if (ready) return;
            if (!string.IsNullOrEmpty(value)) value += ",";
            value += name;
        }

        private static string GetFuelStatus(FuelSnapshot fuel)
        {
            if (fuel == null) return "Unavailable";
            if (!string.IsNullOrEmpty(fuel.Status)) return fuel.Status;
            return "Collecting valid lap samples";
        }

        private static string GetDeltaStatus(DeltaSnapshot delta)
        {
            if (delta == null) return "Unavailable";
            return delta.IsValid ? "Ready" : "Waiting for valid reference lap";
        }

        private static string GetPitWindowStatus(PitWindowSnapshot pitWindow)
        {
            if (pitWindow == null) return "Unavailable";
            if (!string.IsNullOrEmpty(pitWindow.Status)) return pitWindow.Status;
            return "Waiting for fuel estimate";
        }

        private static string GetStrategyStatus(StrategySnapshot strategy)
        {
            if (strategy == null) return "Unavailable";
            if (!string.IsNullOrEmpty(strategy.Status)) return strategy.Status;
            return "Waiting for strategy inputs";
        }

        private static void SelectPrimaryAlert(
            FuelSnapshot fuel,
            DamageSnapshot damage,
            SpotterSnapshot spotter,
            PitWindowSnapshot pitWindow,
            StrategySnapshot strategy,
            SuiteStatusSnapshot output)
        {
            if (spotter != null && spotter.IsUrgent && !string.IsNullOrEmpty(spotter.Callout))
            {
                SetAlert(output, spotter.Callout, spotter.Priority > 0 ? spotter.Priority : 100, spotter.SuggestedAction);
                return;
            }

            if (damage != null && damage.IsDisqualified)
            {
                SetAlert(output, "DISQUALIFIED", 100, "Return to pits");
                return;
            }

            if (damage != null && damage.HasMeatballFlag)
            {
                SetAlert(output, "REPAIR FLAG", 95, "Pit for repairs");
                return;
            }

            if (damage != null && damage.HasBlackFlag)
            {
                SetAlert(output, "BLACK FLAG", 94, "Serve penalty");
                return;
            }

            if (fuel != null && fuel.IsFuelCritical)
            {
                SetAlert(output, "FUEL CRITICAL", 90, "Pit this lap");
                return;
            }

            if (pitWindow != null && pitWindow.MustPitThisLap)
            {
                SetAlert(output, "PIT THIS LAP", 85, pitWindow.Recommendation);
                return;
            }

            if (strategy != null && string.Equals(strategy.RiskLevel, "Critical", StringComparison.OrdinalIgnoreCase))
            {
                SetAlert(output, "CRITICAL RISK", 80, strategy.Recommendation);
                return;
            }

            if (spotter != null && spotter.HasActiveCallout && !string.IsNullOrEmpty(spotter.Callout))
            {
                SetAlert(output, spotter.Callout, spotter.Priority, spotter.SuggestedAction);
                return;
            }

            if (fuel != null && fuel.IsFuelShort)
            {
                SetAlert(output, "FUEL SHORT", 60, "Save fuel or plan a stop");
                return;
            }

            if (strategy != null && strategy.Ready)
            {
                SetAlert(output, "None", 0, strategy.Recommendation);
                return;
            }

            SetAlert(output, "None", 0, "Maintain pace");
        }

        private static void SetAlert(SuiteStatusSnapshot output, string alert, int priority, string action)
        {
            output.PrimaryAlert = string.IsNullOrEmpty(alert) ? "None" : alert;
            output.PrimaryAlertPriority = priority;
            output.PrimaryAction = string.IsNullOrEmpty(action) ? "Maintain pace" : action;
        }
    }
}
