using System;
using Fulcrum.Core.Damage;
using Fulcrum.Core.Intelligence;
using Fulcrum.Core.Performance;
using Fulcrum.Core.Radar;
using Fulcrum.Core.Spotter;
using Fulcrum.Core.Telemetry;
using Fulcrum.Plugin.Publishing;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Modules
{
    public sealed class SpotterModule
    {
        private readonly SpotterEngine engine;
        private readonly SpotterSnapshot snapshot;
        private readonly SpotterPublisher publisher;
        private readonly ScheduledTask updateTask;

        private TelemetrySnapshot telemetry;
        private RadarSnapshot radar;
        private RaceIntelligenceSnapshot intelligence;
        private DamageSnapshot damage;
        private bool gameRunning;

        public SpotterModule(PluginManager pluginManager, Type pluginType, UpdateScheduler scheduler)
        {
            if (pluginManager == null) throw new ArgumentNullException(nameof(pluginManager));
            if (pluginType == null) throw new ArgumentNullException(nameof(pluginType));
            if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));

            engine = new SpotterEngine();
            snapshot = new SpotterSnapshot();
            publisher = new SpotterPublisher(pluginManager, pluginType);
            updateTask = scheduler.RegisterTask("Spotter Engine", UpdateRates.RadarHz, UpdateScheduled, false);
            Reset();
        }

        public SpotterSnapshot Snapshot
        {
            get { return snapshot; }
        }

        public void SetFrameContext(
            TelemetrySnapshot telemetrySnapshot,
            RadarSnapshot radarSnapshot,
            RaceIntelligenceSnapshot intelligenceSnapshot,
            DamageSnapshot damageSnapshot,
            bool isGameRunning)
        {
            telemetry = telemetrySnapshot;
            radar = radarSnapshot;
            intelligence = intelligenceSnapshot;
            damage = damageSnapshot;
            gameRunning = isGameRunning;
        }

        public void Reset()
        {
            telemetry = null;
            radar = null;
            intelligence = null;
            damage = null;
            gameRunning = false;
            engine.Reset(snapshot);
            publisher.Publish(snapshot);
        }

        private void UpdateScheduled()
        {
            if (!gameRunning || telemetry == null || radar == null ||
                intelligence == null || damage == null)
            {
                engine.Reset(snapshot);
            }
            else
            {
                engine.Update(telemetry, radar, intelligence, damage, snapshot);
            }

            publisher.Publish(snapshot);
        }
    }
}
