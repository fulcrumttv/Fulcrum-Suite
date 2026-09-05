namespace Fulcrum.Core.Fuel.States
{
    public sealed class ConsumptionState
    {
        public double LastLiters { get; set; }
        public double AverageLiters { get; set; }
        public double SafeLiters { get; set; }
        public double ExtraLapTargetLiters { get; set; }
        public string Trend { get; set; }
        public string Confidence { get; set; }
        public double ConfidencePercent { get; set; }
        public ConsumptionState() { Trend = "STABLE"; Confidence = "NONE"; }
    }
}
