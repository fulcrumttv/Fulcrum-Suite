namespace Fulcrum.Core.Performance
{
    /// <summary>
    /// Centralized update frequencies used by Fulcrum Suite.
    ///
    /// Keeping these values in one place prevents modules from
    /// using unrelated or inconsistent timing values.
    /// </summary>
    public static class UpdateRates
    {
        /// <summary>
        /// Fast vehicle telemetry such as speed, RPM,
        /// throttle, brake and gear.
        /// </summary>
        public const double TelemetryHz = 60.0;

        /// <summary>
        /// Nearby vehicle calculations used by the radar.
        /// </summary>
        public const double RadarHz = 30.0;

        /// <summary>
        /// Relative position and gap calculations.
        /// </summary>
        public const double RelativeHz = 60.0;

        /// <summary>
        /// Delta calculations and lap comparison.
        /// </summary>
        public const double DeltaHz = 30.0;

        /// <summary>
        /// Fuel consumption and remaining-laps calculations.
        /// </summary>
        public const double FuelHz = 5.0;

        /// <summary>
        /// Vehicle health and inferred damage calculations.
        /// </summary>
        public const double VehicleHealthHz = 5.0;

        /// <summary>
        /// Longer-term race strategy calculations.
        /// </summary>
        public const double StrategyHz = 1.0;

        /// <summary>
        /// Pit-window state and stint planning calculations.
        /// </summary>
        public const double PitWindowHz = 2.0;

        /// <summary>
        /// Overall standings table refresh rate.
        /// </summary>
        public const double StandingsHz = 10.0;

        /// <summary>
        /// Publication frequency for internal performance metrics.
        /// </summary>
        public const double PerformancePublisherHz = 2.0;
    }
}