using System;
using Fulcrum.Core.Damage;
using Fulcrum.Core.Performance;
using Fulcrum.Plugin.Publishing;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Modules
{
    public sealed class DamageModule
    {
        private readonly DamageTelemetryReader reader;
        private readonly DamageTelemetry telemetry;
        private readonly DamageEngine engine;
        private readonly DamageSnapshot snapshot;
        private readonly DamagePublisher publisher;
        private readonly ScheduledTask updateTask;
        private object rawData;
        private bool gameRunning;

        public DamageModule(PluginManager pluginManager, Type pluginType, UpdateScheduler scheduler)
        {
            if (pluginManager == null) throw new ArgumentNullException(nameof(pluginManager));
            if (pluginType == null) throw new ArgumentNullException(nameof(pluginType));
            if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));
            reader = new DamageTelemetryReader();
            telemetry = new DamageTelemetry();
            engine = new DamageEngine();
            snapshot = new DamageSnapshot();
            publisher = new DamagePublisher(pluginManager, pluginType);
            updateTask = scheduler.RegisterTask("Damage Engine", UpdateRates.VehicleHealthHz, UpdateScheduled, false);
            Reset();
        }

        public DamageSnapshot Snapshot { get { return snapshot; } }
        public void SetFrameContext(object currentRawData, bool isGameRunning) { rawData = currentRawData; gameRunning = isGameRunning; }
        public void Reset() { rawData = null; gameRunning = false; engine.Reset(snapshot); publisher.Publish(snapshot); }

        private void UpdateScheduled()
        {
            if (!gameRunning || rawData == null)
            {
                engine.Reset(snapshot);
                publisher.Publish(snapshot);
                return;
            }
            if (!reader.TryRead(rawData, telemetry))
            {
                telemetry.Available = false;
            }
            engine.Update(telemetry, snapshot);
            publisher.Publish(snapshot);
        }
    }
}
