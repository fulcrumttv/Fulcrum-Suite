using System;
using System.Text;
using Fulcrum.Core.Damage;
using Fulcrum.Core.Fuel;
using Fulcrum.Core.PitWindow;
using Fulcrum.Core.Spotter;
using Fulcrum.Core.Strategy;

namespace Fulcrum.Core.Events
{
    /// <summary>
    /// Combines event-producing modules into one stable event stream.
    /// </summary>
    public sealed class EventHubEngine
    {
        private int lastSpotterSequence;
        private int lastDamageSequence;
        private int lastStrategySequence;
        private int lastPitWindowSequence;
        private bool previousFuelCritical;
        private bool previousFuelShort;
        private bool initialized;

        public void Update(
            bool gameRunning,
            SpotterSnapshot spotter,
            DamageSnapshot damage,
            FuelSnapshot fuel,
            StrategySnapshot strategy,
            PitWindowSnapshot pitWindow,
            EventHubSnapshot output)
        {
            if (output == null)
            {
                return;
            }

            if (!gameRunning)
            {
                Reset(output);
                return;
            }

            output.Ready = true;
            UpdateActiveAlerts(spotter, damage, fuel, strategy, pitWindow, output);

            if (!initialized)
            {
                CaptureCurrentState(spotter, damage, fuel, strategy, pitWindow);
                initialized = true;
                return;
            }

            EventCandidate best = new EventCandidate();

            ConsiderSpotter(spotter, ref best);
            ConsiderDamage(damage, ref best);
            ConsiderFuel(fuel, ref best);
            ConsiderPitWindow(pitWindow, ref best);
            ConsiderStrategy(strategy, ref best);

            CaptureCurrentState(spotter, damage, fuel, strategy, pitWindow);

            if (best.HasValue)
            {
                PublishCandidate(best, output);
            }
        }

        public void Reset(EventHubSnapshot output)
        {
            initialized = false;
            lastSpotterSequence = 0;
            lastDamageSequence = 0;
            lastStrategySequence = 0;
            lastPitWindowSequence = 0;
            previousFuelCritical = false;
            previousFuelShort = false;

            if (output != null)
            {
                output.Reset();
            }
        }

        private void ConsiderSpotter(SpotterSnapshot spotter, ref EventCandidate best)
        {
            if (spotter == null || spotter.EventSequence == lastSpotterSequence ||
                string.IsNullOrEmpty(spotter.EventName) || spotter.EventName == "None")
            {
                return;
            }

            Consider(new EventCandidate
            {
                HasValue = true,
                Name = spotter.EventName,
                Category = "Spotter",
                Message = string.IsNullOrEmpty(spotter.Callout) ? spotter.State : spotter.Callout,
                Action = spotter.SuggestedAction,
                Priority = Math.Max(spotter.Priority, 50),
                IsUrgent = spotter.IsUrgent
            }, ref best);
        }

        private void ConsiderDamage(DamageSnapshot damage, ref EventCandidate best)
        {
            if (damage == null || damage.EventSequence == lastDamageSequence ||
                string.IsNullOrEmpty(damage.EventName) || damage.EventName == "None")
            {
                return;
            }

            int priority = 70;
            if (damage.HasMeatballFlag || damage.IsTowing)
            {
                priority = 95;
            }
            else if (damage.HasConfirmedDamage || damage.IsRepairing)
            {
                priority = 85;
            }

            Consider(new EventCandidate
            {
                HasValue = true,
                Name = damage.EventName,
                Category = "Damage",
                Message = damage.Summary,
                Action = damage.HasMeatballFlag ? "Pit for repairs" : "Assess vehicle condition",
                Priority = priority,
                IsUrgent = priority >= 90
            }, ref best);
        }

        private void ConsiderFuel(FuelSnapshot fuel, ref EventCandidate best)
        {
            if (fuel == null)
            {
                return;
            }

            if (fuel.IsFuelCritical && !previousFuelCritical)
            {
                Consider(new EventCandidate
                {
                    HasValue = true,
                    Name = "FuelCritical",
                    Category = "Fuel",
                    Message = fuel.Summary,
                    Action = "Pit or save fuel immediately",
                    Priority = 92,
                    IsUrgent = true
                }, ref best);
            }
            else if (fuel.IsFuelShort && !previousFuelShort)
            {
                Consider(new EventCandidate
                {
                    HasValue = true,
                    Name = "FuelShort",
                    Category = "Fuel",
                    Message = fuel.Summary,
                    Action = "Prepare fuel strategy",
                    Priority = 72,
                    IsUrgent = false
                }, ref best);
            }
            else if (!fuel.IsFuelShort && previousFuelShort)
            {
                Consider(new EventCandidate
                {
                    HasValue = true,
                    Name = "FuelRecovered",
                    Category = "Fuel",
                    Message = "Fuel estimate is sufficient",
                    Action = "Maintain planned pace",
                    Priority = 35,
                    IsUrgent = false
                }, ref best);
            }
        }

        private void ConsiderPitWindow(PitWindowSnapshot pitWindow, ref EventCandidate best)
        {
            if (pitWindow == null || pitWindow.EventSequence == lastPitWindowSequence ||
                string.IsNullOrEmpty(pitWindow.EventName) || pitWindow.EventName == "None")
            {
                return;
            }

            int priority = pitWindow.MustPitThisLap ? 90 : 60;

            Consider(new EventCandidate
            {
                HasValue = true,
                Name = pitWindow.EventName,
                Category = "PitWindow",
                Message = pitWindow.Summary,
                Action = pitWindow.Recommendation,
                Priority = priority,
                IsUrgent = pitWindow.MustPitThisLap
            }, ref best);
        }

        private void ConsiderStrategy(StrategySnapshot strategy, ref EventCandidate best)
        {
            if (strategy == null || strategy.EventSequence == lastStrategySequence ||
                string.IsNullOrEmpty(strategy.EventName) || strategy.EventName == "None")
            {
                return;
            }

            Consider(new EventCandidate
            {
                HasValue = true,
                Name = strategy.EventName,
                Category = "Strategy",
                Message = strategy.Summary,
                Action = strategy.Recommendation,
                Priority = Math.Max(40, Math.Min(89, strategy.RiskScore)),
                IsUrgent = strategy.RiskScore >= 80
            }, ref best);
        }

        private static void Consider(EventCandidate candidate, ref EventCandidate best)
        {
            if (!candidate.HasValue)
            {
                return;
            }

            if (!best.HasValue || candidate.Priority > best.Priority)
            {
                best = candidate;
            }
        }

        private static void PublishCandidate(EventCandidate candidate, EventHubSnapshot output)
        {
            output.LastEventName = candidate.Name ?? "None";
            output.Category = candidate.Category ?? "None";
            output.Message = candidate.Message ?? string.Empty;
            output.SuggestedAction = candidate.Action ?? "Maintain pace";
            output.Priority = candidate.Priority;
            output.IsUrgent = candidate.IsUrgent;
            output.Sequence++;
            output.OccurredAtUtc = DateTime.UtcNow;
        }

        private static void UpdateActiveAlerts(
            SpotterSnapshot spotter,
            DamageSnapshot damage,
            FuelSnapshot fuel,
            StrategySnapshot strategy,
            PitWindowSnapshot pitWindow,
            EventHubSnapshot output)
        {
            int count = 0;
            int highest = 0;
            StringBuilder alerts = new StringBuilder();

            AddAlert(spotter != null && spotter.HasActiveCallout, "Spotter", spotter != null ? spotter.Priority : 0, ref count, ref highest, alerts);
            AddAlert(damage != null && (damage.HasMeatballFlag || damage.HasConfirmedDamage || damage.IsTowing), "Damage", 90, ref count, ref highest, alerts);
            AddAlert(fuel != null && fuel.IsFuelCritical, "FuelCritical", 92, ref count, ref highest, alerts);
            AddAlert(fuel != null && !fuel.IsFuelCritical && fuel.IsFuelShort, "FuelShort", 72, ref count, ref highest, alerts);
            AddAlert(pitWindow != null && pitWindow.MustPitThisLap, "PitNow", 90, ref count, ref highest, alerts);
            AddAlert(strategy != null && strategy.DefenseRequired, "Defense", 65, ref count, ref highest, alerts);

            output.ActiveAlertCount = count;
            output.HighestActivePriority = highest;
            output.ActiveAlerts = alerts.ToString();
        }

        private static void AddAlert(
            bool active,
            string name,
            int priority,
            ref int count,
            ref int highest,
            StringBuilder alerts)
        {
            if (!active)
            {
                return;
            }

            if (alerts.Length > 0)
            {
                alerts.Append(",");
            }

            alerts.Append(name);
            count++;
            highest = Math.Max(highest, priority);
        }

        private void CaptureCurrentState(
            SpotterSnapshot spotter,
            DamageSnapshot damage,
            FuelSnapshot fuel,
            StrategySnapshot strategy,
            PitWindowSnapshot pitWindow)
        {
            lastSpotterSequence = spotter != null ? spotter.EventSequence : 0;
            lastDamageSequence = damage != null ? damage.EventSequence : 0;
            lastStrategySequence = strategy != null ? strategy.EventSequence : 0;
            lastPitWindowSequence = pitWindow != null ? pitWindow.EventSequence : 0;
            previousFuelCritical = fuel != null && fuel.IsFuelCritical;
            previousFuelShort = fuel != null && fuel.IsFuelShort;
        }

        private struct EventCandidate
        {
            public bool HasValue;
            public string Name;
            public string Category;
            public string Message;
            public string Action;
            public int Priority;
            public bool IsUrgent;
        }
    }
}
