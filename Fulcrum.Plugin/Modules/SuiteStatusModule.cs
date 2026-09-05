using System;
using Fulcrum.Core.Damage;
using Fulcrum.Core.Delta;
using Fulcrum.Core.Fuel;
using Fulcrum.Core.Performance;
using Fulcrum.Core.PitWindow;
using Fulcrum.Core.Radar;
using Fulcrum.Core.Relative.Display;
using Fulcrum.Core.Runtime;
using Fulcrum.Core.Spotter;
using Fulcrum.Core.Standings;
using Fulcrum.Core.Strategy;
using Fulcrum.Plugin.Publishing;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Modules
{
    public sealed class SuiteStatusModule
    {
        private readonly SuiteStatusEngine engine;
        private readonly SuiteStatusSnapshot snapshot;
        private readonly SuiteStatusPublisher publisher;
        private readonly ScheduledTask updateTask;

        private bool gameRunning;
        private RelativeDisplaySnapshot relative;
        private RadarSnapshot radar;
        private FuelSnapshot fuel;
        private DamageSnapshot damage;
        private DeltaSnapshot delta;
        private SpotterSnapshot spotter;
        private PitWindowSnapshot pitWindow;
        private StrategySnapshot strategy;
        private StandingsSnapshot standings;

        public SuiteStatusModule(PluginManager pluginManager, Type pluginType, UpdateScheduler scheduler)
        {
            if (pluginManager == null) throw new ArgumentNullException(nameof(pluginManager));
            if (pluginType == null) throw new ArgumentNullException(nameof(pluginType));
            if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));

            engine = new SuiteStatusEngine();
            snapshot = new SuiteStatusSnapshot();
            publisher = new SuiteStatusPublisher(pluginManager, pluginType);
            updateTask = scheduler.RegisterTask("Suite Status", 10.0, UpdateScheduled, false);
            Reset();
        }

        public SuiteStatusSnapshot Snapshot { get { return snapshot; } }

        public void SetFrameContext(
            bool isGameRunning,
            RelativeDisplaySnapshot relativeSnapshot,
            RadarSnapshot radarSnapshot,
            FuelSnapshot fuelSnapshot,
            DamageSnapshot damageSnapshot,
            DeltaSnapshot deltaSnapshot,
            SpotterSnapshot spotterSnapshot,
            PitWindowSnapshot pitWindowSnapshot,
            StrategySnapshot strategySnapshot,
            StandingsSnapshot standingsSnapshot)
        {
            gameRunning = isGameRunning;
            relative = relativeSnapshot;
            radar = radarSnapshot;
            fuel = fuelSnapshot;
            damage = damageSnapshot;
            delta = deltaSnapshot;
            spotter = spotterSnapshot;
            pitWindow = pitWindowSnapshot;
            strategy = strategySnapshot;
            standings = standingsSnapshot;
        }

        public void Reset()
        {
            gameRunning = false;
            relative = null;
            radar = null;
            fuel = null;
            damage = null;
            delta = null;
            spotter = null;
            pitWindow = null;
            strategy = null;
            standings = null;
            engine.Reset(snapshot);
            publisher.Publish(snapshot);
        }

        private void UpdateScheduled()
        {
            engine.Update(gameRunning, relative, radar, fuel, damage, delta, spotter, pitWindow, strategy, standings, snapshot);
            publisher.Publish(snapshot);
        }
    }
}
