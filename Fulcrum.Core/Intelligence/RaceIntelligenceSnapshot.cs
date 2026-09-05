using System;

namespace Fulcrum.Core.Intelligence
{
    /// <summary>
    /// Dashboard-ready race context derived from telemetry, Relative and Radar.
    /// </summary>
    public sealed class RaceIntelligenceSnapshot
    {
        public DateTime CapturedAt { get; set; }
        public bool Ready { get; set; }

        public int ThreatScore { get; set; }
        public string ThreatLevel { get; set; }
        public string ThreatReason { get; set; }

        public string AttackOpportunity { get; set; }
        public bool HasAttackOpportunity { get; set; }
        public bool DefenseRequired { get; set; }

        public bool ClosingCarAhead { get; set; }
        public bool ClosingCarBehind { get; set; }
        public double ClosingRateAhead { get; set; }
        public double ClosingRateBehind { get; set; }

        public bool CarAheadInPits { get; set; }
        public bool CarBehindInPits { get; set; }
        public bool FasterClassApproaching { get; set; }
        public bool SlowerClassAhead { get; set; }
        public string ClassTraffic { get; set; }

        public string SuggestedAction { get; set; }
        public string Summary { get; set; }

        public RaceIntelligenceSnapshot()
        {
            Reset();
        }

        public void Reset()
        {
            CapturedAt = DateTime.MinValue;
            Ready = false;
            ThreatScore = 0;
            ThreatLevel = "Safe";
            ThreatReason = string.Empty;
            AttackOpportunity = "None";
            HasAttackOpportunity = false;
            DefenseRequired = false;
            ClosingCarAhead = false;
            ClosingCarBehind = false;
            ClosingRateAhead = 0.0;
            ClosingRateBehind = 0.0;
            CarAheadInPits = false;
            CarBehindInPits = false;
            FasterClassApproaching = false;
            SlowerClassAhead = false;
            ClassTraffic = "None";
            SuggestedAction = "Maintain pace";
            Summary = "No immediate traffic threat";
        }
    }
}
