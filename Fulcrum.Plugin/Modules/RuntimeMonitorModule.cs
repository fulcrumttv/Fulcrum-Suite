using System;
using Fulcrum.Core.Performance;
using Fulcrum.Core.Runtime;
using Fulcrum.Core.Telemetry;
using Fulcrum.Plugin.Publishing;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Modules
{
    public sealed class RuntimeMonitorModule
    {
        private readonly RuntimeMonitorEngine engine;
        private readonly RuntimeMonitorSnapshot snapshot;
        private readonly RuntimeMonitorPublisher publisher;
        private readonly ScheduledTask updateTask;

        private bool gameRunning;
        private TelemetrySnapshot telemetry;

        public RuntimeMonitorModule(PluginManager pluginManager, Type pluginType, UpdateScheduler scheduler)
        {
            if (pluginManager == null) throw new ArgumentNullException(nameof(pluginManager));
            if (pluginType == null) throw new ArgumentNullException(nameof(pluginType));
            if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));

            engine = new RuntimeMonitorEngine();
            snapshot = new RuntimeMonitorSnapshot();
            publisher = new RuntimeMonitorPublisher(pluginManager, pluginType);
            updateTask = scheduler.RegisterTask("Runtime Monitor", 10.0, UpdateScheduled, false);
            Reset();
        }

        public RuntimeMonitorSnapshot Snapshot { get { return snapshot; } }

        public void SetFrameContext(bool isGameRunning, bool hasNewFrame, TelemetrySnapshot telemetrySnapshot)
        {
            gameRunning = isGameRunning;
            telemetry = telemetrySnapshot;
            engine.NotifyFrame(isGameRunning, hasNewFrame, telemetrySnapshot);
        }

        public void Reset()
        {
            gameRunning = false;
            telemetry = null;
            engine.Reset();
            snapshot.Reset();
            publisher.Publish(snapshot);
        }

        private void UpdateScheduled()
        {
            engine.Update(gameRunning, telemetry, snapshot);
            publisher.Publish(snapshot);
        }
    }
}
