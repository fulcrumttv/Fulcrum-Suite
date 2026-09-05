using System;
using Fulcrum.Core.Fuel.States;

namespace Fulcrum.Core.Fuel
{
    public sealed class FuelSnapshot
    {
        public bool Ready { get; set; }

        // Phase 1 state-oriented architecture. Flat properties remain available
        // for backward-compatible SimHub bindings, while these grouped models
        // provide one source of truth for future compact, vertical and web UIs.
        public FuelState FuelState { get; private set; }
        public ConsumptionState ConsumptionState { get; private set; }
        public StrategyState StrategyState { get; private set; }
        public DebugState DebugState { get; private set; }
        // Fuel Core v3: direct, frame-level telemetry diagnostics.
        public double CurrentFuel { get; set; }
        public double MaxFuel { get; set; }
        public double FuelPercent { get; set; }
        public int CompletedLaps { get; set; }
        public int CurrentLapTelemetry { get; set; }
        public double TrackPositionPercent { get; set; }
        public bool IsInPit { get; set; }
        public bool IsInPitLane { get; set; }
        public long BasicFrameCounter { get; set; }
        public string BasicSource { get; set; }

        // Fuel Core v3.1: minimal lap-tracker diagnostics.
        public bool TrackerInitialized { get; set; }
        public string TrackerStatus { get; set; }
        public int TrackerLapStartNumber { get; set; }
        public double TrackerLapStartFuel { get; set; }
        public int TrackerLastClosedLapNumber { get; set; }
        public double TrackerLastClosedLapStartFuel { get; set; }
        public double TrackerLastClosedLapEndFuel { get; set; }
        public double TrackerLastClosedLapUsage { get; set; }
        public bool TrackerLastClosedLapValid { get; set; }
        public string TrackerLastClosedLapReason { get; set; }
        public int TrackerValidLapCount { get; set; }
        public int TrackerRejectedLapCount { get; set; }

        public bool InstantDataReady { get; set; }
        public string EngineState { get; set; }
        public string LearningProgressText { get; set; }
        public double FuelLevelLiters { get; set; }
        public double FuelLevelPercent { get; set; }
        public double FuelCapacityLiters { get; set; }

        public double LastLapUsageLiters { get; set; }
        public double AverageUsageLiters { get; set; }
        public double MedianUsageLiters { get; set; }
        public double RecentUsageLiters { get; set; }
        public double ConservativeUsageLiters { get; set; }
        public double StrategyUsageLiters { get; set; }
        public double BestUsageLiters { get; set; }
        public double WorstUsageLiters { get; set; }

        // Fuel Core v3.3: session-independent autonomy and trend diagnostics.
        public double FuelLapsRemaining { get; set; }
        public int WholeLapsRemaining { get; set; }
        public double FuelAfterNextLapLiters { get; set; }
        public double StintRemainderLiters { get; set; }
        public double UsageTrendLitersPerLap { get; set; }
        public string UsageTrendStatus { get; set; }

        public int ValidSampleCount { get; set; }
        public int RejectedSampleCount { get; set; }
        public bool LastSampleRejected { get; set; }
        public string LastSampleRejectReason { get; set; }

        public double AverageLapTimeSeconds { get; set; }
        public double MedianLapTimeSeconds { get; set; }
        public double RecentLapTimeSeconds { get; set; }
        public double ConservativeLapTimeSeconds { get; set; }
        public double StrategyLapTimeSeconds { get; set; }
        public string LapTimeSource { get; set; }
        public double DisplayLapsRemaining { get; set; }
        public double FuelTimeRemainingSeconds { get; set; }
        public double EstimatedSessionLapsRemaining { get; set; }

        public double FuelRequiredToFinishLiters { get; set; }
        public double FuelToAddLiters { get; set; }
        public double FillToLiters { get; set; }
        public double FinishMarginLiters { get; set; }
        public double ReserveLiters { get; set; }
        public double TargetUsageLiters { get; set; }
        public double SavePerLapLiters { get; set; }
        public double ExtraLapTargetUsageLiters { get; set; }
        public double SaveForExtraLapLiters { get; set; }
        public double LapsShort { get; set; }
        public double TanksNeeded { get; set; }

        public int PitStopsNeeded { get; set; }
        public int FuelStopsNeeded { get; set; }
        public int MandatoryStopsRequired { get; set; }
        public int MandatoryStopsCompleted { get; set; }
        public int MandatoryStopsRemaining { get; set; }
        public int TotalStopsRemaining { get; set; }
        public string MandatoryStopsSource { get; set; }
        public int CurrentRaceLap { get; set; }
        public int EstimatedFinishLap { get; set; }
        public int PitEarliestLap { get; set; }
        public int PitOptimalLap { get; set; }
        public int PitLatestLap { get; set; }
        public bool PitWindowValid { get; set; }
        public int NextPitLap { get; set; }
        public string PitWindowPhase { get; set; }
        public string PitWindowStateCode { get; set; }
        public string PitWindowStateText { get; set; }
        public string PitWindowActionText { get; set; }
        public string Recommendation { get; set; }
        public double PitWindowProgressPercent { get; set; }
        public double PitWindowCurrentPositionPercent { get; set; }
        public int PitWindowSpanLaps { get; set; }
        public int LapsToOptimalPit { get; set; }
        public string AverageBasisText { get; set; }

        // Real current-stint diagnostics. These values use the actual fuel loaded
        // at pit exit, including partial opening tanks and partial refuels.
        public double StintStartFuelLiters { get; set; }
        public int StintStartLap { get; set; }
        public double FuelAddedThisStopLiters { get; set; }
        public double CurrentStintFuelUsedLiters { get; set; }
        public int PhysicalLatestPitLap { get; set; }
        public double PhysicalStintLapsRemaining { get; set; }

        // Strategy Engine v4: race-first planning. The current partial stint is
        // planned separately from future full/partial stints.
        public int PlannedStintsRemaining { get; set; }
        public double CurrentStintTargetLaps { get; set; }
        public double NextStintTargetLaps { get; set; }
        public double NextStopFuelToAddLiters { get; set; }
        public double TotalFuelDeficitLiters { get; set; }
        public string StrategyPlanStatus { get; set; }

        // Driver-facing Strategy Engine status. These properties intentionally
        // describe decisions and confidence instead of exposing internal model jargon.
        public int StopsCompleted { get; set; }
        public int PlannedStopsTotal { get; set; }
        public int NextStopNumber { get; set; }
        public string StopProgressText { get; set; }
        public string RaceFormatText { get; set; }
        public string EngineerStateText { get; set; }
        public string ConfidenceDisplayText { get; set; }
        public string ConditionsDisplayText { get; set; }
        public string HeaderStatusText { get; set; }

        // Fuel Coach v4.1: driver-facing consumption guidance. Display fields
        // already include Pit Window priority so the overlay never shows competing
        // instructions at the same time.
        public string FuelCoachStateCode { get; set; }
        public string FuelCoachStateText { get; set; }
        public string FuelCoachActionText { get; set; }
        public double FuelCoachTargetLiters { get; set; }
        public double FuelCoachActualLiters { get; set; }
        public double FuelCoachDeltaLiters { get; set; }
        public double FuelCoachBufferLiters { get; set; }

        // Adaptive Fuel Model: detects sustained changes in consumption or pace
        // and temporarily prioritizes the most relevant recent samples.
        public bool AdaptiveModelActive { get; set; }
        public string ModelStatusText { get; set; }
        public string ConditionsStatus { get; set; }
        public double RecentVsStintDeltaPercent { get; set; }
        public double RecentLapTimeDeltaPercent { get; set; }
        public double AdaptiveStrategyUsageLiters { get; set; }
        public int RelevantSampleCount { get; set; }

        public bool IsFuelCritical { get; set; }
        public bool IsFuelShort { get; set; }
        public bool RefuelDetected { get; set; }
        public bool HasFinishEstimate { get; set; }
        public bool IsTimedSession { get; set; }
        public bool IsLapLimitedSession { get; set; }

        public string EstimateSource { get; set; }
        public string ProjectionStatus { get; set; }
        public string Confidence { get; set; }
        public double ConfidencePercent { get; set; }
        public string Status { get; set; }
        public string Summary { get; set; }
        public string Error { get; set; }
        public DateTime UpdatedAtUtc { get; set; }

        public FuelSnapshot()
        {
            FuelState = new FuelState();
            ConsumptionState = new ConsumptionState();
            StrategyState = new StrategyState();
            DebugState = new DebugState();
            LastSampleRejectReason = string.Empty;
            BasicSource = "NO DATA";
            TrackerStatus = "WAITING FOR FRAME";
            TrackerLastClosedLapReason = string.Empty;
            UsageTrendStatus = "STABLE";
            LapTimeSource = "UNAVAILABLE";
            MandatoryStopsSource = "AUTO / UNAVAILABLE";
            PitWindowPhase = "UNAVAILABLE";
            PitWindowStateCode = "UNAVAILABLE";
            PitWindowStateText = "WAITING";
            PitWindowActionText = "NO RACE TARGET";
            Recommendation = "NO RACE TARGET";
            AverageBasisText = "0 VALID LAPS";
            AdaptiveModelActive = false;
            ModelStatusText = "MODEL STABLE";
            ConditionsStatus = "STABLE CONDITIONS";
            RecentVsStintDeltaPercent = 0.0;
            RecentLapTimeDeltaPercent = 0.0;
            AdaptiveStrategyUsageLiters = 0.0;
            RelevantSampleCount = 0;
            StrategyPlanStatus = "NO RACE TARGET";
            StopsCompleted = 0;
            PlannedStopsTotal = 0;
            NextStopNumber = 0;
            StopProgressText = "NO RACE PLAN";
            RaceFormatText = "RACE TYPE UNKNOWN";
            EngineerStateText = "LEARNING";
            ConfidenceDisplayText = "LEARNING (0 LAPS)";
            ConditionsDisplayText = "CONDITIONS STABLE";
            HeaderStatusText = "LEARNING (0 LAPS)";
            FuelCoachStateCode = "WAITING";
            FuelCoachStateText = "LEARNING";
            FuelCoachActionText = "COMPLETE CLEAN LAPS";
            FuelCoachTargetLiters = 0.0;
            FuelCoachActualLiters = 0.0;
            FuelCoachDeltaLiters = 0.0;
            FuelCoachBufferLiters = 0.0;
            EstimateSource = "UNAVAILABLE";
            ProjectionStatus = "LEARNING";
            Confidence = "NONE";
            Status = "LEARNING";
            EngineState = "NO DATA";
            LearningProgressText = "0 / 3 VALID LAPS";
            Summary = "WAITING FOR CLEAN LAPS";
            Error = string.Empty;
        }
    }
}
