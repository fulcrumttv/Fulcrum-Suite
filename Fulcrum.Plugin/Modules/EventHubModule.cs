using System;
using Fulcrum.Core.Damage;
using Fulcrum.Core.Events;
using Fulcrum.Core.Fuel;
using Fulcrum.Core.Performance;
using Fulcrum.Core.PitWindow;
using Fulcrum.Core.Spotter;
using Fulcrum.Core.Strategy;
using Fulcrum.Plugin.Publishing;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Modules
{
    public sealed class EventHubModule
    {
        private readonly EventHubEngine engine;
        private readonly EventHubSnapshot snapshot;
        private readonly EventHubPublisher publisher;

        private bool gameRunning;
        private SpotterSnapshot spotter;
        private DamageSnapshot damage;
        private FuelSnapshot fuel;
        private StrategySnapshot strategy;
        private PitWindowSnapshot pitWindow;

        public EventHubModule(PluginManager pluginManager, Type pluginType, UpdateScheduler scheduler)
        {
            if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));
            engine = new EventHubEngine();
            snapshot = new EventHubSnapshot();
            publisher = new EventHubPublisher(pluginManager, pluginType);
            scheduler.RegisterTask("Event Hub", 20.0, UpdateScheduled, false);
            Reset();
        }

        public EventHubSnapshot Snapshot { get { return snapshot; } }

        public void SetFrameContext(
            bool isGameRunning,
            SpotterSnapshot spotterSnapshot,
            DamageSnapshot damageSnapshot,
            FuelSnapshot fuelSnapshot,
            StrategySnapshot strategySnapshot,
            PitWindowSnapshot pitWindowSnapshot)
        {
            gameRunning = isGameRunning;
            spotter = spotterSnapshot;
            damage = damageSnapshot;
            fuel = fuelSnapshot;
            strategy = strategySnapshot;
            pitWindow = pitWindowSnapshot;
        }

        public void Reset()
        {
            gameRunning = false;
            spotter = null;
            damage = null;
            fuel = null;
            strategy = null;
            pitWindow = null;
            engine.Reset(snapshot);
            publisher.Publish(snapshot);
        }

        private void UpdateScheduled()
        {
            engine.Update(gameRunning, spotter, damage, fuel, strategy, pitWindow, snapshot);
            publisher.Publish(snapshot);
        }
    }
}
