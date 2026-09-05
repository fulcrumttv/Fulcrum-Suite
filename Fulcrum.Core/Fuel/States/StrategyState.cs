namespace Fulcrum.Core.Fuel.States
{
    public sealed class StrategyState
    {
        public bool HasRaceTarget { get; set; }
        public double RaceLapsRemaining { get; set; }
        public double FuelRequiredLiters { get; set; }
        public double FuelToAddLiters { get; set; }
        public double FinishMarginLiters { get; set; }
        public int StopsRemaining { get; set; }
        public bool PitWindowValid { get; set; }
        public int EarliestLap { get; set; }
        public int OptimalLap { get; set; }
        public int LatestLap { get; set; }
        public string PitWindowPhase { get; set; }
        public string Recommendation { get; set; }
        public double WindowPositionPercent { get; set; }
        public int WindowSpanLaps { get; set; }
        public int PlannedStintsRemaining { get; set; }
        public double CurrentStintTargetLaps { get; set; }
        public double NextStintTargetLaps { get; set; }
        public double NextStopFuelToAddLiters { get; set; }
        public double TotalFuelDeficitLiters { get; set; }
        public string PlanStatus { get; set; }
        public int StopsCompleted { get; set; }
        public int PlannedStopsTotal { get; set; }
        public int NextStopNumber { get; set; }
        public string StopProgressText { get; set; }
        public string RaceFormatText { get; set; }
        public string EngineerStateText { get; set; }
        public string HeaderStatusText { get; set; }
        public string FuelCoachStateCode { get; set; }
        public string FuelCoachStateText { get; set; }
        public string FuelCoachActionText { get; set; }
        public double FuelCoachTargetLiters { get; set; }
        public double FuelCoachActualLiters { get; set; }
        public double FuelCoachDeltaLiters { get; set; }
        public double FuelCoachBufferLiters { get; set; }
        public StrategyState()
        {
            PitWindowPhase = "UNAVAILABLE";
            Recommendation = "NO RACE TARGET";
            PlanStatus = "NO RACE TARGET";
            StopProgressText = "NO RACE PLAN";
            RaceFormatText = "RACE TYPE UNKNOWN";
            EngineerStateText = "LEARNING";
            HeaderStatusText = "LEARNING (0 LAPS)";
            FuelCoachStateCode = "WAITING";
            FuelCoachStateText = "LEARNING";
            FuelCoachActionText = "COMPLETE CLEAN LAPS";
        }
    }
}
