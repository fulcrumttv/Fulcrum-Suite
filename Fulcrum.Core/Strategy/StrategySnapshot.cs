using System;

namespace Fulcrum.Core.Strategy
{
    public sealed class StrategySnapshot
    {
        public bool Ready { get; set; }
        public string Status { get; set; }
        public string Summary { get; set; }
        public string Recommendation { get; set; }
        public string RecommendationReason { get; set; }

        public string RiskLevel { get; set; }
        public int RiskScore { get; set; }

        public bool CanFinish { get; set; }
        public bool NeedSplash { get; set; }
        public double TargetFuelLiters { get; set; }
        public double FuelMarginLiters { get; set; }
        public double FuelMarginLaps { get; set; }

        public int PitWindowOpenLap { get; set; }
        public int PitWindowCloseLap { get; set; }
        public bool PitWindowIsOpen { get; set; }
        public int LapsUntilPitWindowOpen { get; set; }
        public int LapsUntilPitWindowClose { get; set; }
        public bool MustPitThisLap { get; set; }

        public bool TrafficAhead { get; set; }
        public bool FastClassIncoming { get; set; }
        public bool CleanAir { get; set; }
        public bool AttackAvailable { get; set; }
        public bool DefenseRequired { get; set; }

        public string EventName { get; set; }
        public int EventSequence { get; set; }
        public DateTime UpdatedAtUtc { get; set; }

        public StrategySnapshot()
        {
            Reset();
        }

        public void Reset()
        {
            Ready = false;
            Status = "Unavailable";
            Summary = "Waiting for race data";
            Recommendation = "Monitor";
            RecommendationReason = "Strategy data is not ready";
            RiskLevel = "Low";
            RiskScore = 0;
            CanFinish = false;
            NeedSplash = false;
            TargetFuelLiters = 0.0;
            FuelMarginLiters = 0.0;
            FuelMarginLaps = 0.0;
            PitWindowOpenLap = 0;
            PitWindowCloseLap = 0;
            PitWindowIsOpen = false;
            LapsUntilPitWindowOpen = 0;
            LapsUntilPitWindowClose = 0;
            MustPitThisLap = false;
            TrafficAhead = false;
            FastClassIncoming = false;
            CleanAir = true;
            AttackAvailable = false;
            DefenseRequired = false;
            EventName = "None";
            UpdatedAtUtc = DateTime.MinValue;
        }
    }
}
