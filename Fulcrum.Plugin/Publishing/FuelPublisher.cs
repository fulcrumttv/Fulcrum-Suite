using System;
using Fulcrum.Core.Fuel;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Publishing
{
    public sealed class FuelPublisher
    {
        private const string Prefix = "Fulcrum.Fuel.";
        private readonly PluginManager pluginManager;
        private readonly Type pluginType;

        public FuelPublisher(PluginManager pluginManager, Type pluginType)
        {
            this.pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
            this.pluginType = pluginType ?? throw new ArgumentNullException(nameof(pluginType));
            RegisterProperties();
        }

        public void Publish(FuelSnapshot v)
        {
            Set("Ready", v.Ready);
            // Fuel Core v3 direct aliases used by the telemetry explorer.
            Set("CurrentFuel", v.CurrentFuel);
            Set("MaxFuel", v.MaxFuel);
            Set("FuelPercent", v.FuelPercent);
            Set("CompletedLaps", v.CompletedLaps);
            Set("CurrentLapTelemetry", v.CurrentLapTelemetry);
            Set("TrackPositionPercent", v.TrackPositionPercent);
            Set("IsInPit", v.IsInPit);
            Set("IsInPitLane", v.IsInPitLane);
            Set("BasicFrameCounter", v.BasicFrameCounter);
            Set("BasicSource", v.BasicSource);
            Set("TrackerInitialized", v.TrackerInitialized);
            Set("TrackerStatus", v.TrackerStatus);
            Set("TrackerLapStartNumber", v.TrackerLapStartNumber);
            Set("TrackerLapStartFuel", v.TrackerLapStartFuel);
            Set("TrackerLastClosedLapNumber", v.TrackerLastClosedLapNumber);
            Set("TrackerLastClosedLapStartFuel", v.TrackerLastClosedLapStartFuel);
            Set("TrackerLastClosedLapEndFuel", v.TrackerLastClosedLapEndFuel);
            Set("TrackerLastClosedLapUsage", v.TrackerLastClosedLapUsage);
            Set("TrackerLastClosedLapValid", v.TrackerLastClosedLapValid);
            Set("TrackerLastClosedLapReason", v.TrackerLastClosedLapReason);
            Set("TrackerValidLapCount", v.TrackerValidLapCount);
            Set("TrackerRejectedLapCount", v.TrackerRejectedLapCount);
            Set("InstantDataReady", v.InstantDataReady);
            Set("EngineState", v.EngineState);
            Set("LearningProgressText", v.LearningProgressText);
            Set("LevelLiters", v.FuelLevelLiters);
            Set("LevelPercent", v.FuelLevelPercent * 100.0);
            Set("CapacityLiters", v.FuelCapacityLiters);

            Set("LastLapUsageLiters", v.LastLapUsageLiters);
            Set("AverageUsageLiters", v.AverageUsageLiters);
            Set("MedianUsageLiters", v.MedianUsageLiters);
            Set("RecentUsageLiters", v.RecentUsageLiters);
            Set("ConservativeUsageLiters", v.ConservativeUsageLiters);
            Set("StrategyUsageLiters", v.StrategyUsageLiters);
            Set("BestUsageLiters", v.BestUsageLiters);
            Set("WorstUsageLiters", v.WorstUsageLiters);
            Set("LapsRemaining", v.FuelLapsRemaining);
            Set("WholeLapsRemaining", v.WholeLapsRemaining);
            Set("FuelAfterNextLapLiters", v.FuelAfterNextLapLiters);
            Set("StintRemainderLiters", v.StintRemainderLiters);
            Set("UsageTrendLitersPerLap", v.UsageTrendLitersPerLap);
            Set("UsageTrendStatus", v.UsageTrendStatus);

            Set("ValidSampleCount", v.ValidSampleCount);
            Set("RejectedSampleCount", v.RejectedSampleCount);
            Set("LastSampleRejected", v.LastSampleRejected);
            Set("LastSampleRejectReason", v.LastSampleRejectReason);

            Set("AverageLapTimeSeconds", v.AverageLapTimeSeconds);
            Set("MedianLapTimeSeconds", v.MedianLapTimeSeconds);
            Set("RecentLapTimeSeconds", v.RecentLapTimeSeconds);
            Set("ConservativeLapTimeSeconds", v.ConservativeLapTimeSeconds);
            Set("StrategyLapTimeSeconds", v.StrategyLapTimeSeconds);
            Set("LapTimeSource", v.LapTimeSource);
            Set("DisplayLapsRemaining", v.DisplayLapsRemaining);
            Set("FuelTimeRemainingSeconds", v.FuelTimeRemainingSeconds);
            Set("SessionLapsRemaining", v.EstimatedSessionLapsRemaining);

            Set("RequiredToFinishLiters", v.FuelRequiredToFinishLiters);
            Set("ToAddLiters", v.FuelToAddLiters);
            Set("FillToLiters", v.FillToLiters);
            Set("FinishMarginLiters", v.FinishMarginLiters);
            Set("ReserveLiters", v.ReserveLiters);
            Set("TargetUsageLiters", v.TargetUsageLiters);
            Set("SavePerLapLiters", v.SavePerLapLiters);
            Set("ExtraLapTargetUsageLiters", v.ExtraLapTargetUsageLiters);
            Set("SaveForExtraLapLiters", v.SaveForExtraLapLiters);
            Set("LapsShort", v.LapsShort);
            Set("TanksNeeded", v.TanksNeeded);

            Set("PitStopsNeeded", v.PitStopsNeeded);
            Set("FuelStopsNeeded", v.FuelStopsNeeded);
            Set("MandatoryStopsRequired", v.MandatoryStopsRequired);
            Set("MandatoryStopsCompleted", v.MandatoryStopsCompleted);
            Set("MandatoryStopsRemaining", v.MandatoryStopsRemaining);
            Set("TotalStopsRemaining", v.TotalStopsRemaining);
            Set("MandatoryStopsSource", v.MandatoryStopsSource);
            Set("CurrentRaceLap", v.CurrentRaceLap);
            Set("EstimatedFinishLap", v.EstimatedFinishLap);
            Set("PitEarliestLap", v.PitEarliestLap);
            Set("PitOptimalLap", v.PitOptimalLap);
            Set("PitLatestLap", v.PitLatestLap);
            Set("PitWindowValid", v.PitWindowValid);
            Set("NextPitLap", v.NextPitLap);
            Set("PitWindowPhase", v.PitWindowPhase);
            Set("PitWindowStateCode", v.PitWindowStateCode);
            Set("PitWindowStateText", v.PitWindowStateText);
            Set("PitWindowActionText", v.PitWindowActionText);
            Set("Recommendation", v.Recommendation);
            Set("PitWindowProgressPercent", v.PitWindowProgressPercent);
            Set("PitWindowCurrentPositionPercent", v.PitWindowCurrentPositionPercent);
            Set("PitWindowSpanLaps", v.PitWindowSpanLaps);
            Set("LapsToOptimalPit", v.LapsToOptimalPit);
            Set("AverageBasisText", v.AverageBasisText);
            Set("StintStartFuelLiters", v.StintStartFuelLiters);
            Set("StintStartLap", v.StintStartLap);
            Set("FuelAddedThisStopLiters", v.FuelAddedThisStopLiters);
            Set("CurrentStintFuelUsedLiters", v.CurrentStintFuelUsedLiters);
            Set("PhysicalLatestPitLap", v.PhysicalLatestPitLap);
            Set("PhysicalStintLapsRemaining", v.PhysicalStintLapsRemaining);
            Set("PlannedStintsRemaining", v.PlannedStintsRemaining);
            Set("CurrentStintTargetLaps", v.CurrentStintTargetLaps);
            Set("NextStintTargetLaps", v.NextStintTargetLaps);
            Set("NextStopFuelToAddLiters", v.NextStopFuelToAddLiters);
            Set("TotalFuelDeficitLiters", v.TotalFuelDeficitLiters);
            Set("StrategyPlanStatus", v.StrategyPlanStatus);
            Set("StopsCompleted", v.StopsCompleted);
            Set("PlannedStopsTotal", v.PlannedStopsTotal);
            Set("NextStopNumber", v.NextStopNumber);
            Set("StopProgressText", v.StopProgressText);
            Set("RaceFormatText", v.RaceFormatText);
            Set("EngineerStateText", v.EngineerStateText);
            Set("ConfidenceDisplayText", v.ConfidenceDisplayText);
            Set("ConditionsDisplayText", v.ConditionsDisplayText);
            Set("HeaderStatusText", v.HeaderStatusText);
            Set("FuelCoachStateCode", v.FuelCoachStateCode);
            Set("FuelCoachStateText", v.FuelCoachStateText);
            Set("FuelCoachActionText", v.FuelCoachActionText);
            Set("FuelCoachTargetLiters", v.FuelCoachTargetLiters);
            Set("FuelCoachActualLiters", v.FuelCoachActualLiters);
            Set("FuelCoachDeltaLiters", v.FuelCoachDeltaLiters);
            Set("FuelCoachBufferLiters", v.FuelCoachBufferLiters);
            Set("AdaptiveModelActive", v.AdaptiveModelActive);
            Set("ModelStatusText", v.ModelStatusText);
            Set("ConditionsStatus", v.ConditionsStatus);
            Set("RecentVsStintDeltaPercent", v.RecentVsStintDeltaPercent);
            Set("RecentLapTimeDeltaPercent", v.RecentLapTimeDeltaPercent);
            Set("AdaptiveStrategyUsageLiters", v.AdaptiveStrategyUsageLiters);
            Set("RelevantSampleCount", v.RelevantSampleCount);

            // State-oriented aliases introduced in Fuel Engineer Phase 1.
            Set("State.Fuel.CurrentLiters", v.FuelState.CurrentLiters);
            Set("State.Fuel.CapacityLiters", v.FuelState.CapacityLiters);
            Set("State.Fuel.Percent", v.FuelState.Percent);
            Set("State.Fuel.LapsRemaining", v.FuelState.LapsRemaining);
            Set("State.Fuel.TimeRemainingSeconds", v.FuelState.TimeRemainingSeconds);
            Set("State.Consumption.LastLiters", v.ConsumptionState.LastLiters);
            Set("State.Consumption.AverageLiters", v.ConsumptionState.AverageLiters);
            Set("State.Consumption.SafeLiters", v.ConsumptionState.SafeLiters);
            Set("State.Consumption.ExtraLapTargetLiters", v.ConsumptionState.ExtraLapTargetLiters);
            Set("State.Strategy.FuelToAddLiters", v.StrategyState.FuelToAddLiters);
            Set("State.Strategy.FinishMarginLiters", v.StrategyState.FinishMarginLiters);
            Set("State.Strategy.StopsRemaining", v.StrategyState.StopsRemaining);
            Set("State.Strategy.EarliestLap", v.StrategyState.EarliestLap);
            Set("State.Strategy.OptimalLap", v.StrategyState.OptimalLap);
            Set("State.Strategy.LatestLap", v.StrategyState.LatestLap);
            Set("State.Strategy.Recommendation", v.StrategyState.Recommendation);
            Set("State.Strategy.WindowPositionPercent", v.StrategyState.WindowPositionPercent);
            Set("State.Strategy.WindowSpanLaps", v.StrategyState.WindowSpanLaps);
            Set("State.Strategy.PlannedStintsRemaining", v.StrategyState.PlannedStintsRemaining);
            Set("State.Strategy.CurrentStintTargetLaps", v.StrategyState.CurrentStintTargetLaps);
            Set("State.Strategy.NextStintTargetLaps", v.StrategyState.NextStintTargetLaps);
            Set("State.Strategy.NextStopFuelToAddLiters", v.StrategyState.NextStopFuelToAddLiters);
            Set("State.Strategy.TotalFuelDeficitLiters", v.StrategyState.TotalFuelDeficitLiters);
            Set("State.Strategy.PlanStatus", v.StrategyState.PlanStatus);
            Set("State.Strategy.StopsCompleted", v.StrategyState.StopsCompleted);
            Set("State.Strategy.PlannedStopsTotal", v.StrategyState.PlannedStopsTotal);
            Set("State.Strategy.NextStopNumber", v.StrategyState.NextStopNumber);
            Set("State.Strategy.StopProgressText", v.StrategyState.StopProgressText);
            Set("State.Strategy.RaceFormatText", v.StrategyState.RaceFormatText);
            Set("State.Strategy.EngineerStateText", v.StrategyState.EngineerStateText);
            Set("State.Strategy.HeaderStatusText", v.StrategyState.HeaderStatusText);
            Set("State.Strategy.FuelCoachStateCode", v.StrategyState.FuelCoachStateCode);
            Set("State.Strategy.FuelCoachStateText", v.StrategyState.FuelCoachStateText);
            Set("State.Strategy.FuelCoachActionText", v.StrategyState.FuelCoachActionText);
            Set("State.Strategy.FuelCoachTargetLiters", v.StrategyState.FuelCoachTargetLiters);
            Set("State.Strategy.FuelCoachActualLiters", v.StrategyState.FuelCoachActualLiters);
            Set("State.Strategy.FuelCoachDeltaLiters", v.StrategyState.FuelCoachDeltaLiters);
            Set("State.Strategy.FuelCoachBufferLiters", v.StrategyState.FuelCoachBufferLiters);

            Set("IsCritical", v.IsFuelCritical);
            Set("IsShort", v.IsFuelShort);
            Set("RefuelDetected", v.RefuelDetected);
            Set("HasFinishEstimate", v.HasFinishEstimate);
            Set("IsTimedSession", v.IsTimedSession);
            Set("IsLapLimitedSession", v.IsLapLimitedSession);

            Set("EstimateSource", v.EstimateSource);
            Set("ProjectionStatus", v.ProjectionStatus);
            Set("Confidence", v.Confidence);
            Set("ConfidencePercent", v.ConfidencePercent);
            Set("Status", v.Status);
            Set("Summary", v.Summary);
            Set("Error", v.Error);
        }

        private void RegisterProperties()
        {
            Add("Ready", false, "True after one valid fuel sample");
            Add("CurrentFuel", 0.0, "Direct current fuel from normalized SimHub frame");
            Add("MaxFuel", 0.0, "Direct maximum fuel from normalized SimHub frame");
            Add("FuelPercent", 0.0, "Direct fuel percentage, 0 to 100");
            Add("CompletedLaps", 0, "Direct normalized completed lap counter");
            Add("CurrentLapTelemetry", 0, "Direct normalized current lap counter");
            Add("TrackPositionPercent", 0.0, "Direct normalized track position, 0 to 100");
            Add("IsInPit", false, "Direct normalized pit state");
            Add("IsInPitLane", false, "Direct normalized pit-lane state");
            Add("BasicFrameCounter", 0L, "Frames published by Fuel Core v3");
            Add("BasicSource", "NO DATA", "Source status for Fuel Core v3 direct telemetry");
            Add("TrackerInitialized", false, "Direct lap tracker baseline is initialized");
            Add("TrackerStatus", "WAITING FOR FRAME", "Current minimal lap tracker state");
            Add("TrackerLapStartNumber", 0, "Lap number currently being tracked");
            Add("TrackerLapStartFuel", 0.0, "Fuel stored at start of tracked lap");
            Add("TrackerLastClosedLapNumber", 0, "Last lap closed by CompletedLaps");
            Add("TrackerLastClosedLapStartFuel", 0.0, "Fuel at start of last closed lap");
            Add("TrackerLastClosedLapEndFuel", 0.0, "Fuel at end of last closed lap");
            Add("TrackerLastClosedLapUsage", 0.0, "Fuel consumed on last closed lap");
            Add("TrackerLastClosedLapValid", false, "Last closed lap passed minimal validation");
            Add("TrackerLastClosedLapReason", string.Empty, "Validation result for last closed lap");
            Add("TrackerValidLapCount", 0, "Accepted laps in direct tracker");
            Add("TrackerRejectedLapCount", 0, "Rejected laps in direct tracker");
            Add("InstantDataReady", false, "Current fuel and tank capacity are available");
            Add("EngineState", "NO DATA", "NO DATA, LEARNING, READY, SAFE or WARNING");
            Add("LearningProgressText", "0 / 3 VALID LAPS", "Progress toward stable fuel estimates");
            Add("LevelLiters", 0.0, "Current fuel level");
            Add("LevelPercent", 0.0, "Current fuel percentage");
            Add("CapacityLiters", 0.0, "Detected usable tank capacity");

            Add("LastLapUsageLiters", 0.0, "Latest accepted lap consumption");
            Add("AverageUsageLiters", 0.0, "Trimmed mean consumption");
            Add("MedianUsageLiters", 0.0, "Stint median consumption");
            Add("RecentUsageLiters", 0.0, "Median of recent laps");
            Add("ConservativeUsageLiters", 0.0, "75th percentile diagnostic");
            Add("StrategyUsageLiters", 0.0, "Consumption used for strategy");
            Add("BestUsageLiters", 0.0, "Lowest accepted consumption");
            Add("WorstUsageLiters", 0.0, "Highest accepted consumption");
            Add("LapsRemaining", 0.0, "Session-independent laps available from current fuel and SAFE usage");
            Add("WholeLapsRemaining", 0, "Whole complete laps available from current fuel");
            Add("FuelAfterNextLapLiters", 0.0, "Projected fuel after one additional lap at SAFE usage");
            Add("StintRemainderLiters", 0.0, "Fuel remaining after the maximum whole laps in the current stint");
            Add("UsageTrendLitersPerLap", 0.0, "Linear trend of recent fuel usage in liters per lap");
            Add("UsageTrendStatus", "STABLE", "RISING, STABLE or FALLING recent consumption trend");

            Add("ValidSampleCount", 0, "Accepted lap samples");
            Add("RejectedSampleCount", 0, "Rejected lap samples");
            Add("LastSampleRejected", false, "Latest completed lap was rejected");
            Add("LastSampleRejectReason", string.Empty, "Reason latest lap was rejected");

            Add("AverageLapTimeSeconds", 0.0, "Trimmed mean lap time");
            Add("MedianLapTimeSeconds", 0.0, "Median lap time");
            Add("RecentLapTimeSeconds", 0.0, "Median of recent valid lap times");
            Add("ConservativeLapTimeSeconds", 0.0, "75th percentile valid lap time");
            Add("StrategyLapTimeSeconds", 0.0, "Robust lap time used for fuel-duration and timed-race projection");
            Add("LapTimeSource", "UNAVAILABLE", "Source used for strategy lap time");
            Add("DisplayLapsRemaining", 0.0, "Laps available with display reserve");
            Add("FuelTimeRemainingSeconds", 0.0, "Estimated driving time from current fuel");
            Add("SessionLapsRemaining", 0.0, "Estimated race laps remaining");

            Add("RequiredToFinishLiters", 0.0, "Fuel required to finish including reserve");
            Add("ToAddLiters", 0.0, "Total additional fuel required");
            Add("FillToLiters", 0.0, "Suggested tank level after stop");
            Add("FinishMarginLiters", 0.0, "Predicted fuel remaining at finish");
            Add("ReserveLiters", 0.0, "Finish reserve");
            Add("TargetUsageLiters", 0.0, "Required consumption target");
            Add("SavePerLapLiters", 0.0, "Saving required per lap to finish");
            Add("ExtraLapTargetUsageLiters", 0.0, "Consumption target to extend by one lap");
            Add("SaveForExtraLapLiters", 0.0, "Saving per lap needed for one extra lap");
            Add("LapsShort", 0.0, "Equivalent laps short of the finish");
            Add("TanksNeeded", 0.0, "Additional fuel expressed as tank fractions");

            Add("PitStopsNeeded", 0, "Minimum estimated fuel stops");
            Add("FuelStopsNeeded", 0, "Stops required by fuel capacity and projected demand");
            Add("MandatoryStopsRequired", 0, "Mandatory stops required by the event when telemetry exposes them");
            Add("MandatoryStopsCompleted", 0, "Mandatory stops already completed");
            Add("MandatoryStopsRemaining", 0, "Mandatory stops still required");
            Add("TotalStopsRemaining", 0, "Total stops remaining after combining fuel and mandatory requirements");
            Add("MandatoryStopsSource", "AUTO / UNAVAILABLE", "Source of mandatory stop information");
            Add("CurrentRaceLap", 0, "Current race lap");
            Add("EstimatedFinishLap", 0, "Estimated absolute finish lap");
            Add("PitEarliestLap", 0, "Earliest valid fuel stop lap");
            Add("PitOptimalLap", 0, "Balanced target fuel stop lap");
            Add("PitLatestLap", 0, "Latest safe fuel stop lap");
            Add("PitWindowValid", false, "True when a usable fuel window exists");
            Add("NextPitLap", 0, "Recommended pit lap");
            Add("PitWindowPhase", "UNAVAILABLE", "Internal pit-window phase");
            Add("PitWindowStateCode", "UNAVAILABLE", "CLOSED, OPEN, CLOSING, MISSED, NO_STOP or UNAVAILABLE");
            Add("PitWindowStateText", "WAITING", "Driver-facing pit-window button text");
            Add("PitWindowActionText", "NO RACE TARGET", "Driver-facing action below the pit-window bar");
            Add("Recommendation", "NO RACE TARGET", "Driver-facing strategy recommendation");
            Add("PitWindowProgressPercent", 0.0, "Progress through the active pit window");
            Add("PitWindowCurrentPositionPercent", 0.0, "Live marker position across the displayed pit window");
            Add("PitWindowSpanLaps", 0, "Width of the pit window in laps");
            Add("LapsToOptimalPit", 0, "Laps remaining until the optimal pit lap");
            Add("AverageBasisText", "0 VALID LAPS", "Accepted sample count used by the displayed average");
            Add("StintStartFuelLiters", 0.0, "Actual fuel present at the start of the current stint");
            Add("StintStartLap", 0, "Absolute lap where the current stint began");
            Add("FuelAddedThisStopLiters", 0.0, "Fuel actually added during the most recent stop");
            Add("CurrentStintFuelUsedLiters", 0.0, "Fuel consumed since the current stint began");
            Add("PhysicalLatestPitLap", 0, "Hard latest pit lap reachable with current fuel and SAFE usage");
            Add("PlannedStintsRemaining", 0, "Strategy Engine v4 planned stints including current stint");
            Add("CurrentStintTargetLaps", 0.0, "Balanced target length for the current actual-fuel stint");
            Add("NextStintTargetLaps", 0.0, "Balanced target length after the next stop");
            Add("NextStopFuelToAddLiters", 0.0, "Fuel load recommended at the next stop");
            Add("TotalFuelDeficitLiters", 0.0, "Total additional fuel demand across all remaining stops");
            Add("StrategyPlanStatus", "NO RACE TARGET", "Strategy Engine v4 plan status");
            Add("StopsCompleted", 0, "Completed refuelling stops in the current race");
            Add("PlannedStopsTotal", 0, "Total currently planned stops including completed stops");
            Add("NextStopNumber", 0, "Number of the next planned stop");
            Add("StopProgressText", "NO RACE PLAN", "Driver-facing NEXT STOP X OF Y status");
            Add("RaceFormatText", "RACE TYPE UNKNOWN", "SPRINT RACE or ENDURANCE RACE");
            Add("EngineerStateText", "LEARNING", "LEARNING READY or ADAPTING TO CONDITIONS");
            Add("ConfidenceDisplayText", "LEARNING (0 LAPS)", "Driver-facing confidence text");
            Add("ConditionsDisplayText", "CONDITIONS STABLE", "Driver-facing track conditions state");
            Add("HeaderStatusText", "LEARNING (0 LAPS)", "Combined driver-facing header status");
            Add("FuelCoachStateCode", "WAITING", "Fuel Coach display state");
            Add("FuelCoachStateText", "LEARNING", "Driver-facing Fuel Coach status");
            Add("FuelCoachActionText", "COMPLETE CLEAN LAPS", "Driver-facing Fuel Coach action");
            Add("FuelCoachTargetLiters", 0.0, "PLAN consumption target used by Fuel Coach");
            Add("FuelCoachActualLiters", 0.0, "Recent actual consumption used by Fuel Coach");
            Add("FuelCoachDeltaLiters", 0.0, "Actual minus PLAN consumption");
            Add("FuelCoachBufferLiters", 0.0, "Estimated fuel buffer created before target pit point");
            Add("PhysicalStintLapsRemaining", 0.0, "Physical laps remaining from current usable fuel");
            Add("AdaptiveModelActive", false, "Recent conditions differ materially from the stint history");
            Add("ModelStatusText", "MODEL STABLE", "MODEL STABLE or MODEL ADAPTING");
            Add("ConditionsStatus", "STABLE CONDITIONS", "STABLE CONDITIONS or CHANGING CONDITIONS");
            Add("RecentVsStintDeltaPercent", 0.0, "Recent fuel usage delta versus stint average");
            Add("RecentLapTimeDeltaPercent", 0.0, "Recent lap-time delta versus stint median");
            Add("AdaptiveStrategyUsageLiters", 0.0, "Fuel strategy usage after adaptive weighting");
            Add("RelevantSampleCount", 0, "Samples prioritized by the current model");

            Add("State.Fuel.CurrentLiters", 0.0, "FuelState current fuel");
            Add("State.Fuel.CapacityLiters", 0.0, "FuelState tank capacity");
            Add("State.Fuel.Percent", 0.0, "FuelState percentage");
            Add("State.Fuel.LapsRemaining", 0.0, "FuelState autonomy");
            Add("State.Fuel.TimeRemainingSeconds", 0.0, "FuelState autonomy time");
            Add("State.Consumption.LastLiters", 0.0, "ConsumptionState last lap");
            Add("State.Consumption.AverageLiters", 0.0, "ConsumptionState average");
            Add("State.Consumption.SafeLiters", 0.0, "ConsumptionState safe value");
            Add("State.Consumption.ExtraLapTargetLiters", 0.0, "ConsumptionState extra-lap target");
            Add("State.Strategy.FuelToAddLiters", 0.0, "StrategyState fuel to add");
            Add("State.Strategy.FinishMarginLiters", 0.0, "StrategyState finish margin");
            Add("State.Strategy.StopsRemaining", 0, "StrategyState stops remaining");
            Add("State.Strategy.EarliestLap", 0, "StrategyState earliest pit lap");
            Add("State.Strategy.OptimalLap", 0, "StrategyState optimal pit lap");
            Add("State.Strategy.LatestLap", 0, "StrategyState latest pit lap");
            Add("State.Strategy.Recommendation", "NO RACE TARGET", "StrategyState recommendation");
            Add("State.Strategy.WindowPositionPercent", 0.0, "StrategyState live window marker");
            Add("State.Strategy.WindowSpanLaps", 0, "StrategyState window width");
            Add("State.Strategy.PlannedStintsRemaining", 0, "Planned stints including current stint");
            Add("State.Strategy.CurrentStintTargetLaps", 0.0, "Target current stint length");
            Add("State.Strategy.NextStintTargetLaps", 0.0, "Target next stint length");
            Add("State.Strategy.NextStopFuelToAddLiters", 0.0, "Recommended next-stop fuel load");
            Add("State.Strategy.TotalFuelDeficitLiters", 0.0, "Total race fuel deficit");
            Add("State.Strategy.PlanStatus", "NO RACE TARGET", "Strategy plan state");
            Add("State.Strategy.StopsCompleted", 0, "Completed strategy stops");
            Add("State.Strategy.PlannedStopsTotal", 0, "Total planned strategy stops");
            Add("State.Strategy.NextStopNumber", 0, "Next strategy stop number");
            Add("State.Strategy.StopProgressText", "NO RACE PLAN", "Driver-facing stop progress");
            Add("State.Strategy.RaceFormatText", "RACE TYPE UNKNOWN", "Driver-facing race format");
            Add("State.Strategy.EngineerStateText", "LEARNING", "Driver-facing engineer state");
            Add("State.Strategy.HeaderStatusText", "LEARNING (0 LAPS)", "Combined driver-facing header status");
            Add("State.Strategy.FuelCoachStateCode", "WAITING", "Fuel Coach display state");
            Add("State.Strategy.FuelCoachStateText", "LEARNING", "Fuel Coach status");
            Add("State.Strategy.FuelCoachActionText", "COMPLETE CLEAN LAPS", "Fuel Coach action");
            Add("State.Strategy.FuelCoachTargetLiters", 0.0, "Fuel Coach PLAN target");
            Add("State.Strategy.FuelCoachActualLiters", 0.0, "Fuel Coach recent actual");
            Add("State.Strategy.FuelCoachDeltaLiters", 0.0, "Fuel Coach consumption delta");
            Add("State.Strategy.FuelCoachBufferLiters", 0.0, "Fuel Coach buffer");

            Add("IsCritical", false, "Critical current fuel level");
            Add("IsShort", false, "Current fuel is insufficient to finish");
            Add("RefuelDetected", false, "Refuel event detected");
            Add("HasFinishEstimate", false, "Finish calculation available");
            Add("IsTimedSession", false, "Timed session estimate");
            Add("IsLapLimitedSession", false, "Lap-limited session estimate");

            Add("EstimateSource", "UNAVAILABLE", "Estimate source");
            Add("ProjectionStatus", "LEARNING", "SAFE, MARGINAL, SHORT, LEARNING or NO RACE TARGET");
            Add("Confidence", "NONE", "NONE LOW MEDIUM HIGH");
            Add("ConfidencePercent", 0.0, "Confidence percentage");
            Add("Status", "LEARNING", "Fuel dashboard status");
            Add("Summary", "WAITING FOR CLEAN LAPS", "Fuel dashboard summary");
            Add("Error", string.Empty, "Fuel diagnostic error");
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
