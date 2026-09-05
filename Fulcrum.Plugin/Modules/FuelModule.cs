using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Fulcrum.Core.Fuel;
using Fulcrum.Core.Performance;
using Fulcrum.Core.Telemetry;
using Fulcrum.Plugin.Publishing;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Modules
{
    public sealed class FuelModule
    {
        private readonly FuelTelemetryReader reader;
        private readonly FuelCalculator calculator;
        private readonly FuelSnapshot snapshot;
        private readonly FuelPublisher publisher;
        private readonly ScheduledTask updateTask;

        private object rawData;
        private TelemetrySnapshot telemetry;
        private bool gameRunning;

        // Minimal direct lap tracker. This is intentionally independent from the
        // strategy calculator so we can validate lap closure and fuel deltas first.
        private bool trackerInitialized;
        private int trackerCompletedLaps;
        private double trackerFuelAtLapStart;
        private bool trackerLapTouchedPit;
        private bool trackerRefuelDetected;
        private double trackerPreviousFuel;
        private readonly List<double> trackerValidUsages = new List<double>();
        private readonly List<double> trackerValidLapTimes = new List<double>();
        private double trackerSessionTimeAtLapStart;

        // Projection stabilization. The race projection is published at a
        // human-readable resolution instead of reacting to every milliliter and
        // every tiny track-position change on every telemetry frame.
        private bool projectionFilterInitialized;
        private double filteredProjectionLaps;
        private double filteredProjectionRequired;
        private double filteredProjectionMargin;
        private string filteredProjectionSource = string.Empty;
        private string cachedSessionType = string.Empty;

        // Race-target debounce and output hold. Some SimHub/iRacing session fields
        // briefly alternate between a real target and an unlimited/sentinel value.
        // The dashboard must never react to those one-frame transitions.
        private string projectionCandidateKey = string.Empty;
        private DateTime projectionCandidateSinceUtc = DateTime.MinValue;
        private DateTime projectionInvalidSinceUtc = DateTime.MinValue;
        private DateTime lastProjectionEvaluationUtc = DateTime.MinValue;
        private bool projectionTargetLocked;
        private string lockedProjectionSource = string.Empty;
        private double lockedProjectionTarget;
        private bool lockedProjectionTimed;
        private bool lockedProjectionLapLimited;

        // Strategic display values are event-driven. They are recalculated only
        // after a valid lap closes or after a genuinely new race target becomes
        // locked. Between those events, the published values remain unchanged.
        private bool projectionRefreshRequested;

        // Real-stint tracking. The first stint may start with a partial tank, and
        // every later stint must begin from the fuel actually loaded in pit lane.
        // These values are deliberately independent from maximum tank capacity.
        private bool stintInitialized;
        private double stintStartFuelLiters;
        private int stintStartLap;
        private double pendingFuelAddedLiters;
        private double previousStintFrameFuel;
        private bool previousStintFrameInPit;

        // Strategy progress tracking. The opening pit exit establishes the first
        // stint and is not counted as a race stop. Later pit exits with detected
        // refuelling advance NEXT STOP X OF Y.
        private bool openingStintStarted;
        private int strategyStopsCompleted;
        private bool strategyPitVisitArmed;
        private int strategyPitEntryCompletedLaps;
        private double strategyPitEntryFuelLiters;

        private int strategySessionNumber;
        private string strategySessionType = string.Empty;

        public FuelModule(PluginManager pluginManager, Type pluginType, UpdateScheduler scheduler)
        {
            if (pluginManager == null) throw new ArgumentNullException(nameof(pluginManager));
            if (pluginType == null) throw new ArgumentNullException(nameof(pluginType));
            if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));

            reader = new FuelTelemetryReader();
            calculator = new FuelCalculator();
            snapshot = new FuelSnapshot();
            publisher = new FuelPublisher(pluginManager, pluginType);
            updateTask = scheduler.RegisterTask("Fuel Calculator", UpdateRates.FuelHz, UpdateScheduled, false);
            Reset();
        }

        public FuelSnapshot Snapshot
        {
            get { return snapshot; }
        }

        public void SetFrameContext(object currentRawData, TelemetrySnapshot telemetrySnapshot, bool isGameRunning)
        {
            rawData = currentRawData;
            telemetry = telemetrySnapshot;
            gameRunning = isGameRunning;

            // Fuel Core v3 deliberately publishes the basic telemetry immediately
            // on every SimHub data frame. It does not depend on the scheduler, the
            // lap tracker, or any strategy calculation. This makes it possible to
            // verify property registration and frame flow independently.
            PublishBasicFrame(currentRawData, telemetrySnapshot, isGameRunning);
        }

        private void PublishBasicFrame(object frame, TelemetrySnapshot telemetrySnapshot, bool isGameRunning)
        {
            if (!isGameRunning || frame == null)
            {
                snapshot.BasicSource = "NO FRAME";
                publisher.Publish(snapshot);
                return;
            }

            double fuel = FirstPositive(
                ReadNumber(frame, "Fuel"),
                ReadNumber(frame, "FuelLevel"),
                telemetrySnapshot != null ? telemetrySnapshot.FuelLevelLiters : 0.0);

            double maxFuel = FirstPositive(
                ReadNumber(frame, "MaxFuel"),
                ReadNumber(frame, "FuelCapacity"),
                ReadNumber(frame, "DriverCarFuelMaxLtr"));

            double percent01 = FirstPositive(
                NormalizePercent(ReadNumber(frame, "FuelPercent")),
                NormalizePercent(ReadNumber(frame, "FuelLevelPct")),
                telemetrySnapshot != null ? telemetrySnapshot.FuelLevelPercent : 0.0);

            if (maxFuel <= 0.0 && fuel > 0.0 && percent01 > 0.0001)
            {
                maxFuel = fuel / percent01;
            }
            if (percent01 <= 0.0 && fuel >= 0.0 && maxFuel > 0.0)
            {
                percent01 = Clamp01(fuel / maxFuel);
            }

            int completed = ReadInt(frame, "CompletedLaps", telemetrySnapshot != null ? telemetrySnapshot.LapCompleted : 0);
            int currentLap = ReadInt(frame, "Lap", telemetrySnapshot != null ? telemetrySnapshot.Lap : 0);
            double track = FirstFinite(
                ReadNumber(frame, "TrackPositionPercent"),
                ReadNumber(frame, "LapDistPct"),
                telemetrySnapshot != null ? telemetrySnapshot.LapDistancePercent : 0.0);
            if (track > 1.5) track /= 100.0;
            track = Clamp01(track);

            bool inPit = FirstBool(frame, "IsInPit", "InPit").GetValueOrDefault(telemetrySnapshot != null && telemetrySnapshot.IsOnPitRoad);
            bool inPitLane = FirstBool(frame, "IsInPitLane", "InPitLane", "OnPitRoad").GetValueOrDefault(telemetrySnapshot != null && telemetrySnapshot.IsOnPitRoad);

            double sessionTime = telemetrySnapshot != null ? telemetrySnapshot.SessionTime : ReadNumber(frame, "SessionTime");
            RefreshStrategySessionContext(frame, telemetrySnapshot, completed, fuel, inPit || inPitLane);
            UpdateDirectLapTracker(completed, fuel, inPit || inPitLane, sessionTime);
            UpdateStintState(completed, fuel, inPit || inPitLane, IsStrategyCountingAllowed());

            snapshot.CurrentFuel = fuel;
            snapshot.MaxFuel = maxFuel;
            snapshot.FuelPercent = percent01 * 100.0;
            snapshot.CompletedLaps = completed;
            snapshot.CurrentLapTelemetry = currentLap;
            snapshot.TrackPositionPercent = track * 100.0;
            snapshot.IsInPit = inPit;
            snapshot.IsInPitLane = inPitLane;
            snapshot.BasicFrameCounter++;
            snapshot.BasicSource = fuel > 0.0 || maxFuel > 0.0 ? "NORMALIZED FRAME" : "FRAME WITHOUT FUEL";

            // Keep the legacy instant aliases synchronized from frame one.
            snapshot.FuelLevelLiters = fuel;
            snapshot.FuelCapacityLiters = maxFuel;
            snapshot.FuelLevelPercent = percent01;
            snapshot.InstantDataReady = fuel >= 0.0 && maxFuel > 0.0;

            UpdateDynamicStintTargets(track);
            UpdateMandatoryStopState(frame);
            UpdateRaceProjection(frame, telemetrySnapshot, track);
            UpdatePitWindowEngine(track);
            UpdateFuelCoach();
            UpdateDriverFacingStrategyStatus();
            SynchronizeStateModels();
            publisher.Publish(snapshot);
        }

        public void Reset()
        {
            rawData = null;
            telemetry = null;
            gameRunning = false;
            trackerInitialized = false;
            trackerCompletedLaps = 0;
            trackerFuelAtLapStart = 0.0;
            trackerLapTouchedPit = false;
            trackerRefuelDetected = false;
            trackerPreviousFuel = 0.0;
            trackerValidUsages.Clear();
            trackerValidLapTimes.Clear();
            trackerSessionTimeAtLapStart = 0.0;
            stintInitialized = false;
            stintStartFuelLiters = 0.0;
            stintStartLap = 0;
            pendingFuelAddedLiters = 0.0;
            previousStintFrameFuel = 0.0;
            previousStintFrameInPit = false;
            openingStintStarted = false;
            strategyStopsCompleted = 0;
            strategyPitVisitArmed = false;
            strategyPitEntryCompletedLaps = 0;
            strategyPitEntryFuelLiters = 0.0;
            strategySessionNumber = -1;
            strategySessionType = string.Empty;
            ResetProjectionFilter();
            calculator.Reset(snapshot);
            snapshot.TrackerInitialized = false;
            snapshot.TrackerStatus = "WAITING FOR FRAME";
            snapshot.TrackerLapStartNumber = 0;
            snapshot.TrackerLapStartFuel = 0.0;
            snapshot.TrackerLastClosedLapNumber = 0;
            snapshot.TrackerLastClosedLapStartFuel = 0.0;
            snapshot.TrackerLastClosedLapEndFuel = 0.0;
            snapshot.TrackerLastClosedLapUsage = 0.0;
            snapshot.TrackerLastClosedLapValid = false;
            snapshot.TrackerLastClosedLapReason = string.Empty;
            snapshot.TrackerValidLapCount = 0;
            snapshot.TrackerRejectedLapCount = 0;
            snapshot.StintStartFuelLiters = 0.0;
            snapshot.StintStartLap = 0;
            snapshot.FuelAddedThisStopLiters = 0.0;
            snapshot.CurrentStintFuelUsedLiters = 0.0;
            snapshot.PhysicalLatestPitLap = 0;
            snapshot.PhysicalStintLapsRemaining = 0.0;
            ResetTrackerStatistics();
            snapshot.PitWindowValid = false;
            snapshot.PitEarliestLap = 0;
            snapshot.PitOptimalLap = 0;
            snapshot.PitLatestLap = 0;
            snapshot.PitWindowPhase = "UNAVAILABLE";
            snapshot.Recommendation = "NO RACE TARGET";
            snapshot.PitWindowProgressPercent = 0.0;
            snapshot.PitWindowCurrentPositionPercent = 0.0;
            snapshot.PitWindowSpanLaps = 0;
            snapshot.LapsToOptimalPit = 0;
            snapshot.PitWindowStateCode = "UNAVAILABLE";
            snapshot.PitWindowStateText = "WAITING";
            snapshot.PitWindowActionText = "NO RACE TARGET";
            snapshot.FuelCoachStateCode = "WAITING";
            snapshot.FuelCoachStateText = "LEARNING";
            snapshot.FuelCoachActionText = "COMPLETE CLEAN LAPS";
            snapshot.FuelCoachTargetLiters = 0.0;
            snapshot.FuelCoachActualLiters = 0.0;
            snapshot.FuelCoachDeltaLiters = 0.0;
            snapshot.FuelCoachBufferLiters = 0.0;
            SynchronizeStateModels();
            publisher.Publish(snapshot);
        }

        private void UpdateStintState(int completedLaps, double currentFuel, bool isInPit, bool strategyCountingAllowed)
        {
            if (currentFuel < 0.0 || double.IsNaN(currentFuel) || double.IsInfinity(currentFuel)) return;

            int canonicalLap = Math.Max(1, completedLaps + 1);
            if (!stintInitialized)
            {
                stintInitialized = true;
                stintStartFuelLiters = currentFuel;
                stintStartLap = canonicalLap;
                previousStintFrameFuel = currentFuel;
                previousStintFrameInPit = isInPit;
                pendingFuelAddedLiters = 0.0;
                openingStintStarted = !isInPit;
            }

            double increase = currentFuel - previousStintFrameFuel;
            if (increase > 0.05)
            {
                pendingFuelAddedLiters += increase;
                snapshot.RefuelDetected = true;
            }

            // Arm a strategy stop only after a genuine on-track stint has begun.
            // This prevents the grid/garage transition and pre-race fuel adjustments
            // from being counted as the first completed stop.
            if (!previousStintFrameInPit && isInPit)
            {
                double stintFuelUsed = Math.Max(0.0, stintStartFuelLiters - currentFuel);
                strategyPitVisitArmed = strategyCountingAllowed &&
                    openingStintStarted &&
                    completedLaps > 0 &&
                    (canonicalLap > stintStartLap || stintFuelUsed > 0.25);
                strategyPitEntryCompletedLaps = completedLaps;
                strategyPitEntryFuelLiters = currentFuel;
            }

            // A stint starts from the fuel that is actually present when the car
            // leaves pit lane. This also handles the opening stint with a partial tank.
            if (previousStintFrameInPit && !isInPit)
            {
                stintStartFuelLiters = currentFuel;
                stintStartLap = canonicalLap;
                double fuelAdded = Math.Round(Math.Max(0.0, pendingFuelAddedLiters), 2);
                snapshot.FuelAddedThisStopLiters = fuelAdded;

                if (!openingStintStarted)
                {
                    // First departure from pit lane/garage starts the opening stint.
                    openingStintStarted = true;
                }
                else if (strategyCountingAllowed && strategyPitVisitArmed && fuelAdded > 0.10)
                {
                    strategyStopsCompleted++;
                }

                strategyPitVisitArmed = false;
                strategyPitEntryCompletedLaps = 0;
                strategyPitEntryFuelLiters = 0.0;
                pendingFuelAddedLiters = 0.0;
            }
            else if (isInPit && increase > 0.05)
            {
                // Keep following the live fill level so the next stint baseline is
                // the final amount loaded, not maximum tank capacity.
                stintStartFuelLiters = currentFuel;
                stintStartLap = canonicalLap;
            }

            snapshot.StintStartFuelLiters = Math.Round(stintStartFuelLiters, 3);
            snapshot.StintStartLap = stintStartLap;
            snapshot.CurrentStintFuelUsedLiters = Math.Round(Math.Max(0.0, stintStartFuelLiters - currentFuel), 3);

            previousStintFrameFuel = currentFuel;
            previousStintFrameInPit = isInPit;
        }

        private void RefreshStrategySessionContext(object frame, TelemetrySnapshot telemetrySnapshot, int completedLaps, double currentFuel, bool isInPit)
        {
            int sessionNumber = telemetrySnapshot != null ? telemetrySnapshot.SessionNumber : ReadInt(frame, "SessionNum", -1);
            string sessionType = telemetrySnapshot != null ? telemetrySnapshot.SessionType : string.Empty;
            if (string.IsNullOrWhiteSpace(sessionType))
            {
                object rawSessionType = ReadValue(frame, "SessionType");
                if (rawSessionType == null) rawSessionType = ReadValue(frame, "SessionTypeName");
                sessionType = rawSessionType != null ? Convert.ToString(rawSessionType, CultureInfo.InvariantCulture) : string.Empty;
            }

            string normalizedType = string.IsNullOrWhiteSpace(sessionType)
                ? string.Empty
                : sessionType.Trim().ToUpperInvariant();

            bool sessionNumberChanged = strategySessionNumber >= 0 && sessionNumber >= 0 && sessionNumber != strategySessionNumber;
            bool sessionTypeChanged = !string.IsNullOrWhiteSpace(normalizedType) &&
                !string.Equals(strategySessionType, normalizedType, StringComparison.Ordinal);

            if (sessionNumberChanged || sessionTypeChanged)
            {
                ResetStrategyProgressForNewSession(currentFuel, isInPit);
            }

            if (sessionNumber >= 0) strategySessionNumber = sessionNumber;
            if (!string.IsNullOrWhiteSpace(normalizedType)) strategySessionType = normalizedType;
        }

        private void ResetStrategyProgressForNewSession(double currentFuel, bool isInPit)
        {
            openingStintStarted = !isInPit;
            strategyStopsCompleted = 0;
            strategyPitVisitArmed = false;
            strategyPitEntryCompletedLaps = 0;
            strategyPitEntryFuelLiters = 0.0;
            pendingFuelAddedLiters = 0.0;
            stintInitialized = false;
            previousStintFrameFuel = currentFuel;
            previousStintFrameInPit = isInPit;
            snapshot.FuelAddedThisStopLiters = 0.0;
            snapshot.StopsCompleted = 0;
            snapshot.NextStopNumber = 0;
            snapshot.StopProgressText = "NO RACE PLAN";
        }

        private bool IsStrategyCountingAllowed()
        {
            return !string.IsNullOrWhiteSpace(strategySessionType) && !IsNonRaceSession(strategySessionType);
        }

        private void UpdateDirectLapTracker(int completedLaps, double currentFuel, bool isInPit, double sessionTime)
        {
            if (completedLaps < 0 || currentFuel < 0.0 || double.IsNaN(currentFuel) || double.IsInfinity(currentFuel))
            {
                snapshot.TrackerStatus = "INVALID DIRECT FRAME";
                return;
            }

            if (!trackerInitialized || completedLaps < trackerCompletedLaps)
            {
                trackerInitialized = true;
                trackerCompletedLaps = completedLaps;
                trackerFuelAtLapStart = currentFuel;
                trackerPreviousFuel = currentFuel;
                trackerLapTouchedPit = isInPit;
                trackerRefuelDetected = false;
                trackerSessionTimeAtLapStart = sessionTime;

                snapshot.TrackerInitialized = true;
                snapshot.TrackerStatus = "BASELINE SET";
                snapshot.TrackerLapStartNumber = completedLaps + 1;
                snapshot.TrackerLapStartFuel = currentFuel;
                return;
            }

            if (isInPit) trackerLapTouchedPit = true;
            if (currentFuel > trackerPreviousFuel + 0.15) trackerRefuelDetected = true;

            if (completedLaps > trackerCompletedLaps)
            {
                int lapDelta = completedLaps - trackerCompletedLaps;
                double usage = trackerFuelAtLapStart - currentFuel;
                double lapTime = sessionTime > trackerSessionTimeAtLapStart ? sessionTime - trackerSessionTimeAtLapStart : 0.0;
                bool physicalUsage = usage >= 0.03 && usage <= 45.0;
                bool valid = lapDelta == 1 && !trackerLapTouchedPit && !trackerRefuelDetected && physicalUsage;
                string reason;

                if (lapDelta != 1) reason = "LAP COUNTER JUMP";
                else if (trackerLapTouchedPit) reason = "PIT / OUTLAP";
                else if (trackerRefuelDetected) reason = "REFUEL";
                else if (!physicalUsage) reason = "USAGE OUT OF RANGE";
                else reason = "VALID";

                snapshot.TrackerLastClosedLapNumber = completedLaps;
                snapshot.TrackerLastClosedLapStartFuel = trackerFuelAtLapStart;
                snapshot.TrackerLastClosedLapEndFuel = currentFuel;
                snapshot.TrackerLastClosedLapUsage = usage;
                snapshot.TrackerLastClosedLapValid = valid;
                snapshot.TrackerLastClosedLapReason = reason;
                snapshot.TrackerStatus = valid ? "LAP ACCEPTED" : "LAP REJECTED";
                if (valid)
                {
                    snapshot.TrackerValidLapCount++;
                    trackerValidUsages.Add(usage);
                    if (trackerValidUsages.Count > 50) trackerValidUsages.RemoveAt(0);
                    if (lapTime >= 20.0 && lapTime <= 1800.0)
                    {
                        trackerValidLapTimes.Add(lapTime);
                        if (trackerValidLapTimes.Count > 50) trackerValidLapTimes.RemoveAt(0);
                    }
                    UpdateTrackerStatistics();
                    projectionRefreshRequested = true;
                }
                else
                {
                    snapshot.TrackerRejectedLapCount++;
                    snapshot.LastSampleRejected = true;
                    snapshot.LastSampleRejectReason = reason;
                    snapshot.RejectedSampleCount = snapshot.TrackerRejectedLapCount;
                }

                trackerCompletedLaps = completedLaps;
                trackerFuelAtLapStart = currentFuel;
                trackerLapTouchedPit = isInPit;
                trackerRefuelDetected = false;
                trackerSessionTimeAtLapStart = sessionTime;
                snapshot.TrackerLapStartNumber = completedLaps + 1;
                snapshot.TrackerLapStartFuel = currentFuel;
            }
            else
            {
                snapshot.TrackerStatus = trackerLapTouchedPit ? "TRACKING - PIT TOUCHED" : "TRACKING LAP";
            }

            trackerPreviousFuel = currentFuel;
        }


        private void ResetTrackerStatistics()
        {
            snapshot.LastLapUsageLiters = 0.0;
            snapshot.AverageUsageLiters = 0.0;
            snapshot.MedianUsageLiters = 0.0;
            snapshot.RecentUsageLiters = 0.0;
            snapshot.ConservativeUsageLiters = 0.0;
            snapshot.StrategyUsageLiters = 0.0;
            snapshot.BestUsageLiters = 0.0;
            snapshot.WorstUsageLiters = 0.0;
            snapshot.FuelLapsRemaining = 0.0;
            snapshot.WholeLapsRemaining = 0;
            snapshot.FuelAfterNextLapLiters = 0.0;
            snapshot.StintRemainderLiters = 0.0;
            snapshot.UsageTrendLitersPerLap = 0.0;
            snapshot.UsageTrendStatus = "STABLE";
            snapshot.AverageLapTimeSeconds = 0.0;
            snapshot.MedianLapTimeSeconds = 0.0;
            snapshot.RecentLapTimeSeconds = 0.0;
            snapshot.ConservativeLapTimeSeconds = 0.0;
            snapshot.StrategyLapTimeSeconds = 0.0;
            snapshot.LapTimeSource = "UNAVAILABLE";
            snapshot.EstimatedSessionLapsRemaining = 0.0;
            snapshot.FuelRequiredToFinishLiters = 0.0;
            snapshot.FinishMarginLiters = 0.0;
            snapshot.ReserveLiters = 0.0;
            snapshot.HasFinishEstimate = false;
            snapshot.IsFuelShort = false;
            snapshot.IsTimedSession = false;
            snapshot.IsLapLimitedSession = false;
            snapshot.EstimateSource = "UNAVAILABLE";
            snapshot.ProjectionStatus = "LEARNING";
            snapshot.ValidSampleCount = 0;
            snapshot.RejectedSampleCount = 0;
            snapshot.LastSampleRejected = false;
            snapshot.LastSampleRejectReason = string.Empty;
            snapshot.Confidence = "NONE";
            snapshot.ConfidencePercent = 0.0;
            snapshot.EngineState = "LEARNING";
            snapshot.LearningProgressText = "0 / 3 VALID LAPS";
            snapshot.Status = "LEARNING";
            snapshot.Summary = "WAITING FOR CLEAN LAPS";
        }

        private void UpdateTrackerStatistics()
        {
            if (trackerValidUsages.Count == 0)
            {
                ResetTrackerStatistics();
                return;
            }

            var ordered = trackerValidUsages.OrderBy(v => v).ToList();
            int count = ordered.Count;
            snapshot.LastLapUsageLiters = trackerValidUsages[trackerValidUsages.Count - 1];
            snapshot.AverageUsageLiters = TrimmedAverage(trackerValidUsages);
            snapshot.AverageBasisText = count.ToString(CultureInfo.InvariantCulture) + " VALID LAPS";
            snapshot.MedianUsageLiters = Percentile(ordered, 0.50);

            int recentCount = Math.Min(3, trackerValidUsages.Count);
            var recent = trackerValidUsages.Skip(trackerValidUsages.Count - recentCount).OrderBy(v => v).ToList();
            snapshot.RecentUsageLiters = Percentile(recent, 0.50);
            snapshot.ConservativeUsageLiters = Percentile(ordered, 0.75);
            snapshot.StrategyUsageLiters = Math.Max(snapshot.RecentUsageLiters, snapshot.ConservativeUsageLiters);
            snapshot.BestUsageLiters = ordered[0];
            snapshot.WorstUsageLiters = ordered[ordered.Count - 1];

            if (trackerValidLapTimes.Count > 0)
            {
                var orderedLapTimes = trackerValidLapTimes.OrderBy(v => v).ToList();
                snapshot.AverageLapTimeSeconds = trackerValidLapTimes.Average();
                snapshot.MedianLapTimeSeconds = Percentile(orderedLapTimes, 0.50);

                int recentLapTimeCount = Math.Min(3, trackerValidLapTimes.Count);
                var recentLapTimes = trackerValidLapTimes
                    .Skip(trackerValidLapTimes.Count - recentLapTimeCount)
                    .OrderBy(v => v)
                    .ToList();
                snapshot.RecentLapTimeSeconds = Percentile(recentLapTimes, 0.50);
                snapshot.ConservativeLapTimeSeconds = Percentile(orderedLapTimes, 0.75);

                double robustLapTime = Math.Max(snapshot.RecentLapTimeSeconds, snapshot.ConservativeLapTimeSeconds);
                if (snapshot.MedianLapTimeSeconds > 1.0)
                {
                    robustLapTime = Math.Min(robustLapTime, snapshot.MedianLapTimeSeconds * 1.25);
                }
                snapshot.StrategyLapTimeSeconds = robustLapTime > 1.0
                    ? robustLapTime
                    : snapshot.MedianLapTimeSeconds;
                snapshot.LapTimeSource = recentLapTimeCount >= 2 ? "RECENT / P75" : "MEDIAN";
            }

            double safeUsage = snapshot.StrategyUsageLiters;
            if (safeUsage > 0.0001)
            {
                snapshot.FuelLapsRemaining = Math.Max(0.0, snapshot.CurrentFuel / safeUsage);
                snapshot.WholeLapsRemaining = Math.Max(0, (int)Math.Floor(snapshot.FuelLapsRemaining));
                snapshot.FuelAfterNextLapLiters = Math.Max(0.0, snapshot.CurrentFuel - safeUsage);
                snapshot.StintRemainderLiters = Math.Max(0.0, snapshot.CurrentFuel - (snapshot.WholeLapsRemaining * safeUsage));

                // The extra-lap target is refreshed from the live circular track
                // position by UpdateDynamicStintTargets(). This avoids the common
                // one-lap error caused by treating a partially completed lap as a
                // complete lap.
                snapshot.DisplayLapsRemaining = snapshot.FuelLapsRemaining;

                double referenceLapTime = snapshot.StrategyLapTimeSeconds > 1.0
                    ? snapshot.StrategyLapTimeSeconds
                    : (snapshot.MedianLapTimeSeconds > 1.0
                        ? snapshot.MedianLapTimeSeconds
                        : snapshot.AverageLapTimeSeconds);
                snapshot.FuelTimeRemainingSeconds = referenceLapTime > 1.0
                    ? snapshot.FuelLapsRemaining * referenceLapTime
                    : 0.0;
            }

            int trendCount = Math.Min(5, trackerValidUsages.Count);
            if (trendCount >= 2)
            {
                var trendValues = trackerValidUsages.Skip(trackerValidUsages.Count - trendCount).ToList();
                double meanX = (trendCount - 1) / 2.0;
                double meanY = trendValues.Average();
                double numerator = 0.0;
                double denominator = 0.0;
                for (int i = 0; i < trendCount; i++)
                {
                    double dx = i - meanX;
                    numerator += dx * (trendValues[i] - meanY);
                    denominator += dx * dx;
                }
                snapshot.UsageTrendLitersPerLap = denominator > 0.0 ? numerator / denominator : 0.0;
                if (snapshot.UsageTrendLitersPerLap > 0.015) snapshot.UsageTrendStatus = "RISING";
                else if (snapshot.UsageTrendLitersPerLap < -0.015) snapshot.UsageTrendStatus = "FALLING";
                else snapshot.UsageTrendStatus = "STABLE";
            }
            else
            {
                snapshot.UsageTrendLitersPerLap = 0.0;
                snapshot.UsageTrendStatus = "STABLE";
            }

            snapshot.ValidSampleCount = count;
            snapshot.RejectedSampleCount = snapshot.TrackerRejectedLapCount;
            snapshot.LastSampleRejected = false;
            snapshot.LastSampleRejectReason = string.Empty;

            if (count >= 5)
            {
                snapshot.Confidence = "HIGH";
                snapshot.ConfidencePercent = 96.0;
                snapshot.EngineState = "READY";
                snapshot.Status = "READY";
            }
            else if (count >= 3)
            {
                snapshot.Confidence = "MEDIUM";
                snapshot.ConfidencePercent = 70.0;
                snapshot.EngineState = "READY";
                snapshot.Status = "READY";
            }
            else
            {
                snapshot.Confidence = "LOW";
                snapshot.ConfidencePercent = count == 2 ? 45.0 : 25.0;
                snapshot.EngineState = "LEARNING";
                snapshot.Status = "LEARNING";
            }

            snapshot.LearningProgressText = count >= 3
                ? snapshot.Confidence + " CONFIDENCE"
                : count.ToString(CultureInfo.InvariantCulture) + " / 3 VALID LAPS";
            snapshot.Summary = "LAST " + snapshot.LastLapUsageLiters.ToString("0.000", CultureInfo.InvariantCulture) +
                " L  AVG " + snapshot.AverageUsageLiters.ToString("0.000", CultureInfo.InvariantCulture) + " L";
        }

        private void UpdateDynamicStintTargets(double trackFraction)
        {
            double safeUsage = snapshot.StrategyUsageLiters;
            if (safeUsage <= 0.0001 || snapshot.CurrentFuel <= 0.0)
            {
                snapshot.ExtraLapTargetUsageLiters = 0.0;
                snapshot.SaveForExtraLapLiters = 0.0;
                return;
            }

            double track = Clamp01(trackFraction);
            double lapsAvailable = snapshot.CurrentFuel / safeUsage;
            int achievableCrossings = Math.Max(0, (int)Math.Floor(track + lapsAvailable));
            double equivalentDistanceToExtraCrossing = Math.Max(0.05, (achievableCrossings + 1.0) - track);
            double target = snapshot.CurrentFuel / equivalentDistanceToExtraCrossing;

            // Publish a stable, driver-readable target. The raw calculation may
            // move by milliliters as the car travels, but the displayed value only
            // changes when the rounded hundredth changes.
            snapshot.ExtraLapTargetUsageLiters = Math.Round(target, 2);
            snapshot.SaveForExtraLapLiters = Math.Round(Math.Max(0.0, safeUsage - target), 2);
        }

        private void UpdateMandatoryStopState(object frame)
        {
            int required = FirstNonNegativeInt(
                ReadInt(frame, "MandatoryStopsRequired", -1),
                ReadInt(frame, "MandatoryPitStopsRequired", -1),
                ReadInt(frame, "NumMandatoryPitStops", -1));
            int completed = FirstNonNegativeInt(
                ReadInt(frame, "MandatoryStopsCompleted", -1),
                ReadInt(frame, "MandatoryPitStopsCompleted", -1),
                ReadInt(frame, "NumMandatoryPitStopsCompleted", -1));

            if (required >= 0)
            {
                snapshot.MandatoryStopsRequired = required;
                snapshot.MandatoryStopsCompleted = Math.Max(0, Math.Min(required, completed >= 0 ? completed : 0));
                snapshot.MandatoryStopsRemaining = Math.Max(0, required - snapshot.MandatoryStopsCompleted);
                snapshot.MandatoryStopsSource = "AUTO";
            }
            else
            {
                snapshot.MandatoryStopsRequired = 0;
                snapshot.MandatoryStopsCompleted = 0;
                snapshot.MandatoryStopsRemaining = 0;
                snapshot.MandatoryStopsSource = "AUTO / UNAVAILABLE";
            }

            snapshot.TotalStopsRemaining = Math.Max(snapshot.FuelStopsNeeded, snapshot.MandatoryStopsRemaining);
        }

        private static int FirstNonNegativeInt(params int[] values)
        {
            if (values == null) return -1;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] >= 0) return values[i];
            }
            return -1;
        }

        private void UpdateRaceProjection(object frame, TelemetrySnapshot telemetrySnapshot, double trackFraction)
        {
            double safeUsage = snapshot.StrategyUsageLiters;
            if (snapshot.ValidSampleCount <= 0 || safeUsage <= 0.0001)
            {
                ClearRaceProjection("LEARNING", "LEARNING", false, false);
                return;
            }

            string sessionType = telemetrySnapshot != null ? telemetrySnapshot.SessionType : string.Empty;
            if (string.IsNullOrWhiteSpace(sessionType))
            {
                object rawSessionType = ReadValue(frame, "SessionType");
                if (rawSessionType == null) rawSessionType = ReadValue(frame, "SessionTypeName");
                sessionType = rawSessionType != null ? Convert.ToString(rawSessionType, CultureInfo.InvariantCulture) : string.Empty;
            }
            if (!string.IsNullOrWhiteSpace(sessionType)) cachedSessionType = sessionType;
            else if (!string.IsNullOrWhiteSpace(cachedSessionType)) sessionType = cachedSessionType;

            // A confirmed non-race session is authoritative. Clear once and keep it
            // stable; do not let transient lap/time sentinel values override it.
            if (IsNonRaceSession(sessionType))
            {
                if (snapshot.HasFinishEstimate || snapshot.ProjectionStatus != "NO RACE TARGET")
                {
                    ClearRaceProjection("NO RACE TARGET", NormalizeSessionLabel(sessionType), false, false);
                }
                return;
            }

            int directLapsRemaining = telemetrySnapshot != null ? telemetrySnapshot.SessionLapsRemaining : 0;
            if (!IsReasonableLapTarget(directLapsRemaining)) directLapsRemaining = ReadInt(frame, "SessionLapsRemaining", 0);
            if (!IsReasonableLapTarget(directLapsRemaining)) directLapsRemaining = 0;

            double timeRemaining = telemetrySnapshot != null ? telemetrySnapshot.SessionTimeRemaining : 0.0;
            if (!IsReasonableTimeTarget(timeRemaining))
            {
                timeRemaining = FirstPositive(ReadNumber(frame, "SessionTimeRemaining"), ReadNumber(frame, "SessionTimeRemain"));
            }
            if (!IsReasonableTimeTarget(timeRemaining)) timeRemaining = 0.0;

            double referenceLapTime = snapshot.StrategyLapTimeSeconds > 1.0
                ? snapshot.StrategyLapTimeSeconds
                : (snapshot.MedianLapTimeSeconds > 1.0
                    ? snapshot.MedianLapTimeSeconds
                    : snapshot.AverageLapTimeSeconds);

            string candidateSource = string.Empty;
            double candidateTarget = 0.0;
            bool candidateTimed = false;
            bool candidateLapLimited = false;

            if (directLapsRemaining > 0)
            {
                candidateSource = "LAPS";
                candidateTarget = directLapsRemaining;
                candidateLapLimited = true;
            }
            else if (timeRemaining > 0.0 && referenceLapTime > 1.0)
            {
                candidateSource = "TIME";
                candidateTarget = timeRemaining;
                candidateTimed = true;
            }

            DateTime now = DateTime.UtcNow;
            if (string.IsNullOrEmpty(candidateSource))
            {
                // Hold the last valid projection during brief telemetry dropouts.
                if (projectionTargetLocked)
                {
                    if (projectionInvalidSinceUtc == DateTime.MinValue) projectionInvalidSinceUtc = now;
                    if ((now - projectionInvalidSinceUtc).TotalSeconds < 3.0) return;
                }
                ClearRaceProjection("NO RACE TARGET", "NO TARGET", false, false);
                return;
            }
            projectionInvalidSinceUtc = DateTime.MinValue;

            // Quantize the candidate key so normal countdown changes do not restart
            // the debounce timer. We validate the source/shape, not every decimal.
            string candidateKey = candidateSource + (candidateSource == "LAPS" ? ":DIRECT" : ":TIMED");
            if (!string.Equals(candidateKey, projectionCandidateKey, StringComparison.Ordinal))
            {
                projectionCandidateKey = candidateKey;
                projectionCandidateSinceUtc = now;
            }

            if (!projectionTargetLocked || !string.Equals(lockedProjectionSource, candidateSource, StringComparison.Ordinal))
            {
                if ((now - projectionCandidateSinceUtc).TotalSeconds < 2.0)
                {
                    // Keep the previous display while a new target proves stable.
                    return;
                }
                projectionTargetLocked = true;
                lockedProjectionSource = candidateSource;
                lockedProjectionTimed = candidateTimed;
                lockedProjectionLapLimited = candidateLapLimited;
                projectionRefreshRequested = true;
            }

            lockedProjectionTarget = candidateTarget;

            // Keep strategic numbers completely fixed between meaningful events.
            // Current fuel continues to update every frame, but race laps, fuel
            // required, finish margin and add-to-finish only change after a valid
            // lap closes or a new race target is accepted.
            if (!projectionRefreshRequested)
            {
                return;
            }
            lastProjectionEvaluationUtc = now;

            double lapsToFinish;
            if (lockedProjectionSource == "LAPS")
            {
                lapsToFinish = Math.Max(0.0, lockedProjectionTarget - Clamp01(trackFraction));
            }
            else
            {
                double finishCrossing = Math.Ceiling(Clamp01(trackFraction) + (lockedProjectionTarget / referenceLapTime));
                lapsToFinish = Math.Max(0.0, finishCrossing - Clamp01(trackFraction));
            }

            if (lapsToFinish <= 0.0 || lapsToFinish > 1000.0)
            {
                // Do not wipe a valid display for one anomalous evaluation.
                return;
            }

            double reserve = Math.Max(0.50, safeUsage * 0.25);
            double required = (lapsToFinish * safeUsage) + reserve;
            double margin = snapshot.CurrentFuel - required;

            ApplyProjectionFilter(lockedProjectionSource, lapsToFinish, required, margin);

            double marginalThreshold = Math.Max(0.50, safeUsage * 0.50);
            string projectionStatus = filteredProjectionMargin < 0.0
                ? "SHORT"
                : (filteredProjectionMargin < marginalThreshold ? "MARGINAL" : "SAFE");

            snapshot.EstimatedSessionLapsRemaining = Math.Round(filteredProjectionLaps, 1);
            snapshot.FuelRequiredToFinishLiters = Math.Round(filteredProjectionRequired, 2);
            snapshot.FinishMarginLiters = Math.Round(filteredProjectionMargin, 2);
            snapshot.ReserveLiters = Math.Round(reserve, 2);
            snapshot.FuelToAddLiters = Math.Round(Math.Max(0.0, snapshot.FuelRequiredToFinishLiters - snapshot.CurrentFuel), 2);
            snapshot.FillToLiters = Math.Round(Math.Min(snapshot.MaxFuel > 0.0 ? snapshot.MaxFuel : double.MaxValue,
                snapshot.CurrentFuel + snapshot.FuelToAddLiters), 2);
            snapshot.FuelStopsNeeded = snapshot.FuelToAddLiters <= 0.01 || snapshot.MaxFuel <= 0.01
                ? 0
                : Math.Max(1, (int)Math.Ceiling(snapshot.FuelToAddLiters / snapshot.MaxFuel));
            snapshot.PitStopsNeeded = snapshot.FuelStopsNeeded;
            snapshot.TotalStopsRemaining = Math.Max(snapshot.FuelStopsNeeded, snapshot.MandatoryStopsRemaining);
            snapshot.HasFinishEstimate = true;
            snapshot.EstimateSource = lockedProjectionSource;
            snapshot.ProjectionStatus = projectionStatus;
            snapshot.IsFuelShort = filteredProjectionMargin < 0.0;
            snapshot.IsTimedSession = lockedProjectionTimed;
            snapshot.IsLapLimitedSession = lockedProjectionLapLimited;
            projectionRefreshRequested = false;
        }

        private void UpdatePitWindowEngine(double trackFraction)
        {
            snapshot.PitWindowValid = false;
            snapshot.PitEarliestLap = 0;
            snapshot.PitOptimalLap = 0;
            snapshot.PitLatestLap = 0;
            snapshot.NextPitLap = 0;
            snapshot.PitWindowProgressPercent = 0.0;
            snapshot.PitWindowCurrentPositionPercent = 0.0;
            snapshot.PitWindowSpanLaps = 0;
            snapshot.LapsToOptimalPit = 0;

            if (!snapshot.HasFinishEstimate || snapshot.StrategyUsageLiters <= 0.0001 ||
                snapshot.MaxFuel <= 0.01 || snapshot.TotalStopsRemaining <= 0)
            {
                snapshot.PitWindowPhase = snapshot.HasFinishEstimate ? "NO STOP REQUIRED" : "UNAVAILABLE";
                snapshot.PitWindowStateCode = snapshot.HasFinishEstimate ? "NO_STOP" : "UNAVAILABLE";
                snapshot.PitWindowStateText = snapshot.HasFinishEstimate ? "NO STOP REQUIRED" : "WAITING";
                snapshot.PitWindowActionText = snapshot.HasFinishEstimate ? "FULL PUSH" : "NO RACE TARGET";
                snapshot.Recommendation = snapshot.PitWindowActionText;
                return;
            }

            double track = Clamp01(trackFraction);
            int completed = Math.Max(0, snapshot.CompletedLaps);
            int currentLap = completed + 1;
            int finishLap = currentLap + Math.Max(1, (int)Math.Ceiling(snapshot.EstimatedSessionLapsRemaining));
            snapshot.CurrentRaceLap = currentLap;
            snapshot.EstimatedFinishLap = finishLap;

            double finalStintReserve = Math.Max(snapshot.ReserveLiters, snapshot.StrategyUsageLiters * 0.20);
            double pitEntryReserve = Math.Max(snapshot.ReserveLiters, snapshot.StrategyUsageLiters * 0.45);
            double usableFullTank = Math.Max(0.0, snapshot.MaxFuel - finalStintReserve);
            double usableCurrentFuel = Math.Max(0.0, snapshot.CurrentFuel - pitEntryReserve);

            int fullTankCrossings = Math.Max(1, (int)Math.Floor(usableFullTank / snapshot.StrategyUsageLiters));

            // Physical reach is based on CURRENT fuel, not full-tank capacity and
            // not the amount that happened to be present at session start. Saving
            // fuel naturally extends this value because CurrentFuel / SAFE usage grows.
            double physicalStintLaps = usableCurrentFuel / snapshot.StrategyUsageLiters;
            double currentRacePosition = completed + track;
            int physicalLatest = Math.Max(currentLap, (int)Math.Floor(currentRacePosition + physicalStintLaps));
            snapshot.PhysicalStintLapsRemaining = Math.Round(physicalStintLaps, 2);
            snapshot.PhysicalLatestPitLap = physicalLatest;

            int earliest = Math.Max(currentLap, finishLap - fullTankCrossings);
            int latest = Math.Min(finishLap - 1, physicalLatest);

            // A multi-stop race can make the one-stop earliest calculation later
            // than the physical latest. In that case the current stint's physical
            // limit wins; the next stop must occur no later than that lap.
            if (earliest > latest) earliest = currentLap;
            if (latest < currentLap) latest = currentLap;
            if (earliest > latest) earliest = latest;
            if (latest == earliest && earliest > currentLap) earliest = Math.Max(currentLap, earliest - 1);

            int windowSpan = Math.Max(0, latest - earliest);
            int optimal = earliest + (int)Math.Round(windowSpan * 0.60);
            optimal = Math.Max(earliest, Math.Min(latest, optimal));

            snapshot.PitEarliestLap = earliest;
            snapshot.PitOptimalLap = optimal;
            snapshot.PitLatestLap = latest;
            snapshot.NextPitLap = optimal;
            snapshot.PitWindowValid = true;
            snapshot.PitWindowSpanLaps = windowSpan;
            snapshot.LapsToOptimalPit = optimal - currentLap;

            double liveLap = completed + track + 1.0;
            double displayStart = Math.Max(currentLap, earliest - 1.0);
            double displayEnd = Math.Max(displayStart + 1.0, latest + 0.999);
            snapshot.PitWindowCurrentPositionPercent = Math.Max(0.0, Math.Min(100.0,
                ((liveLap - displayStart) / (displayEnd - displayStart)) * 100.0));
            snapshot.PitWindowProgressPercent = Math.Max(0.0, Math.Min(100.0,
                ((liveLap - earliest) / Math.Max(1.0, latest - earliest + 1.0)) * 100.0));

            int lapsToOptimal = optimal - currentLap;
            int lapsToLatest = latest - currentLap;
            if (currentLap < earliest)
            {
                snapshot.PitWindowPhase = "BEFORE WINDOW";
                snapshot.PitWindowStateCode = "CLOSED";
                snapshot.PitWindowStateText = "WINDOW CLOSED";
                snapshot.PitWindowActionText = FormatBoxRecommendation(lapsToOptimal);
            }
            else if (currentLap > latest)
            {
                snapshot.PitWindowPhase = "WINDOW MISSED";
                snapshot.PitWindowStateCode = "MISSED";
                snapshot.PitWindowStateText = "PIT NOW";
                snapshot.PitWindowActionText = "WINDOW MISSED";
            }
            else if (lapsToLatest <= 2)
            {
                snapshot.PitWindowPhase = currentLap == latest ? "LATEST LAP" : "WINDOW CLOSING";
                snapshot.PitWindowStateCode = "CLOSING";
                snapshot.PitWindowStateText = "WINDOW CLOSING";
                snapshot.PitWindowActionText = currentLap == latest ? "PIT NOW" : "RECOMMENDED THIS LAP";
            }
            else
            {
                snapshot.PitWindowPhase = currentLap == optimal ? "OPTIMAL LAP" : "WINDOW OPEN";
                snapshot.PitWindowStateCode = "OPEN";
                snapshot.PitWindowStateText = "WINDOW OPEN";
                snapshot.PitWindowActionText = currentLap < optimal ? FormatBoxRecommendation(lapsToOptimal) : "BOX ANYTIME";
            }
            snapshot.Recommendation = snapshot.PitWindowActionText;
        }

        private static string FormatBoxRecommendation(int lapsToOptimal)
        {
            if (lapsToOptimal <= 0) return "BOX NOW";
            if (lapsToOptimal == 1) return "BOX NEXT LAP";
            return "BOX IN " + lapsToOptimal.ToString(CultureInfo.InvariantCulture) + " LAPS";
        }

        private void UpdateFuelCoach()
        {
            snapshot.FuelCoachTargetLiters = Math.Round(Math.Max(0.0, snapshot.StrategyUsageLiters), 2);
            double actual = snapshot.RecentUsageLiters > 0.0001
                ? snapshot.RecentUsageLiters
                : snapshot.AverageUsageLiters;
            snapshot.FuelCoachActualLiters = Math.Round(Math.Max(0.0, actual), 2);
            snapshot.FuelCoachDeltaLiters = Math.Round(actual - snapshot.StrategyUsageLiters, 2);
            snapshot.FuelCoachBufferLiters = 0.0;

            // Pit execution always has priority over consumption coaching.
            if (snapshot.PitWindowStateCode == "MISSED")
            {
                snapshot.FuelCoachStateCode = "PIT_NOW";
                snapshot.FuelCoachStateText = "PIT NOW";
                snapshot.FuelCoachActionText = "WINDOW MISSED";
                return;
            }
            if (snapshot.PitWindowStateCode == "CLOSING")
            {
                snapshot.FuelCoachStateCode = "WINDOW_CLOSING";
                snapshot.FuelCoachStateText = "WINDOW CLOSING";
                snapshot.FuelCoachActionText = snapshot.PitWindowActionText;
                return;
            }
            if (snapshot.PitWindowStateCode == "OPEN")
            {
                snapshot.FuelCoachStateCode = "WINDOW_OPEN";
                snapshot.FuelCoachStateText = "WINDOW OPEN";
                snapshot.FuelCoachActionText = snapshot.PitWindowActionText;
                return;
            }

            if (!snapshot.Ready || snapshot.ValidSampleCount <= 0 || snapshot.StrategyUsageLiters <= 0.0001)
            {
                snapshot.FuelCoachStateCode = "WAITING";
                snapshot.FuelCoachStateText = "LEARNING";
                snapshot.FuelCoachActionText = "COMPLETE CLEAN LAPS";
                return;
            }

            if (string.Equals(snapshot.StrategyPlanStatus, "PLAN INFEASIBLE", StringComparison.Ordinal) ||
                string.Equals(snapshot.StrategyPlanStatus, "NO PHYSICAL WINDOW", StringComparison.Ordinal))
            {
                snapshot.FuelCoachStateCode = "UNREACHABLE";
                snapshot.FuelCoachStateText = "FUEL TARGET UNREACHABLE";
                snapshot.FuelCoachActionText = "PIT EARLIER OR ADD FUEL";
                return;
            }

            // PLAN is the conservative consumption value used by Strategy Engine.
            // Compare it with the recent real-world rate, not a single noisy lap.
            double tolerance = Math.Max(0.02, snapshot.StrategyUsageLiters * 0.010);
            double meaningfulBuffer = Math.Max(0.04, snapshot.StrategyUsageLiters * 0.020);
            double delta = actual - snapshot.StrategyUsageLiters;

            if (delta > tolerance)
            {
                double save = Math.Round(delta, 2);
                double saveRatio = snapshot.StrategyUsageLiters > 0.0001 ? save / snapshot.StrategyUsageLiters : 0.0;
                if (saveRatio > 0.12)
                {
                    snapshot.FuelCoachStateCode = "UNREACHABLE";
                    snapshot.FuelCoachStateText = "FUEL TARGET UNREACHABLE";
                    snapshot.FuelCoachActionText = "PIT EARLIER OR ADD FUEL";
                }
                else
                {
                    snapshot.FuelCoachStateCode = "SAVE";
                    snapshot.FuelCoachStateText = "SAVE " + save.ToString("0.00", CultureInfo.InvariantCulture) + " L/LAP";
                    snapshot.FuelCoachActionText = saveRatio >= 0.06 ? "SIGNIFICANTLY REDUCE CONSUMPTION" : "REDUCE CONSUMPTION TO HIT PLAN";
                }
                return;
            }

            if (delta < -meaningfulBuffer)
            {
                int laps = snapshot.PitWindowValid
                    ? Math.Max(1, snapshot.LapsToOptimalPit)
                    : Math.Max(1, Math.Min(10, (int)Math.Ceiling(snapshot.DisplayLapsRemaining)));
                double buffer = Math.Max(0.0, -delta * laps);
                snapshot.FuelCoachBufferLiters = Math.Round(buffer, 2);
                snapshot.FuelCoachStateCode = "TARGET_REACHED";
                snapshot.FuelCoachStateText = "TARGET REACHED";
                snapshot.FuelCoachActionText = "BUFFER +" + snapshot.FuelCoachBufferLiters.ToString("0.00", CultureInfo.InvariantCulture) + " L";
                return;
            }

            if (!snapshot.IsFuelShort || snapshot.PitWindowStateCode == "NO_STOP")
            {
                snapshot.FuelCoachStateCode = "FULL_PUSH";
                snapshot.FuelCoachStateText = "FULL PUSH";
                snapshot.FuelCoachActionText = "NO NEED TO SAVE FUEL";
                return;
            }

            snapshot.FuelCoachStateCode = "ON_TARGET";
            snapshot.FuelCoachStateText = "ON TARGET";
            snapshot.FuelCoachActionText = "KEEP CURRENT CONSUMPTION";
        }

        private void UpdateDriverFacingStrategyStatus()
        {
            snapshot.StopsCompleted = Math.Max(0, strategyStopsCompleted);

            int remaining = Math.Max(0, snapshot.TotalStopsRemaining);
            int total = snapshot.StopsCompleted + remaining;
            snapshot.PlannedStopsTotal = total;

            if (!snapshot.HasFinishEstimate)
            {
                snapshot.NextStopNumber = 0;
                snapshot.StopProgressText = "NO RACE PLAN";
            }
            else if (remaining <= 0)
            {
                snapshot.NextStopNumber = 0;
                snapshot.StopProgressText = snapshot.StopsCompleted > 0 ? "STOPS COMPLETE" : "NO STOP REQUIRED";
            }
            else
            {
                snapshot.NextStopNumber = snapshot.StopsCompleted + 1;
                snapshot.StopProgressText = "NEXT STOP " +
                    snapshot.NextStopNumber.ToString(CultureInfo.InvariantCulture) + " OF " +
                    total.ToString(CultureInfo.InvariantCulture);
            }

            double estimatedRemainingSeconds = Math.Max(0.0, snapshot.EstimatedSessionLapsRemaining * snapshot.StrategyLapTimeSeconds);
            bool endurance = total >= 2 || estimatedRemainingSeconds >= 3600.0;
            snapshot.RaceFormatText = endurance ? "ENDURANCE RACE" : "SPRINT RACE";

            if (snapshot.AdaptiveModelActive)
            {
                snapshot.EngineerStateText = "ADAPTING TO CONDITIONS";
                snapshot.ConditionsDisplayText = "CONDITIONS CHANGING";
            }
            else if (snapshot.ValidSampleCount < 3)
            {
                snapshot.EngineerStateText = "LEARNING";
                snapshot.ConditionsDisplayText = "CONDITIONS STABLE";
            }
            else
            {
                snapshot.EngineerStateText = "READY";
                snapshot.ConditionsDisplayText = "CONDITIONS STABLE";
            }

            if (snapshot.ValidSampleCount < 3)
            {
                snapshot.ConfidenceDisplayText = "LEARNING (" +
                    snapshot.ValidSampleCount.ToString(CultureInfo.InvariantCulture) + " LAPS)";
            }
            else
            {
                snapshot.ConfidenceDisplayText = snapshot.Confidence + " CONFIDENCE";
            }

            if (snapshot.AdaptiveModelActive)
            {
                snapshot.HeaderStatusText = "ADAPTING TO CONDITIONS • " + snapshot.ConfidenceDisplayText + " • " + snapshot.RaceFormatText;
            }
            else if (snapshot.ValidSampleCount < 3)
            {
                snapshot.HeaderStatusText = snapshot.ConfidenceDisplayText + " • " + snapshot.RaceFormatText;
            }
            else
            {
                snapshot.HeaderStatusText = "READY • " + snapshot.ConfidenceDisplayText + " • " + snapshot.RaceFormatText;
            }
        }

        private void SynchronizeStateModels()
        {
            snapshot.FuelState.CurrentLiters = snapshot.CurrentFuel;
            snapshot.FuelState.CapacityLiters = snapshot.MaxFuel;
            snapshot.FuelState.Percent = snapshot.FuelPercent;
            snapshot.FuelState.LapsRemaining = snapshot.FuelLapsRemaining;
            snapshot.FuelState.TimeRemainingSeconds = snapshot.FuelTimeRemainingSeconds;

            snapshot.ConsumptionState.LastLiters = snapshot.LastLapUsageLiters;
            snapshot.ConsumptionState.AverageLiters = snapshot.AverageUsageLiters;
            snapshot.ConsumptionState.SafeLiters = snapshot.StrategyUsageLiters;
            snapshot.ConsumptionState.ExtraLapTargetLiters = snapshot.ExtraLapTargetUsageLiters;
            snapshot.ConsumptionState.Trend = snapshot.UsageTrendStatus;
            snapshot.ConsumptionState.Confidence = snapshot.Confidence;
            snapshot.ConsumptionState.ConfidencePercent = snapshot.ConfidencePercent;

            snapshot.StrategyState.HasRaceTarget = snapshot.HasFinishEstimate;
            snapshot.StrategyState.RaceLapsRemaining = snapshot.EstimatedSessionLapsRemaining;
            snapshot.StrategyState.FuelRequiredLiters = snapshot.FuelRequiredToFinishLiters;
            snapshot.StrategyState.FuelToAddLiters = snapshot.FuelToAddLiters;
            snapshot.StrategyState.FinishMarginLiters = snapshot.FinishMarginLiters;
            snapshot.StrategyState.StopsRemaining = snapshot.TotalStopsRemaining;
            snapshot.StrategyState.PitWindowValid = snapshot.PitWindowValid;
            snapshot.StrategyState.EarliestLap = snapshot.PitEarliestLap;
            snapshot.StrategyState.OptimalLap = snapshot.PitOptimalLap;
            snapshot.StrategyState.LatestLap = snapshot.PitLatestLap;
            snapshot.StrategyState.PitWindowPhase = snapshot.PitWindowPhase;
            snapshot.StrategyState.Recommendation = snapshot.Recommendation;
            snapshot.StrategyState.WindowPositionPercent = snapshot.PitWindowCurrentPositionPercent;
            snapshot.StrategyState.WindowSpanLaps = snapshot.PitWindowSpanLaps;
            snapshot.StrategyState.PlannedStintsRemaining = snapshot.PlannedStintsRemaining;
            snapshot.StrategyState.CurrentStintTargetLaps = snapshot.CurrentStintTargetLaps;
            snapshot.StrategyState.NextStintTargetLaps = snapshot.NextStintTargetLaps;
            snapshot.StrategyState.NextStopFuelToAddLiters = snapshot.NextStopFuelToAddLiters;
            snapshot.StrategyState.TotalFuelDeficitLiters = snapshot.TotalFuelDeficitLiters;
            snapshot.StrategyState.PlanStatus = snapshot.StrategyPlanStatus;
            snapshot.StrategyState.StopsCompleted = snapshot.StopsCompleted;
            snapshot.StrategyState.PlannedStopsTotal = snapshot.PlannedStopsTotal;
            snapshot.StrategyState.NextStopNumber = snapshot.NextStopNumber;
            snapshot.StrategyState.StopProgressText = snapshot.StopProgressText;
            snapshot.StrategyState.RaceFormatText = snapshot.RaceFormatText;
            snapshot.StrategyState.EngineerStateText = snapshot.EngineerStateText;
            snapshot.StrategyState.HeaderStatusText = snapshot.HeaderStatusText;
            snapshot.StrategyState.FuelCoachStateCode = snapshot.FuelCoachStateCode;
            snapshot.StrategyState.FuelCoachStateText = snapshot.FuelCoachStateText;
            snapshot.StrategyState.FuelCoachActionText = snapshot.FuelCoachActionText;
            snapshot.StrategyState.FuelCoachTargetLiters = snapshot.FuelCoachTargetLiters;
            snapshot.StrategyState.FuelCoachActualLiters = snapshot.FuelCoachActualLiters;
            snapshot.StrategyState.FuelCoachDeltaLiters = snapshot.FuelCoachDeltaLiters;
            snapshot.StrategyState.FuelCoachBufferLiters = snapshot.FuelCoachBufferLiters;

            snapshot.DebugState.ValidLaps = snapshot.ValidSampleCount;
            snapshot.DebugState.RejectedLaps = snapshot.RejectedSampleCount;
            snapshot.DebugState.LastRejectReason = snapshot.LastSampleRejectReason;
            snapshot.DebugState.ProjectionSource = snapshot.EstimateSource;
        }

        private static string NormalizeSessionLabel(string sessionType)
        {
            if (string.IsNullOrWhiteSpace(sessionType)) return "NO TARGET";
            string value = sessionType.Trim().ToUpperInvariant();
            if (value.Contains("PRACTICE")) return "PRACTICE";
            if (value.Contains("QUALIFY")) return "QUALY";
            if (value.Contains("WARM")) return "WARMUP";
            if (value.Contains("TEST")) return "TEST";
            return value.Length > 12 ? value.Substring(0, 12) : value;
        }

        private void ApplyProjectionFilter(string source, double laps, double required, double margin)
        {
            if (!projectionFilterInitialized || !string.Equals(filteredProjectionSource, source, StringComparison.Ordinal))
            {
                projectionFilterInitialized = true;
                filteredProjectionSource = source ?? string.Empty;
                filteredProjectionLaps = laps;
                filteredProjectionRequired = required;
                filteredProjectionMargin = margin;
                return;
            }

            const double alpha = 0.12;
            filteredProjectionLaps += (laps - filteredProjectionLaps) * alpha;
            filteredProjectionRequired += (required - filteredProjectionRequired) * alpha;
            filteredProjectionMargin += (margin - filteredProjectionMargin) * alpha;
        }

        private void ClearRaceProjection(string status, string source, bool timed, bool lapLimited)
        {
            snapshot.HasFinishEstimate = false;
            snapshot.EstimatedSessionLapsRemaining = 0.0;
            snapshot.FuelRequiredToFinishLiters = 0.0;
            snapshot.FinishMarginLiters = 0.0;
            snapshot.ReserveLiters = 0.0;
            snapshot.FuelToAddLiters = 0.0;
            snapshot.FillToLiters = 0.0;
            snapshot.FuelStopsNeeded = 0;
            snapshot.PitStopsNeeded = 0;
            snapshot.TotalStopsRemaining = snapshot.MandatoryStopsRemaining;
            snapshot.EstimateSource = source;
            snapshot.ProjectionStatus = status;
            snapshot.IsFuelShort = false;
            snapshot.IsTimedSession = timed;
            snapshot.IsLapLimitedSession = lapLimited;
            ResetProjectionFilter();
        }

        private void ResetProjectionFilter()
        {
            projectionFilterInitialized = false;
            filteredProjectionLaps = 0.0;
            filteredProjectionRequired = 0.0;
            filteredProjectionMargin = 0.0;
            filteredProjectionSource = string.Empty;
            projectionCandidateKey = string.Empty;
            projectionCandidateSinceUtc = DateTime.MinValue;
            projectionInvalidSinceUtc = DateTime.MinValue;
            lastProjectionEvaluationUtc = DateTime.MinValue;
            projectionTargetLocked = false;
            lockedProjectionSource = string.Empty;
            lockedProjectionTarget = 0.0;
            lockedProjectionTimed = false;
            lockedProjectionLapLimited = false;
            projectionRefreshRequested = true;
            cachedSessionType = string.Empty;
        }

        private static bool IsReasonableLapTarget(int value)
        {
            return value > 0 && value < 1000 && value < 32760;
        }

        private static bool IsReasonableTimeTarget(double value)
        {
            // Supports endurance races up to 48 hours while rejecting unlimited or
            // corrupt timer sentinels.
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0.0 && value <= 172800.0;
        }

        private static bool IsNonRaceSession(string sessionType)
        {
            if (string.IsNullOrWhiteSpace(sessionType)) return false;
            string value = sessionType.Trim().ToUpperInvariant();
            return value.Contains("PRACTICE") ||
                   value.Contains("TEST") ||
                   value.Contains("WARMUP") ||
                   value.Contains("WARM UP") ||
                   value.Contains("QUALIFY");
        }

        private static double TrimmedAverage(IList<double> values)
        {
            if (values == null || values.Count == 0) return 0.0;
            var ordered = values.OrderBy(v => v).ToList();
            int trim = ordered.Count >= 6 ? 1 : 0;
            if (ordered.Count >= 14) trim = Math.Max(1, ordered.Count / 10);
            int start = trim;
            int end = ordered.Count - trim;
            if (end <= start) return ordered.Average();
            double sum = 0.0;
            for (int i = start; i < end; i++) sum += ordered[i];
            return sum / (end - start);
        }

        private static double Percentile(IList<double> sorted, double percentile)
        {
            if (sorted == null || sorted.Count == 0) return 0.0;
            if (sorted.Count == 1) return sorted[0];
            double position = (sorted.Count - 1) * percentile;
            int lower = (int)Math.Floor(position);
            int upper = (int)Math.Ceiling(position);
            if (lower == upper) return sorted[lower];
            double fraction = position - lower;
            return sorted[lower] + ((sorted[upper] - sorted[lower]) * fraction);
        }

        private void UpdateScheduled()
        {
            if (!gameRunning || rawData == null || telemetry == null)
            {
                calculator.Reset(snapshot);
                publisher.Publish(snapshot);
                return;
            }

            double fuelLiters = FirstPositive(
                ReadNumber(rawData, "Fuel"),
                ReadNumber(rawData, "FuelLevel"),
                telemetry.FuelLevelLiters);

            double capacityLiters = FirstPositive(
                ReadNumber(rawData, "MaxFuel"),
                ReadNumber(rawData, "FuelCapacity"),
                ReadNumber(rawData, "DriverCarFuelMaxLtr"));

            double fuelPercent = FirstPositive(
                NormalizePercent(ReadNumber(rawData, "FuelPercent")),
                NormalizePercent(ReadNumber(rawData, "FuelLevelPct")),
                telemetry.FuelLevelPercent);

            if (capacityLiters <= 0.0 && fuelLiters > 0.0 && fuelPercent > 0.0001)
            {
                capacityLiters = fuelLiters / fuelPercent;
            }

            if (fuelPercent <= 0.0 && fuelLiters > 0.0 && capacityLiters > 0.0)
            {
                fuelPercent = fuelLiters / capacityLiters;
            }

            if (fuelLiters <= 0.0 || capacityLiters <= 0.0)
            {
                double rawFuelLiters;
                double rawFuelPercent;
                double rawCapacityLiters;

                if (reader.TryRead(rawData, out rawFuelLiters, out rawFuelPercent, out rawCapacityLiters))
                {
                    if (fuelLiters <= 0.0) fuelLiters = rawFuelLiters;
                    if (fuelPercent <= 0.0) fuelPercent = rawFuelPercent;
                    if (capacityLiters <= 0.0) capacityLiters = rawCapacityLiters;
                }
            }

            if (fuelLiters <= 0.0 && capacityLiters <= 0.0)
            {
                snapshot.Ready = false;
                snapshot.InstantDataReady = false;
                snapshot.EngineState = "NO DATA";
                snapshot.Status = "NO DATA";
                snapshot.Summary = "FUEL TELEMETRY UNAVAILABLE";
                snapshot.Error = "Fuel and MaxFuel were not found";
                publisher.Publish(snapshot);
                return;
            }

            // Use SimHub's normalized GameData lap counters as the authoritative
            // source for fuel-lap sampling. These are the same properties exposed
            // to dashboards as DataCorePlugin.GameData.CompletedLaps and Lap.
            TelemetrySnapshot fuelTelemetry = CloneTelemetry(telemetry);
            int normalizedCompletedLaps = ReadInt(rawData, "CompletedLaps", -1);
            int normalizedLap = ReadInt(rawData, "Lap", -1);

            if (normalizedCompletedLaps >= 0)
            {
                fuelTelemetry.LapCompleted = normalizedCompletedLaps;
            }
            if (normalizedLap >= 0)
            {
                fuelTelemetry.Lap = normalizedLap;
            }

            bool? normalizedPit = FirstBool(rawData,
                "IsInPitLane",
                "IsInPit",
                "InPitLane",
                "OnPitRoad");
            if (normalizedPit.HasValue)
            {
                fuelTelemetry.IsOnPitRoad = normalizedPit.Value;
            }

            double normalizedTrackPosition = FirstFinite(
                ReadNumber(rawData, "TrackPositionPercent"),
                ReadNumber(rawData, "LapDistPct"));
            if (!double.IsNaN(normalizedTrackPosition))
            {
                if (normalizedTrackPosition > 1.5) normalizedTrackPosition /= 100.0;
                if (normalizedTrackPosition >= 0.0 && normalizedTrackPosition <= 1.0)
                {
                    fuelTelemetry.LapDistancePercent = (float)normalizedTrackPosition;
                }
            }

            calculator.Update(fuelTelemetry, fuelLiters, fuelPercent, capacityLiters, snapshot);
            publisher.Publish(snapshot);
        }

        private static TelemetrySnapshot CloneTelemetry(TelemetrySnapshot source)
        {
            return new TelemetrySnapshot
            {
                CapturedAt = source.CapturedAt,
                GameRunning = source.GameRunning,
                GameName = source.GameName,
                SessionType = source.SessionType,
                SessionTime = source.SessionTime,
                SessionTimeRemaining = source.SessionTimeRemaining,
                SessionNumber = source.SessionNumber,
                SessionState = source.SessionState,
                SessionFlags = source.SessionFlags,
                SessionLapsRemaining = source.SessionLapsRemaining,
                SessionLapsTotal = source.SessionLapsTotal,
                PlayerCarIndex = source.PlayerCarIndex,
                PlayerPosition = source.PlayerPosition,
                PlayerClassPosition = source.PlayerClassPosition,
                PlayerClassId = source.PlayerClassId,
                Lap = source.Lap,
                LapCompleted = source.LapCompleted,
                LapDistancePercent = source.LapDistancePercent,
                SpeedMetersPerSecond = source.SpeedMetersPerSecond,
                Throttle = source.Throttle,
                Brake = source.Brake,
                Clutch = source.Clutch,
                Gear = source.Gear,
                Rpm = source.Rpm,
                IsOnTrack = source.IsOnTrack,
                IsOnPitRoad = source.IsOnPitRoad,
                IsReplayPlaying = source.IsReplayPlaying,
                TrackTemperatureCelsius = source.TrackTemperatureCelsius,
                AirTemperatureCelsius = source.AirTemperatureCelsius,
                FuelLevelLiters = source.FuelLevelLiters,
                FuelLevelPercent = source.FuelLevelPercent
            };
        }

        private static double ReadNumber(object target, string propertyName)
        {
            object value = ReadValue(target, propertyName);
            if (value == null) return 0.0;
            try { return Convert.ToDouble(value, CultureInfo.InvariantCulture); }
            catch { return 0.0; }
        }

        private static int ReadInt(object target, string propertyName, int fallback)
        {
            object value = ReadValue(target, propertyName);
            if (value == null) return fallback;
            try { return Convert.ToInt32(value, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }

        private static bool? FirstBool(object target, params string[] propertyNames)
        {
            for (int i = 0; i < propertyNames.Length; i++)
            {
                object value = ReadValue(target, propertyNames[i]);
                if (value == null) continue;
                try { return Convert.ToBoolean(value, CultureInfo.InvariantCulture); }
                catch { }
            }
            return null;
        }

        private static object ReadValue(object target, string propertyName)
        {
            if (target == null || string.IsNullOrEmpty(propertyName)) return null;
            try
            {
                PropertyInfo property = target.GetType().GetProperty(
                    propertyName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (property != null && property.CanRead)
                {
                    return property.GetValue(target, null);
                }
            }
            catch { }
            return null;
        }

        private static double FirstPositive(params double[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (!double.IsNaN(values[i]) && !double.IsInfinity(values[i]) && values[i] > 0.0)
                    return values[i];
            }
            return 0.0;
        }

        private static double FirstFinite(params double[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (!double.IsNaN(values[i]) && !double.IsInfinity(values[i]) && values[i] != 0.0)
                    return values[i];
            }
            return double.NaN;
        }

        private static double Clamp01(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 0.0;
            if (value < 0.0) return 0.0;
            if (value > 1.0) return 1.0;
            return value;
        }

        private static double NormalizePercent(double value)
        {
            if (value > 1.5) value /= 100.0;
            return Clamp01(value);
        }
    }
}
