using System;
using System.Globalization;
using Fulcrum.Core.Fuel;
using Fulcrum.Core.Strategy;
using Fulcrum.Core.Telemetry;

namespace Fulcrum.Core.PitWindow
{
    public sealed class PitWindowEngine
    {
        private bool previousOnPitRoad;
        private bool initializedPitState;
        private int pitStopCount;
        private int lastPitEntryLap;
        private int lastPitExitLap;
        private int eventSequence;
        private string previousStatus;

        public PitWindowEngine()
        {
            lastPitEntryLap = -1;
            lastPitExitLap = -1;
            previousStatus = string.Empty;
        }

        public void Reset(PitWindowSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            previousOnPitRoad = false;
            initializedPitState = false;
            pitStopCount = 0;
            lastPitEntryLap = -1;
            lastPitExitLap = -1;
            eventSequence = 0;
            previousStatus = string.Empty;
            snapshot.Reset();
        }

        public void Update(
            TelemetrySnapshot telemetry,
            FuelSnapshot fuel,
            StrategySnapshot strategy,
            PitWindowSnapshot snapshot)
        {
            if (telemetry == null) throw new ArgumentNullException(nameof(telemetry));
            if (fuel == null) throw new ArgumentNullException(nameof(fuel));
            if (strategy == null) throw new ArgumentNullException(nameof(strategy));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            snapshot.UpdatedAtUtc = DateTime.UtcNow;
            snapshot.JustEnteredPits = false;
            snapshot.JustExitedPits = false;
            snapshot.CurrentLap = Math.Max(0, telemetry.LapCompleted);
            snapshot.IsOnPitRoad = telemetry.IsOnPitRoad;

            UpdatePitTransitions(telemetry, snapshot);
            UpdateFuelProjection(fuel, snapshot);

            snapshot.Ready = telemetry.GameRunning && fuel.Ready && fuel.HasFinishEstimate;
            snapshot.HasWindow = snapshot.Ready && strategy.PitWindowOpenLap >= 0 && strategy.PitWindowCloseLap >= strategy.PitWindowOpenLap;
            snapshot.OpenLap = snapshot.HasWindow ? strategy.PitWindowOpenLap : 0;
            snapshot.CloseLap = snapshot.HasWindow ? strategy.PitWindowCloseLap : 0;
            snapshot.IsOpen = snapshot.HasWindow && strategy.PitWindowIsOpen;
            snapshot.MustPitThisLap = snapshot.Ready && strategy.MustPitThisLap;
            snapshot.LapsUntilOpen = snapshot.HasWindow ? Math.Max(0, strategy.LapsUntilPitWindowOpen) : 0;
            snapshot.LapsUntilClose = snapshot.HasWindow ? Math.Max(0, strategy.LapsUntilPitWindowClose) : 0;
            snapshot.CanFinishWithoutStop = snapshot.Ready && strategy.CanFinish;
            snapshot.CanReachWindow = snapshot.HasWindow &&
                                      (snapshot.IsOpen || snapshot.FuelLapsRemaining + 0.15 >= snapshot.LapsUntilOpen);

            snapshot.CurrentStintLap = lastPitExitLap >= 0
                ? Math.Max(0, snapshot.CurrentLap - lastPitExitLap)
                : snapshot.CurrentLap;

            UpdateText(snapshot);
            UpdateStatus(snapshot);
            UpdateEvent(snapshot);
        }

        private void UpdatePitTransitions(TelemetrySnapshot telemetry, PitWindowSnapshot snapshot)
        {
            bool onPitRoad = telemetry.IsOnPitRoad;

            if (!initializedPitState)
            {
                initializedPitState = true;
                previousOnPitRoad = onPitRoad;
                return;
            }

            if (!previousOnPitRoad && onPitRoad)
            {
                snapshot.JustEnteredPits = true;
                lastPitEntryLap = Math.Max(0, telemetry.LapCompleted);
                pitStopCount++;
                eventSequence++;
                snapshot.EventName = "PitEntry";
            }
            else if (previousOnPitRoad && !onPitRoad)
            {
                snapshot.JustExitedPits = true;
                lastPitExitLap = Math.Max(0, telemetry.LapCompleted);
                eventSequence++;
                snapshot.EventName = "PitExit";
            }

            previousOnPitRoad = onPitRoad;
            snapshot.PitStopCount = pitStopCount;
            snapshot.LastPitEntryLap = lastPitEntryLap;
            snapshot.LastPitExitLap = lastPitExitLap;
        }

        private static void UpdateFuelProjection(FuelSnapshot fuel, PitWindowSnapshot snapshot)
        {
            snapshot.FuelLapsRemaining = Math.Max(0.0, fuel.FuelLapsRemaining);
            snapshot.EstimatedSessionLapsRemaining = Math.Max(0.0, fuel.EstimatedSessionLapsRemaining);
            snapshot.MaximumFuelToAddLiters = Math.Max(0.0, fuel.FuelCapacityLiters - fuel.FuelLevelLiters);
            snapshot.RecommendedFuelToAddLiters = fuel.HasFinishEstimate
                ? Math.Min(snapshot.MaximumFuelToAddLiters, Math.Max(0.0, fuel.FuelToAddLiters))
                : 0.0;

            snapshot.FullTankStintLaps = fuel.AverageUsageLiters > 0.01 && fuel.FuelCapacityLiters > 0.0
                ? Math.Max(0.0, (fuel.FuelCapacityLiters / fuel.AverageUsageLiters) - 1.0)
                : 0.0;

            if (!fuel.HasFinishEstimate || snapshot.FullTankStintLaps <= 0.1)
            {
                snapshot.EstimatedStopsRemaining = 0;
                return;
            }

            double distanceAfterCurrentFuel = Math.Max(
                0.0,
                snapshot.EstimatedSessionLapsRemaining - snapshot.FuelLapsRemaining);

            snapshot.EstimatedStopsRemaining = distanceAfterCurrentFuel <= 0.05
                ? 0
                : (int)Math.Ceiling(distanceAfterCurrentFuel / snapshot.FullTankStintLaps);
        }

        private static void UpdateText(PitWindowSnapshot snapshot)
        {
            snapshot.WindowText = snapshot.HasWindow
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    "L{0}-L{1}",
                    snapshot.OpenLap,
                    snapshot.CloseLap)
                : "--";

            if (!snapshot.Ready)
            {
                snapshot.CountdownText = "--";
            }
            else if (snapshot.MustPitThisLap)
            {
                snapshot.CountdownText = "PIT NOW";
            }
            else if (snapshot.IsOpen)
            {
                snapshot.CountdownText = snapshot.LapsUntilClose <= 0
                    ? "LAST LAP"
                    : string.Format(
                        CultureInfo.InvariantCulture,
                        "OPEN · {0}L LEFT",
                        snapshot.LapsUntilClose);
            }
            else if (snapshot.HasWindow)
            {
                snapshot.CountdownText = string.Format(
                    CultureInfo.InvariantCulture,
                    "OPENS IN {0}L",
                    snapshot.LapsUntilOpen);
            }
            else
            {
                snapshot.CountdownText = "NO WINDOW";
            }
        }

        private static void UpdateStatus(PitWindowSnapshot snapshot)
        {
            if (!snapshot.Ready)
            {
                snapshot.Status = "Unavailable";
                snapshot.Recommendation = "Collect Data";
                snapshot.Summary = "Complete clean laps to calculate the pit window";
                return;
            }

            if (snapshot.IsOnPitRoad)
            {
                snapshot.Status = "InPits";
                snapshot.Recommendation = "Complete Service";
                snapshot.Summary = string.Format(
                    CultureInfo.InvariantCulture,
                    "Add {0:0.0} L · {1} stop(s) remaining",
                    snapshot.RecommendedFuelToAddLiters,
                    snapshot.EstimatedStopsRemaining);
                return;
            }

            if (snapshot.CanFinishWithoutStop)
            {
                snapshot.Status = "NoStopRequired";
                snapshot.Recommendation = "Stay Out";
                snapshot.Summary = string.Format(
                    CultureInfo.InvariantCulture,
                    "Fuel can reach the finish · {0:0.0} laps available",
                    snapshot.FuelLapsRemaining);
                return;
            }

            if (snapshot.MustPitThisLap)
            {
                snapshot.Status = "PitNow";
                snapshot.Recommendation = "Pit This Lap";
                snapshot.Summary = string.Format(
                    CultureInfo.InvariantCulture,
                    "Latest safe lap · add {0:0.0} L",
                    snapshot.RecommendedFuelToAddLiters);
                return;
            }

            if (snapshot.IsOpen)
            {
                snapshot.Status = "WindowOpen";
                snapshot.Recommendation = "Pit Window Open";
                snapshot.Summary = string.Format(
                    CultureInfo.InvariantCulture,
                    "Window {0} · add {1:0.0} L",
                    snapshot.WindowText,
                    snapshot.RecommendedFuelToAddLiters);
                return;
            }

            if (!snapshot.CanReachWindow)
            {
                snapshot.Status = "CannotReachWindow";
                snapshot.Recommendation = "Save Fuel";
                snapshot.Summary = "Current fuel estimate cannot safely reach the window";
                return;
            }

            snapshot.Status = "BeforeWindow";
            snapshot.Recommendation = "Stay Out";
            snapshot.Summary = string.Format(
                CultureInfo.InvariantCulture,
                "{0} · fuel for {1:0.0} laps",
                snapshot.CountdownText,
                snapshot.FuelLapsRemaining);
        }

        private void UpdateEvent(PitWindowSnapshot snapshot)
        {
            if (snapshot.JustEnteredPits || snapshot.JustExitedPits)
            {
                snapshot.EventSequence = eventSequence;
                previousStatus = snapshot.Status;
                return;
            }

            if (!string.Equals(previousStatus, snapshot.Status, StringComparison.Ordinal))
            {
                if (!string.IsNullOrEmpty(previousStatus))
                {
                    eventSequence++;
                    snapshot.EventName = snapshot.Status;
                }

                previousStatus = snapshot.Status;
            }
            else
            {
                snapshot.EventName = "None";
            }

            snapshot.EventSequence = eventSequence;
        }
    }
}
