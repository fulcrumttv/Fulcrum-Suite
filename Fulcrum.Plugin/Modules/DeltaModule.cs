using System;
using Fulcrum.Core.Delta;
using Fulcrum.Core.Performance;
using Fulcrum.Plugin.Publishing;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Modules
{
    /// <summary>
    /// Publishes a low-latency native iRacing lap delta at 30 Hz.
    /// </summary>
    public sealed class DeltaModule
    {
        private readonly DeltaReader reader;
        private readonly DeltaSnapshot snapshot;
        private readonly DeltaPublisher publisher;
        private readonly ScheduledTask updateTask;

        private object latestRawData;
        private bool latestGameRunning;

        public DeltaModule(PluginManager pluginManager, Type pluginType, UpdateScheduler scheduler)
        {
            if (pluginManager == null) throw new ArgumentNullException(nameof(pluginManager));
            if (pluginType == null) throw new ArgumentNullException(nameof(pluginType));
            if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));

            reader = new DeltaReader();
            snapshot = new DeltaSnapshot();
            publisher = new DeltaPublisher(pluginManager, pluginType);
            updateTask = scheduler.RegisterTask("Delta Module", UpdateRates.DeltaHz, UpdateScheduled, false);
            Reset();
        }

        public DeltaSnapshot Snapshot
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
