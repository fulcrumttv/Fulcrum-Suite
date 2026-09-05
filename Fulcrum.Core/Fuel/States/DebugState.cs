namespace Fulcrum.Core.Fuel.States
{
    public sealed class DebugState
    {
        public int ValidLaps { get; set; }
        public int RejectedLaps { get; set; }
        public string LastRejectReason { get; set; }
        public string ProjectionSource { get; set; }
        public DebugState() { LastRejectReason = string.Empty; ProjectionSource = "UNAVAILABLE"; }
    }
}
