using System;
using Fulcrum.Core.Fuel;
using Fulcrum.Core.Intelligence;
using Fulcrum.Core.Relative.Display;
using Fulcrum.Core.Telemetry;

namespace Fulcrum.Core.Strategy
{
    public sealed class StrategyEngine
    {
        private string previousRecommendation;
        private string previousRiskLevel;
        private int eventSequence;

        public StrategyEngine()
        {
            previousRecommendation = string.Empty;
            previousRiskLevel = string.Empty;
        }

        public void Reset(StrategySnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            eventSequence = 0;
            previousRecommendation = string.Empty;
            previousRiskLevel = string.Empty;
            snapshot.Reset();
            snapshot.EventSequence = eventSequence;
        }

        public void Update(
            TelemetrySnapshot telemetry,
            FuelSnapshot fuel,
            RaceIntelligenceSnapshot intelligence,
            RelativeDisplaySnapshot relative,
            StrategySnapshot snapshot)
        {
            if (telemetry == null) throw new ArgumentNullException(nameof(telemetry));
            if (fuel == null) throw new ArgumentNullException(nameof(fuel));
            if (intelligence == null) throw new ArgumentNullException(nameof(intelligence));
            if (relative == null) throw new ArgumentNullException(nameof(relative));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            snapshot.UpdatedAtUtc = DateTime.UtcNow;
            snapshot.Ready = telemetry.GameRunning && fuel.Ready;
            snapshot.TrafficAhead = HasRelevantTrafficAhead(relative);
            snapshot.FastClassIncoming = intelligence.FasterClassApproaching;
            snapshot.CleanAir = !snapshot.TrafficAhead && !snapshot.FastClassIncoming;
            snapshot.AttackAvailable = intelligence.HasAttackOpportunity;
            snapshot.DefenseRequired = intelligence.DefenseRequired;

            UpdateFuelPlan(telemetry, fuel, snapshot);
            UpdateRisk(fuel, intelligence, snapshot);
            UpdateRecommendation(telemetry, fuel, intelligence, snapshot);
            UpdateStatus(snapshot);
            UpdateEvent(snapshot);
        }

        private static void UpdateFuelPlan(
            TelemetrySnapshot telemetry,
            FuelSnapshot fuel,
            StrategySnapshot snapshot)
        {
            snapshot.TargetFuelLiters = fuel.HasFinishEstimate
                ? Math.Max(0.0, fuel.FuelRequiredToFinishLiters)
                : 0.0;

            snapshot.FuelMarginLiters = fuel.HasFinishEstimate
                ? fuel.FuelLevelLiters - fuel.FuelRequiredToFinishLiters
                : 0.0;

            snapshot.FuelMarginLaps = fuel.AverageUsageLiters > 0.01
                ? snapshot.FuelMarginLiters / fuel.AverageUsageLiters
                : 0.0;

            snapshot.CanFinish = fuel.HasFinishEstimate && !fuel.IsFuelShort;
            snapshot.NeedSplash = fuel.HasFinishEstimate &&
                                  fuel.IsFuelShort &&
                                  fuel.FuelToAddLiters > 0.05 &&
                                  fuel.FuelToAddLiters <= Math.Max(0.5, fuel.AverageUsageLiters * 2.25);

            int currentLap = Math.Max(0, telemetry.LapCompleted);
            snapshot.PitWindowOpenLap = currentLap;
            snapshot.PitWindowCloseLap = currentLap;
            snapshot.PitWindowIsOpen = false;
            snapshot.LapsUntilPitWindowOpen = 0;
            snapshot.LapsUntilPitWindowClose = 0;
            snapshot.MustPitThisLap = false;

            if (!fuel.HasFinishEstimate || fuel.AverageUsageLiters <= 0.01)
            {
                return;
            }

            double maxUsableFuel = fuel.FuelCapacityLiters > 0.1
                ? fuel.FuelCapacityLiters
                : fuel.FuelLevelLiters;

            double fullTankSafeLaps = Math.Max(
                0.0,
                (maxUsableFuel / fuel.AverageUsageLiters) - 1.0);

            double currentSafeLaps = Math.Max(
                0.0,
                (fuel.FuelLevelLiters / fuel.AverageUsageLiters) - 1.0);

            double remaining = Math.Max(0.0, fuel.EstimatedSessionLapsRemaining);
            int openOffset = Math.Max(0, (int)Math.Ceiling(remaining - fullTankSafeLaps));
            int closeOffset = Math.Max(0, (int)Math.Floor(currentSafeLaps));

            snapshot.PitWindowOpenLap = currentLap + openOffset;
            snapshot.PitWindowCloseLap = currentLap + closeOffset;
            snapshot.LapsUntilPitWindowOpen = openOffset;
            snapshot.LapsUntilPitWindowClose = closeOffset;
            snapshot.PitWindowIsOpen = openOffset == 0 && closeOffset >= 0;
            snapshot.MustPitThisLap = fuel.IsFuelCritical ||
                                      (fuel.IsFuelShort && closeOffset <= 0);
        }

        private static void UpdateRisk(
            FuelSnapshot fuel,
            RaceIntelligenceSnapshot intelligence,
            StrategySnapshot snapshot)
        {
            int score = 0;

            if (fuel.IsFuelCritical) score += 55;
            else if (fuel.IsFuelShort) score += 30;

            if (intelligence.ThreatLevel == "Critical") score += 40;
            else if (intelligence.ThreatLevel == "Danger") score += 25;
            else if (intelligence.ThreatLevel == "Caution") score += 10;

            if (snapshot.FastClassIncoming) score += 15;
            if (snapshot.DefenseRequired) score += 10;
            if (snapshot.MustPitThisLap) score += 20;

            snapshot.RiskScore = Math.Min(100, score);
            snapshot.RiskLevel = snapshot.RiskScore >= 75
                ? "Critical"
                : snapshot.RiskScore >= 45
                    ? "High"
                    : snapshot.RiskScore >= 20
                        ? "Medium"
                        : "Low";
        }

        private static void UpdateRecommendation(
            TelemetrySnapshot telemetry,
            FuelSnapshot fuel,
            RaceIntelligenceSnapshot intelligence,
            StrategySnapshot snapshot)
        {
            if (!snapshot.Ready)
            {
                snapshot.Recommendation = "Monitor";
                snapshot.RecommendationReason = "Collecting valid fuel and session data";
                return;
            }

            if (!IsRaceSession(telemetry))
            {
                snapshot.Recommendation = "Collect Data";
                snapshot.RecommendationReason = "Complete clean laps to improve the strategy estimate";
                return;
            }

            if (snapshot.MustPitThisLap)
            {
                snapshot.Recommendation = "Pit This Lap";
                snapshot.RecommendationReason = fuel.IsFuelCritical
                    ? "Fuel is at a critical level"
                    : "This is the latest safe fuel lap";
                return;
            }

            if (fuel.IsFuelShort)
            {
                if (snapshot.PitWindowIsOpen)
                {
                    snapshot.Recommendation = snapshot.NeedSplash ? "Splash And Go" : "Pit Soon";
                    snapshot.RecommendationReason = "The fuel window is open and current fuel cannot reach the finish";
                }
                else
                {
                    snapshot.Recommendation = "Save Fuel";
                    snapshot.RecommendationReason = "Current fuel is short and the optimal pit window is not open yet";
                }
                return;
            }

            if (snapshot.FastClassIncoming)
            {
                snapshot.Recommendation = "Hold Line";
                snapshot.RecommendationReason = "Faster-class traffic is approaching";
                return;
            }

            if (snapshot.DefenseRequired)
            {
                snapshot.Recommendation = "Prepare To Defend";
                snapshot.RecommendationReason = "A car behind is closing and presents a threat";
                return;
            }

            if (snapshot.AttackAvailable)
            {
                snapshot.Recommendation = "Push";
                snapshot.RecommendationReason = "The car ahead is within an attack opportunity";
                return;
            }

            if (snapshot.CleanAir)
            {
                snapshot.Recommendation = "Maintain Pace";
                snapshot.RecommendationReason = "Fuel is sufficient and no immediate traffic action is required";
                return;
            }

            snapshot.Recommendation = "Manage Traffic";
            snapshot.RecommendationReason = intelligence.SuggestedAction ?? "Nearby traffic requires attention";
        }

        private static void UpdateStatus(StrategySnapshot snapshot)
        {
            if (!snapshot.Ready)
            {
                snapshot.Status = "Collecting";
                snapshot.Summary = "Waiting for valid strategy inputs";
                return;
            }

            snapshot.Status = snapshot.MustPitThisLap
                ? "PitNow"
                : snapshot.PitWindowIsOpen && !snapshot.CanFinish
                    ? "PitWindowOpen"
                    : snapshot.CanFinish
                        ? "CanFinish"
                        : "FuelShort";

            if (snapshot.CanFinish)
            {
                snapshot.Summary = string.Format(
                    "Can finish | margin {0:+0.0;-0.0;0.0} L | {1}",
                    snapshot.FuelMarginLiters,
                    snapshot.Recommendation);
            }
            else if (snapshot.NeedSplash)
            {
                snapshot.Summary = string.Format(
                    "Splash required | target {0:0.0} L | {1}",
                    snapshot.TargetFuelLiters,
                    snapshot.Recommendation);
            }
            else
            {
                snapshot.Summary = string.Format(
                    "Fuel short {0:0.0} L | window L{1}-L{2} | {3}",
                    Math.Abs(Math.Min(0.0, snapshot.FuelMarginLiters)),
                    snapshot.PitWindowOpenLap,
                    snapshot.PitWindowCloseLap,
                    snapshot.Recommendation);
            }
        }

        private void UpdateEvent(StrategySnapshot snapshot)
        {
            string eventName = "None";

            if (snapshot.MustPitThisLap)
            {
                eventName = "PitRequired";
            }
            else if (snapshot.RiskLevel != previousRiskLevel && snapshot.RiskLevel == "Critical")
            {
                eventName = "RiskCritical";
            }
            else if (snapshot.Recommendation != previousRecommendation)
            {
                eventName = "RecommendationChanged";
            }

            if (eventName != "None")
            {
                eventSequence++;
            }

            snapshot.EventName = eventName;
            snapshot.EventSequence = eventSequence;
            previousRecommendation = snapshot.Recommendation;
            previousRiskLevel = snapshot.RiskLevel;
        }

        private static bool HasRelevantTrafficAhead(RelativeDisplaySnapshot relative)
        {
            RelativeDisplayEntry ahead = relative.GetAhead(0);
            return ahead.HasData && !ahead.IsInPits && ahead.HasGap && Math.Abs(ahead.GapSeconds) <= 5.0f;
        }

        private static bool IsRaceSession(TelemetrySnapshot telemetry)
        {
            string sessionType = telemetry.SessionType ?? string.Empty;
            return sessionType.IndexOf("race", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   telemetry.SessionState == 4;
        }
    }
}
