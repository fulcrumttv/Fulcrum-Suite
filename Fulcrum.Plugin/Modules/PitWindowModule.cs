using System;
using Fulcrum.Core.Fuel;
using Fulcrum.Core.Performance;
using Fulcrum.Core.PitWindow;
using Fulcrum.Core.Strategy;
using Fulcrum.Core.Telemetry;
using Fulcrum.Plugin.Publishing;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Modules
{
    public sealed class PitWindowModule
    {
        private readonly PitWindowEngine engine;
        private readonly PitWindowSnapshot snapshot;
        private readonly PitWindowPublisher publisher;
        private readonly ScheduledTask updateTask;

        private TelemetrySnapshot telemetry;
        private FuelSnapshot fuel;
        private StrategySnapshot strategy;
        private bool gameRunning;

        public PitWindowModule(PluginManager pluginManager, Type pluginType, UpdateScheduler scheduler)
        {
            if (pluginManager == null) throw new ArgumentNullException(nameof(pluginManager));
            if (pluginType == null) throw new ArgumentNullException(nameof(pluginType));
            if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));

            engine = new PitWindowEngine();
            snapshot = new PitWindowSnapshot();
            publisher = new PitWindowPublisher(pluginManager, pluginType);
            updateTask = scheduler.RegisterTask("Pit Window", UpdateRates.PitWindowHz, UpdateScheduled, false);
            Reset();
        }

        public PitWindowSnapshot Snapshot
        {
            get { return snapshot; }
        }

        public void SetFrameContext(
            TelemetrySnapshot telemetrySnapshot,
            FuelSnapshot fuelSnapshot,
            StrategySnapshot strategySnapshot,
            bool isGameRunning)
        {
            telemetry = telemetrySnapshot;
            fuel = fuelSnapshot;
            strategy = strategySnapshot;
            gameRunning = isGameRunning;
        }

        public void Reset()
        {
            telemetry = null;
            fuel = null;
            strategy = null;
            gameRunning = false;
            engine.Reset(snapshot);
            publisher.Publish(snapshot);
        }

        private void UpdateScheduled()
        {
            if (!gameRunning || telemetry == null || fuel == null || strategy == null)
            {
                engine.Reset(snapshot);
            }
            else
            {
                engine.Update(telemetry, fuel, strategy, snapshot);
            }

            publisher.Publish(snapshot);
        }
    }
}
