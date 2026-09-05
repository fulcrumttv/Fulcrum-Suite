using System;

namespace Fulcrum.Core.Relative.Prediction
{
    /// <summary>
    /// Stable predictive context for the nearest cars ahead and behind.
    /// Positive closing rates mean the gap is shrinking.
    /// </summary>
    public sealed class RelativePredictionSnapshot
    {
        public DateTime CapturedAt { get; set; }
        public bool Ready { get; set; }

        public int AheadCarIndex { get; set; }
        public double AheadGapSeconds { get; set; }
        public double AheadClosingRate { get; set; }
        public double AheadTimeToCatchSeconds { get; set; }
        public bool IsCatchingAhead { get; set; }
        public bool BattleAhead { get; set; }

        public int BehindCarIndex { get; set; }
        public double BehindGapSeconds { get; set; }
        public double BehindClosingRate { get; set; }
        public double BehindTimeToCatchSeconds { get; set; }
        public bool IsBeingCaught { get; set; }
        public bool BattleBehind { get; set; }

        public string PressureLevel { get; set; }
        public string BattleState { get; set; }
        public string Recommendation { get; set; }
        public string Summary { get; set; }

        public RelativePredictionSnapshot()
        {
            Reset();
        }

        public void Reset()
        {
            CapturedAt = DateTime.MinValue;
            Ready = false;

            AheadCarIndex = -1;
            AheadGapSeconds = 0.0;
            AheadClosingRate = 0.0;
            AheadTimeToCatchSeconds = 0.0;
            IsCatchingAhead = false;
            BattleAhead = false;

            BehindCarIndex = -1;
            BehindGapSeconds = 0.0;
            BehindClosingRate = 0.0;
            BehindTimeToCatchSeconds = 0.0;
            IsBeingCaught = false;
            BattleBehind = false;

            PressureLevel = "None";
            BattleState = "Clear";
            Recommendation = "Maintain pace";
            Summary = "No nearby battle";
        }
    }
}
