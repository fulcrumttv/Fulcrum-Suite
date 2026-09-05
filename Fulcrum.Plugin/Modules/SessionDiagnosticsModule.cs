using System;
using Fulcrum.Core.Performance;
using Fulcrum.Core.Session;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Modules
{
    /// <summary>
    /// Captures and publishes the structure of SimHub's
    /// CurrentSessionInfo object for development diagnostics.
    /// </summary>
    public sealed class SessionDiagnosticsModule
    {
        private const double DiagnosticsUpdateHz = 1.0;

        private readonly PluginManager pluginManager;
        private readonly Type pluginType;
        private readonly SessionInfoInspector inspector;
        private readonly ScheduledTask updateTask;

        private object latestRawData;
        private bool latestGameRunning;

        public SessionDiagnosticsModule(
            PluginManager pluginManager,
            Type pluginType,
            UpdateScheduler updateScheduler)
        {
            if (pluginManager == null)
            {
                throw new ArgumentNullException(nameof(pluginManager));
            }

            if (pluginType == null)
            {
                throw new ArgumentNullException(nameof(pluginType));
            }

            if (updateScheduler == null)
            {
                throw new ArgumentNullException(nameof(updateScheduler));
            }

            this.pluginManager = pluginManager;
            this.pluginType = pluginType;

            inspector = new SessionInfoInspector();

            RegisterProperties();

            updateTask = updateScheduler.RegisterTask(
                "Session Diagnostics",
                DiagnosticsUpdateHz,
                UpdateScheduled,
                false);

            Reset();
        }

        public void SetFrameContext(
            object rawData,
            bool gameRunning)
        {
            latestRawData = rawData;
            latestGameRunning = gameRunning;
        }

        public void Reset()
        {
            latestRawData = null;
            latestGameRunning = false;

            inspector.Reset();
            PublishCurrentState();
        }

        private void RegisterProperties()
        {
            pluginManager.AddProperty(
                "Fulcrum.Diagnostics.Session.HasData",
                pluginType,
                false,
                "True when CurrentSessionInfo is available");

            pluginManager.AddProperty(
                "Fulcrum.Diagnostics.Session.RawDataType",
                pluginType,
                string.Empty,
                "Runtime type of the raw SimHub telemetry object");

            pluginManager.AddProperty(
                "Fulcrum.Diagnostics.Session.InfoType",
                pluginType,
                string.Empty,
                "Runtime type of CurrentSessionInfo");

            pluginManager.AddProperty(
                "Fulcrum.Diagnostics.Session.Structure",
                pluginType,
                string.Empty,
                "Reflected CurrentSessionInfo object structure");

            pluginManager.AddProperty(
                "Fulcrum.Diagnostics.Session.Error",
                pluginType,
                string.Empty,
                "Latest SessionInfo diagnostic error");

            pluginManager.AddProperty(
                "Fulcrum.Diagnostics.Session.CapturedAtUtc",
                pluginType,
                string.Empty,
                "UTC timestamp of the latest successful capture");

            pluginManager.AddProperty(
                "Fulcrum.Diagnostics.Session.ExecutionCount",
                pluginType,
                0L,
                "Number of SessionInfo diagnostic executions");

            pluginManager.AddProperty(
                "Fulcrum.Diagnostics.Session.LastExecutionMs",
                pluginType,
                0.0,
                "Latest SessionInfo diagnostic execution time");
        }

        private void UpdateScheduled()
        {
            if (!latestGameRunning || latestRawData == null)
            {
                inspector.Reset();
                PublishCurrentState();
                return;
            }

            inspector.Inspect(latestRawData);
            PublishCurrentState();
        }

        private void PublishCurrentState()
        {
            pluginManager.SetPropertyValue(
                "Fulcrum.Diagnostics.Session.HasData",
                pluginType,
                inspector.HasSessionInfo);

            pluginManager.SetPropertyValue(
                "Fulcrum.Diagnostics.Session.RawDataType",
                pluginType,
                inspector.RawDataType ?? string.Empty);

            pluginManager.SetPropertyValue(
                "Fulcrum.Diagnostics.Session.InfoType",
                pluginType,
                inspector.SessionInfoType ?? string.Empty);

            pluginManager.SetPropertyValue(
                "Fulcrum.Diagnostics.Session.Structure",
                pluginType,
                inspector.Structure ?? string.Empty);

            pluginManager.SetPropertyValue(
                "Fulcrum.Diagnostics.Session.Error",
                pluginType,
                inspector.Error ?? string.Empty);

            pluginManager.SetPropertyValue(
                "Fulcrum.Diagnostics.Session.CapturedAtUtc",
                pluginType,
                inspector.CapturedAtUtc == DateTime.MinValue
                    ? string.Empty
                    : inspector.CapturedAtUtc.ToString("O"));

            pluginManager.SetPropertyValue(
                "Fulcrum.Diagnostics.Session.ExecutionCount",
                pluginType,
                updateTask != null ? updateTask.ExecutionCount : 0L);

            pluginManager.SetPropertyValue(
                "Fulcrum.Diagnostics.Session.LastExecutionMs",
                pluginType,
                updateTask != null ? updateTask.LastExecutionMilliseconds : 0.0);
        }
    }
}
