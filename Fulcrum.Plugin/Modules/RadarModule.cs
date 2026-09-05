using System;
using Fulcrum.Core.Performance;
using Fulcrum.Core.Radar;
using Fulcrum.Plugin.Publishing;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Modules
{
    /// <summary>
    /// First functional Fulcrum Radar module. It publishes iRacing's native
    /// left/right spotter state at a stable 30 Hz update rate.
    /// </summary>
    public sealed class RadarModule
    {
        private readonly RadarReader reader;
        private readonly RadarSnapshot snapshot;
        private readonly RadarPublisher publisher;
        private readonly ScheduledTask updateTask;

        private object latestRawData;
        private bool latestGameRunning;

        public RadarModule(PluginManager pluginManager, Type pluginType, UpdateScheduler scheduler)
        {
            if (pluginManager == null) throw new ArgumentNullException(nameof(pluginManager));
            if (pluginType == null) throw new ArgumentNullException(nameof(pluginType));
            if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));

            reader = new RadarReader();
            snapshot = new RadarSnapshot();
            publisher = new RadarPublisher(pluginManager, pluginType);
            updateTask = scheduler.RegisterTask("Radar Module", UpdateRates.RadarHz, UpdateScheduled, false);
            Reset();
        }

        public RadarSnapshot Snapshot
        {
            get { return snapshot; }
        }

        public void SetFrameContext(object rawData, bool gameRunning)
        {
            latestRawData = rawData;
            latestGameRunning = gameRunning;
        }

        public void Reset()
        {
            latestRawData = null;
            latestGameRunning = false;
            reader.Reset(snapshot);
            publisher.Publish(reader, snapshot);
        }

        private void UpdateScheduled()
        {
            if (!latestGameRunning || latestRawData == null)
            {
                reader.Reset(snapshot);
            }
            else
            {
                reader.Update(latestRawData, snapshot);
            }

            publisher.Publish(reader, snapshot);
        }
    }
}
