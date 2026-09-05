using System;
using Fulcrum.Core.Performance;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Modules
{
    /// <summary>
    /// Measures Fulcrum update performance and publishes
    /// performance properties to SimHub.
    /// </summary>
    public sealed class PerformanceModule
    {
        private readonly PluginManager pluginManager;
        private readonly Type pluginType;

        private readonly PerformanceMonitor performanceMonitor;
        private readonly ScheduledTask publisherTask;

        public PerformanceModule(
            PluginManager pluginManager,
            Type pluginType,
            UpdateScheduler updateScheduler)
        {
            if (pluginManager == null)
            {
                throw new ArgumentNullException(
                    nameof(pluginManager));
            }

            if (pluginType == null)
            {
                throw new ArgumentNullException(
                    nameof(pluginType));
            }

            if (updateScheduler == null)
            {
                throw new ArgumentNullException(
                    nameof(updateScheduler));
            }

            this.pluginManager =
                pluginManager;

            this.pluginType =
                pluginType;

            performanceMonitor =
                new PerformanceMonitor();

            RegisterProperties();

            publisherTask =
                updateScheduler.RegisterTask(
                    "Performance Publisher",
                    UpdateRates.PerformancePublisherHz,
                    PublishProperties,
                    false);

            PublishProperties();
        }

        /// <summary>
        /// Starts measuring one complete Fulcrum plugin update.
        /// </summary>
        public void BeginUpdate()
        {
            performanceMonitor.BeginUpdate();
        }

        /// <summary>
        /// Finishes measuring one complete Fulcrum plugin update.
        /// </summary>
        public void EndUpdate()
        {
            performanceMonitor.EndUpdate();
        }

        /// <summary>
        /// Publishes the latest available values immediately.
        /// </summary>
        public void PublishNow()
        {
            PublishProperties();
        }

        private void RegisterProperties()
        {
            pluginManager.AddProperty(
                "Fulcrum.Performance.LastUpdateMs",
                pluginType,
                0.0,
                "Execution time of the latest Fulcrum update");

            pluginManager.AddProperty(
                "Fulcrum.Performance.AverageUpdateMs",
                pluginType,
                0.0,
                "Average Fulcrum update time");

            pluginManager.AddProperty(
                "Fulcrum.Performance.PeakUpdateMs",
                pluginType,
                0.0,
                "Highest Fulcrum update time");

            pluginManager.AddProperty(
                "Fulcrum.Performance.UpdatesPerSecond",
                pluginType,
                0.0,
                "Number of Fulcrum updates per second");

            pluginManager.AddProperty(
                "Fulcrum.Performance.TotalUpdateCount",
                pluginType,
                0L,
                "Total number of Fulcrum updates");

            pluginManager.AddProperty(
                "Fulcrum.Performance.PublisherExecutionCount",
                pluginType,
                0L,
                "Number of performance property publications");

            pluginManager.AddProperty(
                "Fulcrum.Performance.PublisherLastExecutionMs",
                pluginType,
                0.0,
                "Latest performance publication time");

            pluginManager.AddProperty(
                "Fulcrum.Performance.PublisherPeakExecutionMs",
                pluginType,
                0.0,
                "Peak performance publication time");
        }

        private void PublishProperties()
        {
            pluginManager.SetPropertyValue(
                "Fulcrum.Performance.LastUpdateMs",
                pluginType,
                performanceMonitor.LastUpdateMilliseconds);

            pluginManager.SetPropertyValue(
                "Fulcrum.Performance.AverageUpdateMs",
                pluginType,
                performanceMonitor.AverageUpdateMilliseconds);

            pluginManager.SetPropertyValue(
                "Fulcrum.Performance.PeakUpdateMs",
                pluginType,
                performanceMonitor.PeakUpdateMilliseconds);

            pluginManager.SetPropertyValue(
                "Fulcrum.Performance.UpdatesPerSecond",
                pluginType,
                performanceMonitor.UpdatesPerSecond);

            pluginManager.SetPropertyValue(
                "Fulcrum.Performance.TotalUpdateCount",
                pluginType,
                performanceMonitor.TotalUpdateCount);

            pluginManager.SetPropertyValue(
                "Fulcrum.Performance.PublisherExecutionCount",
                pluginType,
                publisherTask != null
                    ? publisherTask.ExecutionCount
                    : 0L);

            pluginManager.SetPropertyValue(
                "Fulcrum.Performance.PublisherLastExecutionMs",
                pluginType,
                publisherTask != null
                    ? publisherTask.LastExecutionMilliseconds
                    : 0.0);

            pluginManager.SetPropertyValue(
                "Fulcrum.Performance.PublisherPeakExecutionMs",
                pluginType,
                publisherTask != null
                    ? publisherTask.PeakExecutionMilliseconds
                    : 0.0);
        }
    }
}