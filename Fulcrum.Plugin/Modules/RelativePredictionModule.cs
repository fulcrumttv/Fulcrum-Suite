using System;
using Fulcrum.Core.Performance;
using Fulcrum.Core.Relative.Display;
using Fulcrum.Core.Relative.Prediction;
using Fulcrum.Plugin.Publishing;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Modules
{
    /// <summary>
    /// Produces stable closing-rate and battle predictions from the Relative.
    /// </summary>
    public sealed class RelativePredictionModule
    {
        private readonly RelativePredictionEngine engine;
        private readonly RelativePredictionSnapshot snapshot;
        private readonly RelativePredictionPublisher publisher;

        private RelativeDisplaySnapshot latestRelative;
        private bool latestGameRunning;

        public RelativePredictionModule(
            PluginManager pluginManager,
            Type pluginType,
            UpdateScheduler scheduler)
        {
            if (pluginManager == null)
            {
                throw new ArgumentNullException(nameof(pluginManager));
            }

            if (pluginType == null)
            {
                throw new ArgumentNullException(nameof(pluginType));
            }

            if (scheduler == null)
            {
                throw new ArgumentNullException(nameof(scheduler));
            }

            engine = new RelativePredictionEngine();
            snapshot = new RelativePredictionSnapshot();
            publisher = new RelativePredictionPublisher(pluginManager, pluginType);

            scheduler.RegisterTask(
                "Relative Prediction",
                UpdateRates.RelativeHz,
                UpdateScheduled,
                false);

            Reset();
        }

        public RelativePredictionSnapshot Snapshot
        {
            get { return snapshot; }
        }

        public void SetFrameContext(
            RelativeDisplaySnapshot relative,
            bool gameRunning)
        {
            latestRelative = relative;
            latestGameRunning = gameRunning;
        }

        public void Reset()
        {
            latestRelative = null;
            latestGameRunning = false;
            engine.Reset(snapshot);
            publisher.Publish(snapshot);
        }

        private void UpdateScheduled()
        {
            if (!latestGameRunning || latestRelative == null)
            {
                engine.Reset(snapshot);
                publisher.Publish(snapshot);
                return;
            }

            engine.Update(latestRelative, snapshot);
            publisher.Publish(snapshot);
        }
    }
}
