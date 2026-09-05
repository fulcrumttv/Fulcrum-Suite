using System;
using System.Collections.Generic;
using System.Globalization;
using Fulcrum.Core.Telemetry;

namespace Fulcrum.Core.Fuel
{
    public sealed class FuelCalculator
    {
        private const int MaximumSamples = 30;
        private const int RecentSampleCount = 5;
        private const int UnlimitedLapSentinelThreshold = 30000;
        private const double MinimumValidLapUsage = 0.03;
        private const double MaximumValidLapUsage = 45.0;
        private const double MinimumValidLapTime = 15.0;
        private const double MaximumValidLapTime = 2400.0;
        private const double LowSpeedThresholdMetersPerSecond = 1.0;
        private const double MaximumStationarySecondsPerLap = 30.0;
        private const double RefuelThresholdLiters = 0.15;
        private const double DisplayReserveLaps = 0.30;
        private const double FinishReserveLaps = 0.50;

        private readonly List<double> usageSamples = new List<double>();
        private readonly List<double> lapTimeSamples = new List<double>();

        private int previousCompletedLap;
        private int previousSessionNumber;
        private double fuelAtLapStart;
        private double sessionTimeAtLapStart;
        private double previousUpdateSessionTime;
        private double previousFuelLiters;
        private double stationarySecondsThisLap;
        private float previousLapDistancePercent;
        private double lastLapCompletionSessionTime;
        private bool lapTouchedPitRoad;
        private bool refuelDuringLap;
        private bool initialized;

        public void Reset(FuelSnapshot snapshot)
        {
            usageSamples.Clear();
            lapTimeSamples.Clear();
            previousCompletedLap = 0;
            previousSessionNumber = -1;
            fuelAtLapStart = 0.0;
            sessionTimeAtLapStart = 0.0;
            previousUpdateSessionTime = 0.0;
            previousFuelLiters = 0.0;
            stationarySecondsThisLap = 0.0;
            previousLapDistancePercent = 0.0f;
            lastLapCompletionSessionTime = -1000.0;
            lapTouchedPitRoad = false;
            refuelDuringLap = false;
            initialized = false;
            ClearSnapshot(snapshot);
        }

        public void Update(TelemetrySnapshot telemetry, double fuelLiters, double fuelPercent, double capacityLiters, FuelSnapshot snapshot)
        {
            if (telemetry == null || snapshot == null) return;

            snapshot.RefuelDetected = false;
            snapshot.LastSampleRejected = false;
            snapshot.LastSampleRejectReason = string.Empty;
            snapshot.Error = string.Empty;
            snapshot.FuelLevelLiters = Math.Max(0.0, fuelLiters);
            snapshot.FuelLevelPercent = Clamp01(fuelPercent);
            snapshot.FuelCapacityLiters = Math.Max(capacityLiters, fuelLiters);
            snapshot.InstantDataReady = snapshot.FuelLevelLiters >= 0.0 && snapshot.FuelCapacityLiters > 0.0;
            snapshot.UpdatedAtUtc = DateTime.UtcNow;

            if (!initialized)
            {
                InitializeLapState(telemetry, fuelLiters);
            }

            if (telemetry.SessionNumber != previousSessionNumber ||
                telemetry.LapCompleted < previousCompletedLap ||
                telemetry.SessionTime < previousUpdateSessionTime)
            {
                usageSamples.Clear();
                lapTimeSamples.Clear();
                snapshot.RejectedSampleCount = 0;
                InitializeLapState(telemetry, fuelLiters);
            }

            TrackLapValidity(telemetry);

            if (fuelLiters > previousFuelLiters + RefuelThresholdLiters)
            {
                refuelDuringLap = true;
                snapshot.RefuelDetected = true;
            }

            // Some normalized SimHub frames expose current fuel correctly but do
            // not advance LapCompleted. Detect the start/finish crossing from the
            // circular lap-distance value as a fallback. This also keeps working
            // in sessions where LapCompleted is delayed by one or more frames.
            bool completedCounterAdvanced = telemetry.LapCompleted > previousCompletedLap;
            bool crossedStartFinish =
                previousLapDistancePercent > 0.80f &&
                telemetry.LapDistancePercent >= 0.0f &&
                telemetry.LapDistancePercent < 0.20f;

            bool duplicateDelayedCounter =
                completedCounterAdvanced &&
                !crossedStartFinish &&
                telemetry.SessionTime - lastLapCompletionSessionTime < 5.0;

            if (duplicateDelayedCounter)
            {
                // The normalized lap counter can arrive shortly after the distance
                // wrap. Synchronize it without recording the same lap twice.
                previousCompletedLap = telemetry.LapCompleted;
            }
            else if (completedCounterAdvanced || crossedStartFinish)
            {
                ProcessCompletedLap(telemetry, fuelLiters, snapshot);
                StartNewLap(telemetry, fuelLiters);
                lastLapCompletionSessionTime = telemetry.SessionTime;
            }

            previousLapDistancePercent = telemetry.LapDistancePercent;
            previousFuelLiters = fuelLiters;
            previousUpdateSessionTime = telemetry.SessionTime;
            CalculateOutputs(telemetry, snapshot);
        }

        private void InitializeLapState(TelemetrySnapshot telemetry, double fuelLiters)
        {
            initialized = true;
            previousCompletedLap = telemetry.LapCompleted;
            previousSessionNumber = telemetry.SessionNumber;
            fuelAtLapStart = fuelLiters;
            previousFuelLiters = fuelLiters;
            sessionTimeAtLapStart = telemetry.SessionTime;
            previousUpdateSessionTime = telemetry.SessionTime;
            previousLapDistancePercent = telemetry.LapDistancePercent;
            stationarySecondsThisLap = 0.0;
            lapTouchedPitRoad = telemetry.IsOnPitRoad;
            refuelDuringLap = false;
        }

        private void StartNewLap(TelemetrySnapshot telemetry, double fuelLiters)
        {
            previousCompletedLap = telemetry.LapCompleted;
            previousSessionNumber = telemetry.SessionNumber;
            fuelAtLapStart = fuelLiters;
            sessionTimeAtLapStart = telemetry.SessionTime;
            previousLapDistancePercent = telemetry.LapDistancePercent;
            stationarySecondsThisLap = 0.0;
            lapTouchedPitRoad = telemetry.IsOnPitRoad;
            refuelDuringLap = false;
        }

        private void TrackLapValidity(TelemetrySnapshot telemetry)
        {
            lapTouchedPitRoad = lapTouchedPitRoad || telemetry.IsOnPitRoad;

            double dt = telemetry.SessionTime - previousUpdateSessionTime;
            if (dt > 0.0 && dt < 2.0 && telemetry.SpeedMetersPerSecond < LowSpeedThresholdMetersPerSecond)
            {
                stationarySecondsThisLap += dt;
            }
        }

        private void ProcessCompletedLap(TelemetrySnapshot telemetry, double fuelLiters, FuelSnapshot snapshot)
        {
            double usage = fuelAtLapStart - fuelLiters;
            double lapTime = telemetry.SessionTime - sessionTimeAtLapStart;
            string reason = GetRejectReason(usage, lapTime);

            if (string.IsNullOrEmpty(reason))
            {
                AddSample(usageSamples, usage);
                AddSample(lapTimeSamples, lapTime);
                snapshot.LastLapUsageLiters = usage;
            }
            else
            {
                snapshot.LastSampleRejected = true;
                snapshot.LastSampleRejectReason = reason;
                snapshot.RejectedSampleCount++;
            }
        }

        private string GetRejectReason(double usage, double lapTime)
        {
            if (refuelDuringLap) return "REFUEL LAP";
            if (lapTouchedPitRoad) return "PIT / OUT LAP";
            if (stationarySecondsThisLap > MaximumStationarySecondsPerLap) return "STATIONARY LAP";
            if (usage < MinimumValidLapUsage || usage > MaximumValidLapUsage) return "INVALID FUEL SAMPLE";
            if (lapTime < MinimumValidLapTime || lapTime > MaximumValidLapTime) return "INVALID LAP TIME";
            return string.Empty;
        }

        private void CalculateOutputs(TelemetrySnapshot telemetry, FuelSnapshot snapshot)
        {
            snapshot.ValidSampleCount = usageSamples.Count;
            snapshot.AverageUsageLiters = TrimmedMean(usageSamples);
            snapshot.MedianUsageLiters = Percentile(usageSamples, 0.50);
            snapshot.RecentUsageLiters = RecentMedian(usageSamples, RecentSampleCount);
            snapshot.ConservativeUsageLiters = Percentile(usageSamples, 0.75);
            snapshot.UsageTrendLitersPerLap = LinearTrend(usageSamples, RecentSampleCount);
            snapshot.UsageTrendStatus = GetTrendStatus(snapshot.UsageTrendLitersPerLap);

            snapshot.BestUsageLiters = Minimum(usageSamples);
            snapshot.WorstUsageLiters = Maximum(usageSamples);
            snapshot.AverageLapTimeSeconds = TrimmedMean(lapTimeSamples);
            snapshot.MedianLapTimeSeconds = Percentile(lapTimeSamples, 0.50);
            snapshot.RecentLapTimeSeconds = RecentMedian(lapTimeSamples, 3);
            snapshot.ConservativeLapTimeSeconds = Percentile(lapTimeSamples, 0.75);
            double robustLapTime = Math.Max(snapshot.RecentLapTimeSeconds, snapshot.ConservativeLapTimeSeconds);
            if (snapshot.MedianLapTimeSeconds > 1.0)
            {
                robustLapTime = Math.Min(robustLapTime, snapshot.MedianLapTimeSeconds * 1.25);
            }
            snapshot.StrategyLapTimeSeconds = robustLapTime > 1.0
                ? robustLapTime
                : snapshot.MedianLapTimeSeconds;
            snapshot.LapTimeSource = snapshot.StrategyLapTimeSeconds > 1.0 ? "RECENT / P75" : "UNAVAILABLE";
            // Adaptive Fuel Model. A sustained shift between recent and stint-level
            // samples means the old regime is no longer fully representative
            // (for example a drying track, rain arrival, tyre change or pace change).
            double recentP75 = RecentPercentile(usageSamples, RecentSampleCount, 0.75);
            snapshot.RecentVsStintDeltaPercent = RelativeDeltaPercent(
                snapshot.RecentUsageLiters, snapshot.AverageUsageLiters);
            snapshot.RecentLapTimeDeltaPercent = RelativeDeltaPercent(
                RecentMedian(lapTimeSamples, 3), snapshot.MedianLapTimeSeconds);
            double normalizedTrendPercent = snapshot.AverageUsageLiters > 0.0
                ? Math.Abs(snapshot.UsageTrendLitersPerLap) / snapshot.AverageUsageLiters * 100.0
                : 0.0;
            snapshot.AdaptiveModelActive = usageSamples.Count >= 5 &&
                (Math.Abs(snapshot.RecentVsStintDeltaPercent) >= 2.5 ||
                 Math.Abs(snapshot.RecentLapTimeDeltaPercent) >= 3.5 ||
                 normalizedTrendPercent >= 0.8);
            snapshot.RelevantSampleCount = snapshot.AdaptiveModelActive
                ? Math.Min(RecentSampleCount, usageSamples.Count)
                : usageSamples.Count;
            snapshot.AdaptiveStrategyUsageLiters = snapshot.AdaptiveModelActive
                ? MaximumPositive(snapshot.RecentUsageLiters, recentP75)
                : MaximumPositive(snapshot.AverageUsageLiters, snapshot.RecentUsageLiters);
            snapshot.StrategyUsageLiters = snapshot.AdaptiveStrategyUsageLiters;
            snapshot.ModelStatusText = snapshot.AdaptiveModelActive ? "MODEL ADAPTING" : "MODEL STABLE";
            snapshot.ConditionsStatus = snapshot.AdaptiveModelActive
                ? "CHANGING CONDITIONS"
                : "STABLE CONDITIONS";
            snapshot.AverageBasisText = snapshot.AdaptiveModelActive
                ? snapshot.RelevantSampleCount.ToString(CultureInfo.InvariantCulture) + " RECENT / " + usageSamples.Count.ToString(CultureInfo.InvariantCulture) + " TOTAL"
                : usageSamples.Count.ToString(CultureInfo.InvariantCulture) + " VALID LAPS";
            snapshot.Ready = snapshot.ValidSampleCount > 0 && snapshot.StrategyUsageLiters > 0.0;
            SetConfidence(snapshot);
            ApplyAdaptiveConfidence(snapshot);
            snapshot.LearningProgressText = Math.Min(3, snapshot.ValidSampleCount).ToString(CultureInfo.InvariantCulture) + " / 3 VALID LAPS";
            if (!snapshot.InstantDataReady) snapshot.EngineState = "NO DATA";
            else if (snapshot.ValidSampleCount < 3) snapshot.EngineState = "LEARNING";
            else snapshot.EngineState = "READY";

            // Use the completed-lap counter as the canonical absolute race-lap reference.
            // Some SimHub/iRacing frames expose `Lap` with a session-relative or stale
            // value, which previously shifted the whole pit window many laps forward.
            snapshot.CurrentRaceLap = Math.Max(
                1,
                telemetry.LapCompleted >= 0
                    ? telemetry.LapCompleted + 1
                    : Math.Max(1, telemetry.Lap));

            double representativeLapTime = snapshot.StrategyLapTimeSeconds > 0.0
                ? snapshot.StrategyLapTimeSeconds
                : (snapshot.MedianLapTimeSeconds > 0.0
                    ? snapshot.MedianLapTimeSeconds
                    : snapshot.AverageLapTimeSeconds);

            SessionEstimate estimate = GetSessionEstimate(telemetry, representativeLapTime);
            snapshot.EstimatedSessionLapsRemaining = estimate.LapsRemaining;
            snapshot.HasFinishEstimate = estimate.IsAvailable;
            snapshot.IsTimedSession = estimate.IsTimed;
            snapshot.IsLapLimitedSession = estimate.IsLapLimited;
            snapshot.EstimateSource = estimate.Source;
            snapshot.EstimatedFinishLap = estimate.IsAvailable
                ? snapshot.CurrentRaceLap + Math.Max(0, (int)Math.Ceiling(estimate.LapsRemaining) - 1)
                : 0;

            if (!snapshot.Ready)
            {
                ClearCalculatedFuelOutputs(snapshot);
                snapshot.Status = snapshot.InstantDataReady ? "LEARNING" : "NO DATA";
                snapshot.Summary = snapshot.LastSampleRejected
                    ? snapshot.LastSampleRejectReason
                    : "COMPLETE A CLEAN LAP";
                return;
            }

            snapshot.FuelLapsRemaining = snapshot.FuelLevelLiters / snapshot.StrategyUsageLiters;
            snapshot.DisplayLapsRemaining = Math.Max(0.0, snapshot.FuelLapsRemaining - DisplayReserveLaps);
            snapshot.FuelTimeRemainingSeconds = representativeLapTime > 0.0
                ? snapshot.DisplayLapsRemaining * representativeLapTime
                : 0.0;
            snapshot.ReserveLiters = snapshot.StrategyUsageLiters * FinishReserveLaps;
            snapshot.IsFuelCritical = snapshot.FuelLapsRemaining < 1.25;

            int wholeLapsAvailable = Math.Max(0, (int)Math.Floor(snapshot.FuelLapsRemaining));
            double trackFraction = Clamp01(telemetry.LapDistancePercent);
            int achievableCrossings = Math.Max(0, (int)Math.Floor(trackFraction + snapshot.FuelLapsRemaining));
            double equivalentDistanceToExtraCrossing = Math.Max(0.05, (achievableCrossings + 1.0) - trackFraction);
            snapshot.ExtraLapTargetUsageLiters = Math.Round(snapshot.FuelLevelLiters / equivalentDistanceToExtraCrossing, 2);
            snapshot.SaveForExtraLapLiters = Math.Round(Math.Max(0.0, snapshot.StrategyUsageLiters - snapshot.ExtraLapTargetUsageLiters), 2);

            if (!snapshot.HasFinishEstimate)
            {
                snapshot.Status = snapshot.IsFuelCritical ? "FUEL CRITICAL" : "NO ESTIMATE";
                snapshot.Summary = snapshot.DisplayLapsRemaining.ToString("0.0", CultureInfo.InvariantCulture) + " LAPS AVAILABLE";
                ClearFinishAndPitOutputs(snapshot);
                return;
            }

            double required = snapshot.EstimatedSessionLapsRemaining * snapshot.StrategyUsageLiters + snapshot.ReserveLiters;
            if (required <= 0.0 || required > GetReasonableRequiredFuelUpperBound(snapshot))
            {
                snapshot.HasFinishEstimate = false;
                snapshot.EstimateSource = "REJECTED";
                snapshot.Status = "NO ESTIMATE";
                snapshot.Summary = "IMPLAUSIBLE FINISH ESTIMATE";
                snapshot.Error = "Fuel requirement exceeded safety bounds";
                ClearFinishAndPitOutputs(snapshot);
                return;
            }

            snapshot.FuelRequiredToFinishLiters = required;
            snapshot.FuelToAddLiters = Math.Max(0.0, required - snapshot.FuelLevelLiters);
            snapshot.FinishMarginLiters = snapshot.FuelLevelLiters - required;
            snapshot.IsFuelShort = snapshot.FinishMarginLiters < -0.05;
            snapshot.TargetUsageLiters = Math.Max(
                0.0,
                (snapshot.FuelLevelLiters - snapshot.ReserveLiters) /
                Math.Max(1.0, snapshot.EstimatedSessionLapsRemaining));
            snapshot.SavePerLapLiters = Math.Max(0.0, snapshot.StrategyUsageLiters - snapshot.TargetUsageLiters);
            snapshot.FillToLiters = snapshot.FuelCapacityLiters > 0.0
                ? Math.Min(snapshot.FuelCapacityLiters, snapshot.FuelLevelLiters + snapshot.FuelToAddLiters)
                : snapshot.FuelLevelLiters + snapshot.FuelToAddLiters;
            snapshot.LapsShort = snapshot.StrategyUsageLiters > 0.0
                ? snapshot.FuelToAddLiters / snapshot.StrategyUsageLiters
                : 0.0;
            snapshot.TanksNeeded = snapshot.FuelCapacityLiters > 0.0
                ? snapshot.FuelToAddLiters / snapshot.FuelCapacityLiters
                : 0.0;
            snapshot.PitStopsNeeded = snapshot.FuelToAddLiters <= 0.01
                ? 0
                : Math.Max(1, (int)Math.Ceiling(snapshot.TanksNeeded));
            snapshot.FuelStopsNeeded = snapshot.PitStopsNeeded;
            snapshot.TotalStopsRemaining = Math.Max(snapshot.FuelStopsNeeded, snapshot.MandatoryStopsRemaining);

            CalculatePitWindow(telemetry, snapshot);

            if (snapshot.IsFuelCritical && snapshot.IsFuelShort)
            {
                snapshot.Status = "FUEL CRITICAL";
                snapshot.Summary = "PIT THIS LAP";
            }
            else if (!snapshot.IsFuelShort)
            {
                snapshot.Status = "SAFE TO FINISH";
                snapshot.Summary = "+" + snapshot.FinishMarginLiters.ToString("0.0", CultureInfo.InvariantCulture) + " L AT FINISH";
            }
            else if (snapshot.PitWindowValid && snapshot.PitLatestLap <= snapshot.CurrentRaceLap)
            {
                snapshot.Status = "PIT THIS LAP";
                snapshot.Summary = "ADD " + snapshot.FuelToAddLiters.ToString("0.0", CultureInfo.InvariantCulture) + " L";
            }
            else
            {
                snapshot.Status = "FUEL NEEDED";
                snapshot.Summary = "ADD " + snapshot.FuelToAddLiters.ToString("0.0", CultureInfo.InvariantCulture) + " L";
            }
        }

        private static void CalculatePitWindow(TelemetrySnapshot telemetry, FuelSnapshot snapshot)
        {
            snapshot.PitWindowValid = false;
            snapshot.PitEarliestLap = 0;
            snapshot.PitOptimalLap = 0;
            snapshot.PitLatestLap = 0;
            snapshot.NextPitLap = 0;
            snapshot.PlannedStintsRemaining = 0;
            snapshot.CurrentStintTargetLaps = 0.0;
            snapshot.NextStintTargetLaps = 0.0;
            snapshot.NextStopFuelToAddLiters = 0.0;
            snapshot.TotalFuelDeficitLiters = snapshot.FuelToAddLiters;
            snapshot.StrategyPlanStatus = "NO PLAN";

            if (!snapshot.IsFuelShort ||
                snapshot.EstimatedFinishLap <= snapshot.CurrentRaceLap ||
                snapshot.StrategyUsageLiters <= 0.0 ||
                snapshot.FuelCapacityLiters <= 0.0)
            {
                snapshot.StrategyPlanStatus = snapshot.IsFuelShort ? "WAITING FOR PLAN" : "NO STOP REQUIRED";
                return;
            }

            double trackFraction = Clamp01(telemetry.LapDistancePercent);
            double raceDistanceRemaining = Math.Max(0.25, snapshot.EstimatedSessionLapsRemaining);
            double reserveFuel = Math.Max(snapshot.ReserveLiters, snapshot.StrategyUsageLiters * 0.15);
            double usableCurrentFuel = Math.Max(0.0, snapshot.FuelLevelLiters - reserveFuel);
            double currentPhysicalLaps = usableCurrentFuel / snapshot.StrategyUsageLiters;
            double usableFutureFuel = Math.Max(0.0, snapshot.FuelCapacityLiters - reserveFuel);
            double futureFullStintLaps = Math.Max(1.0, usableFutureFuel / snapshot.StrategyUsageLiters);

            // Minimum future stops required after accounting for the actual fuel
            // loaded in the current (possibly partial) opening stint.
            double distanceAfterCurrentTank = Math.Max(0.0, raceDistanceRemaining - currentPhysicalLaps);
            int fuelStops = distanceAfterCurrentTank <= 0.01
                ? 0
                : Math.Max(1, (int)Math.Ceiling(distanceAfterCurrentTank / futureFullStintLaps));
            fuelStops = Math.Max(fuelStops, snapshot.MandatoryStopsRemaining);
            snapshot.PitStopsNeeded = fuelStops;
            snapshot.FuelStopsNeeded = Math.Max(0, (int)Math.Ceiling(distanceAfterCurrentTank / futureFullStintLaps));
            snapshot.TotalStopsRemaining = Math.Max(snapshot.FuelStopsNeeded, snapshot.MandatoryStopsRemaining);
            snapshot.PlannedStintsRemaining = fuelStops + 1;

            // A race-first balanced target. The opening stint can be partial, but
            // it must leave a feasible distance for all future stints.
            double balancedCurrentStint = raceDistanceRemaining / Math.Max(1, snapshot.PlannedStintsRemaining);
            double minimumCurrentStint = Math.Max(
                0.0,
                raceDistanceRemaining - fuelStops * futureFullStintLaps);
            double maximumCurrentStint = Math.Min(raceDistanceRemaining, currentPhysicalLaps);

            double earliestDistance = Math.Max(0.0, minimumCurrentStint);
            double latestDistance = Math.Max(0.0, maximumCurrentStint);
            if (latestDistance + 0.001 < earliestDistance)
            {
                snapshot.StrategyPlanStatus = "PLAN INFEASIBLE";
                return;
            }

            double optimalDistance = Math.Max(earliestDistance, Math.Min(latestDistance, balancedCurrentStint));
            snapshot.CurrentStintTargetLaps = optimalDistance;

            // Convert equivalent remaining distances into absolute pit-lap labels.
            // The crossing after one remaining lap is the current displayed lap.
            int completed = Math.Max(0, telemetry.LapCompleted);
            int earliest = Math.Max(snapshot.CurrentRaceLap,
                completed + Math.Max(1, (int)Math.Ceiling(trackFraction + earliestDistance)));
            int latest = Math.Max(snapshot.CurrentRaceLap,
                completed + Math.Max(1, (int)Math.Floor(trackFraction + latestDistance)));
            int optimal = Math.Max(earliest, Math.Min(latest,
                completed + Math.Max(1, (int)Math.Round(trackFraction + optimalDistance))));

            // Non-negotiable physical invariant: Latest can move later only when
            // actual saving increases currentPhysicalLaps.
            int physicalLatest = Math.Max(snapshot.CurrentRaceLap,
                completed + Math.Max(1, (int)Math.Floor(trackFraction + currentPhysicalLaps)));
            latest = Math.Min(latest, physicalLatest);
            optimal = Math.Min(optimal, latest);
            earliest = Math.Min(earliest, optimal);
            if (latest < earliest)
            {
                snapshot.StrategyPlanStatus = "NO PHYSICAL WINDOW";
                return;
            }

            double distanceBeforeStop = Math.Max(0.0, optimal - completed - trackFraction);
            double distanceAfterStop = Math.Max(0.0, raceDistanceRemaining - distanceBeforeStop);
            int futureStints = Math.Max(1, fuelStops);
            double targetNextStintLaps = Math.Min(futureFullStintLaps, distanceAfterStop / futureStints);
            snapshot.NextStintTargetLaps = targetNextStintLaps;

            double expectedFuelAtStop = Math.Max(0.0,
                snapshot.FuelLevelLiters - distanceBeforeStop * snapshot.StrategyUsageLiters);
            double targetFuelAfterStop = Math.Min(snapshot.FuelCapacityLiters,
                targetNextStintLaps * snapshot.StrategyUsageLiters + reserveFuel);
            snapshot.NextStopFuelToAddLiters = Math.Round(
                Math.Max(0.0, targetFuelAfterStop - expectedFuelAtStop), 2);

            // Driver-facing ToAdd is the next stop load. Total deficit remains
            // available separately for endurance planning/debugging.
            snapshot.TotalFuelDeficitLiters = snapshot.FuelToAddLiters;
            snapshot.FuelToAddLiters = snapshot.NextStopFuelToAddLiters;
            snapshot.FillToLiters = Math.Round(targetFuelAfterStop, 2);

            snapshot.PitEarliestLap = earliest;
            snapshot.PitOptimalLap = optimal;
            snapshot.PitLatestLap = latest;
            snapshot.NextPitLap = optimal;
            snapshot.PitWindowValid = true;
            snapshot.StrategyPlanStatus = "STRATEGY ACTIVE";
        }

        private static SessionEstimate GetSessionEstimate(TelemetrySnapshot telemetry, double lapTime)
        {
            int lapsRemaining = telemetry.SessionLapsRemaining;
            if (lapsRemaining > 0 && lapsRemaining < UnlimitedLapSentinelThreshold)
            {
                return new SessionEstimate(lapsRemaining, true, false, true, "LAPS");
            }

            if (telemetry.SessionTimeRemaining > 0.0 && lapTime > 0.0)
            {
                // Include the lap in progress and the final lap after time expires.
                double laps = Math.Ceiling(telemetry.SessionTimeRemaining / lapTime) + 1.0;
                if (laps > 0.0 && laps < UnlimitedLapSentinelThreshold)
                {
                    return new SessionEstimate(laps, true, true, false, "TIME + FINAL LAP");
                }
            }

            return new SessionEstimate(0.0, false, false, false, "UNAVAILABLE");
        }

        private static void SetConfidence(FuelSnapshot s)
        {
            if (s.ValidSampleCount <= 0)
            {
                s.Confidence = "NONE";
                s.ConfidencePercent = 0.0;
            }
            else if (s.ValidSampleCount < 3)
            {
                s.Confidence = "LOW";
                s.ConfidencePercent = 33.0;
            }
            else if (s.ValidSampleCount < 6)
            {
                s.Confidence = "MEDIUM";
                s.ConfidencePercent = 66.0;
            }
            else
            {
                s.Confidence = "HIGH";
                s.ConfidencePercent = 96.0;
            }
        }

        private static void ApplyAdaptiveConfidence(FuelSnapshot s)
        {
            if (!s.AdaptiveModelActive) return;

            if (s.Confidence == "HIGH")
            {
                s.Confidence = "MEDIUM";
                s.ConfidencePercent = Math.Min(78.0, s.ConfidencePercent);
            }
            else if (s.Confidence == "MEDIUM")
            {
                s.Confidence = "LOW";
                s.ConfidencePercent = Math.Min(55.0, s.ConfidencePercent);
            }
            else if (s.Confidence == "LOW")
            {
                s.ConfidencePercent = Math.Min(35.0, s.ConfidencePercent);
            }
        }

        private static double RelativeDeltaPercent(double recent, double baseline)
        {
            if (recent <= 0.0 || baseline <= 0.0) return 0.0;
            return (recent - baseline) / baseline * 100.0;
        }

        private static double RecentPercentile(List<double> values, int count, double percentile)
        {
            if (values.Count == 0) return 0.0;
            int start = Math.Max(0, values.Count - count);
            return Percentile(values.GetRange(start, values.Count - start), percentile);
        }

        private static double LinearTrend(List<double> values, int count)
        {
            if (values.Count < 2) return 0.0;
            int start = Math.Max(0, values.Count - count);
            int n = values.Count - start;
            if (n < 2) return 0.0;

            double sumX = 0.0;
            double sumY = 0.0;
            double sumXY = 0.0;
            double sumXX = 0.0;
            for (int i = 0; i < n; i++)
            {
                double x = i;
                double y = values[start + i];
                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumXX += x * x;
            }

            double denominator = n * sumXX - sumX * sumX;
            return Math.Abs(denominator) < 0.000001
                ? 0.0
                : (n * sumXY - sumX * sumY) / denominator;
        }

        private static string GetTrendStatus(double trend)
        {
            if (trend > 0.015) return "RISING";
            if (trend < -0.015) return "FALLING";
            return "STABLE";
        }

        private static double GetReasonableRequiredFuelUpperBound(FuelSnapshot s)
        {
            return s.FuelCapacityLiters > 0.0
                ? Math.Max(500.0, s.FuelCapacityLiters * 25.0)
                : 2500.0;
        }

        private static void AddSample(List<double> samples, double value)
        {
            samples.Add(value);
            while (samples.Count > MaximumSamples) samples.RemoveAt(0);
        }

        private static double RecentMedian(List<double> values, int count)
        {
            if (values.Count == 0) return 0.0;
            int start = Math.Max(0, values.Count - count);
            return Percentile(values.GetRange(start, values.Count - start), 0.50);
        }

        private static double TrimmedMean(List<double> values)
        {
            if (values.Count == 0) return 0.0;
            List<double> sorted = new List<double>(values);
            sorted.Sort();

            int trim = sorted.Count >= 8 ? Math.Max(1, sorted.Count / 10) : 0;
            double total = 0.0;
            int count = 0;
            for (int index = trim; index < sorted.Count - trim; index++)
            {
                total += sorted[index];
                count++;
            }

            return count > 0 ? total / count : 0.0;
        }

        private static double Percentile(List<double> values, double percentile)
        {
            if (values.Count == 0) return 0.0;
            List<double> sorted = new List<double>(values);
            sorted.Sort();

            double index = (sorted.Count - 1) * percentile;
            int lower = (int)Math.Floor(index);
            int upper = (int)Math.Ceiling(index);
            if (lower == upper) return sorted[lower];

            double fraction = index - lower;
            return sorted[lower] + (sorted[upper] - sorted[lower]) * fraction;
        }

        private static double Minimum(List<double> values)
        {
            if (values.Count == 0) return 0.0;
            double result = values[0];
            for (int index = 1; index < values.Count; index++) result = Math.Min(result, values[index]);
            return result;
        }

        private static double Maximum(List<double> values)
        {
            if (values.Count == 0) return 0.0;
            double result = values[0];
            for (int index = 1; index < values.Count; index++) result = Math.Max(result, values[index]);
            return result;
        }

        private static double MaximumPositive(double first, double second)
        {
            return Math.Max(first, second);
        }

        private static double Clamp01(double value)
        {
            return value < 0.0 ? 0.0 : (value > 1.0 ? 1.0 : value);
        }

        private static void ClearFinishAndPitOutputs(FuelSnapshot s)
        {
            s.FuelRequiredToFinishLiters = 0.0;
            s.FuelToAddLiters = 0.0;
            s.FillToLiters = 0.0;
            s.FinishMarginLiters = 0.0;
            s.TargetUsageLiters = 0.0;
            s.SavePerLapLiters = 0.0;
            s.LapsShort = 0.0;
            s.TanksNeeded = 0.0;
            s.PitStopsNeeded = 0;
            s.FuelStopsNeeded = 0;
            s.TotalStopsRemaining = s.MandatoryStopsRemaining;
            s.PitEarliestLap = 0;
            s.PitOptimalLap = 0;
            s.PitLatestLap = 0;
            s.PitWindowValid = false;
            s.NextPitLap = 0;
            s.PlannedStintsRemaining = 0;
            s.CurrentStintTargetLaps = 0.0;
            s.NextStintTargetLaps = 0.0;
            s.NextStopFuelToAddLiters = 0.0;
            s.TotalFuelDeficitLiters = 0.0;
            s.StrategyPlanStatus = "NO RACE TARGET";
            s.IsFuelShort = false;
        }

        private static void ClearCalculatedFuelOutputs(FuelSnapshot s)
        {
            s.FuelLapsRemaining = 0.0;
            s.DisplayLapsRemaining = 0.0;
            s.FuelTimeRemainingSeconds = 0.0;
            s.ReserveLiters = 0.0;
            s.ExtraLapTargetUsageLiters = 0.0;
            s.SaveForExtraLapLiters = 0.0;
            s.IsFuelCritical = false;
            ClearFinishAndPitOutputs(s);
        }

        private static void ClearSnapshot(FuelSnapshot s)
        {
            if (s == null) return;

            s.Ready = false;
            s.InstantDataReady = false;
            s.EngineState = "NO DATA";
            s.LearningProgressText = "0 / 3 VALID LAPS";
            s.FuelLevelLiters = 0.0;
            s.FuelLevelPercent = 0.0;
            s.FuelCapacityLiters = 0.0;
            s.LastLapUsageLiters = 0.0;
            s.AverageUsageLiters = 0.0;
            s.MedianUsageLiters = 0.0;
            s.RecentUsageLiters = 0.0;
            s.ConservativeUsageLiters = 0.0;
            s.StrategyUsageLiters = 0.0;
            s.BestUsageLiters = 0.0;
            s.WorstUsageLiters = 0.0;
            s.ValidSampleCount = 0;
            s.RejectedSampleCount = 0;
            s.LastSampleRejected = false;
            s.LastSampleRejectReason = string.Empty;
            s.AverageLapTimeSeconds = 0.0;
            s.MedianLapTimeSeconds = 0.0;
            s.RecentLapTimeSeconds = 0.0;
            s.ConservativeLapTimeSeconds = 0.0;
            s.StrategyLapTimeSeconds = 0.0;
            s.LapTimeSource = "UNAVAILABLE";
            s.EstimatedSessionLapsRemaining = 0.0;
            s.CurrentRaceLap = 0;
            s.EstimatedFinishLap = 0;
            ClearCalculatedFuelOutputs(s);
            s.RefuelDetected = false;
            s.HasFinishEstimate = false;
            s.IsTimedSession = false;
            s.IsLapLimitedSession = false;
            s.EstimateSource = "UNAVAILABLE";
            s.Confidence = "NONE";
            s.ConfidencePercent = 0.0;
            s.Status = "LEARNING";
            s.Summary = "WAITING FOR CLEAN LAPS";
            s.Error = string.Empty;
            s.UpdatedAtUtc = DateTime.MinValue;
        }

        private sealed class SessionEstimate
        {
            public SessionEstimate(double lapsRemaining, bool available, bool timed, bool lapLimited, string source)
            {
                LapsRemaining = lapsRemaining;
                IsAvailable = available;
                IsTimed = timed;
                IsLapLimited = lapLimited;
                Source = source;
            }

            public double LapsRemaining { get; private set; }
            public bool IsAvailable { get; private set; }
            public bool IsTimed { get; private set; }
            public bool IsLapLimited { get; private set; }
            public string Source { get; private set; }
        }
    }
}
