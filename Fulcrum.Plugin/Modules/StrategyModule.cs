using System;
using Fulcrum.Core.Fuel;
using Fulcrum.Core.Intelligence;
using Fulcrum.Core.Performance;
using Fulcrum.Core.Relative.Display;
using Fulcrum.Core.Strategy;
using Fulcrum.Core.Telemetry;
using Fulcrum.Plugin.Publishing;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Modules
{
    public sealed class StrategyModule
    {
        private readonly StrategyEngine engine;
        private readonly StrategySnapshot snapshot;
        private readonly StrategyPublisher publisher;
        private readonly ScheduledTask updateTask;

        private TelemetrySnapshot telemetry;
        private FuelSnapshot fuel;
        private RaceIntelligenceSnapshot intelligence;
        private RelativeDisplaySnapshot relative;
        private bool gameRunning;

        public StrategyModule(PluginManager pluginManager, Type pluginType, UpdateScheduler scheduler)
        {
            if (pluginManager == null) throw new ArgumentNullException(nameof(pluginManager));
            if (pluginType == null) throw new ArgumentNullException(nameof(pluginType));
            if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));

            engine = new StrategyEngine();
            snapshot = new StrategySnapshot();
            publisher = new StrategyPublisher(pluginManager, pluginType);
            updateTask = scheduler.RegisterTask("Strategy Engine", UpdateRates.StrategyHz, UpdateScheduled, false);
            Reset();
        }

        public StrategySnapshot Snapshot
        {
            get { return snapshot; }
        }

        public void SetFrameContext(
            TelemetrySnapshot telemetrySnapshot,
            FuelSnapshot fuelSnapshot,
            RaceIntelligenceSnapshot intelligenceSnapshot,
            RelativeDisplaySnapshot relativeSnapshot,
            bool isGameRunning)
        {
            telemetry = telemetrySnapshot;
            fuel = fuelSnapshot;
            intelligence = intelligenceSnapshot;
            relative = relativeSnapshot;
            gameRunning = isGameRunning;
        }

        public void Reset()
        {
            telemetry = null;
            fuel = null;
            intelligence = null;
            relative = null;
            gameRunning = false;
            engine.Reset(snapshot);
            publisher.Publish(snapshot);
        }

        private void UpdateScheduled()
        {
            if (!gameRunning || telemetry == null || fuel == null || intelligence == null || relative == null)
            {
                engine.Reset(snapshot);
            }
            else
            {
                engine.Update(telemetry, fuel, intelligence, relative, snapshot);
            }

            publisher.Publish(snapshot);
        }
    }
}
