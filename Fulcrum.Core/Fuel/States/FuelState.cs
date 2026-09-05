namespace Fulcrum.Core.Fuel.States
{
    public sealed class FuelState
    {
        public double CurrentLiters { get; set; }
        public double CapacityLiters { get; set; }
        public double Percent { get; set; }
        public double LapsRemaining { get; set; }
        public double TimeRemainingSeconds { get; set; }
    }
}
