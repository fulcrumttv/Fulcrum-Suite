using System;
using Fulcrum.Core.Intelligence;
using Fulcrum.Core.Performance;
using Fulcrum.Core.Radar;
using Fulcrum.Core.Relative.Display;
using Fulcrum.Core.Telemetry;
using Fulcrum.Plugin.Publishing;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Modules
{
    public sealed class IntelligenceModule
    {
        private readonly RaceIntelligenceEngine engine;
        private readonly RaceIntelligenceSnapshot snapshot;
        private readonly IntelligencePublisher publisher;
        private readonly ScheduledTask updateTask;

        private TelemetrySnapshot telemetry;
        private RelativeDisplaySnapshot relative;
        private RadarSnapshot radar;
        private bool gameRunning;

        public IntelligenceModule(PluginManager pluginManager, Type pluginType, UpdateScheduler scheduler)
        {
            if (pluginManager == null) throw new ArgumentNullException(nameof(pluginManager));
            if (pluginType == null) throw new ArgumentNullException(nameof(pluginType));
            if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));

            engine = new RaceIntelligenceEngine();
            snapshot = new RaceIntelligenceSnapshot();
            publisher = new IntelligencePublisher(pluginManager, pluginType);
            updateTask = scheduler.RegisterTask("Race Intelligence", UpdateRates.RelativeHz, UpdateScheduled, false);
            Reset();
        }

        public RaceIntelligenceSnapshot Snapshot
        {
            get { return snapshot; }
        }

        public void SetFrameContext(
            TelemetrySnapshot telemetrySnapshot,
            RelativeDisplaySnapshot relativeSnapshot,
            RadarSnapshot radarSnapshot,
            bool isGameRunning)
        {
            telemetry = telemetrySnapshot;
            relative = relativeSnapshot;
            radar = radarSnapshot;
            gameRunning = isGameRunning;
        }

        public void Reset()
        {
            telemetry = null;
            relative = null;
            radar = null;
            gameRunning = false;
            engine.Reset(snapshot);
            publisher.Publish(snapshot);
        }

        private void UpdateScheduled()
        {
            if (!gameRunning || telemetry == null || relative == null || radar == null)
            {
                engine.Reset(snapshot);
            }
            else
            {
                engine.Update(telemetry, relative, radar, snapshot);
            }

            publisher.Publish(snapshot);
        }
    }
}
