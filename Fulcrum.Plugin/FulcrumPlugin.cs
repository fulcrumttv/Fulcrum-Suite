using Fulcrum.Core.Performance;
using Fulcrum.Core.Telemetry;
using Fulcrum.Plugin.Modules;
using Fulcrum.Plugin.Publishing;
using Fulcrum.Plugin.Settings;
using Fulcrum.Plugin.UI;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Reflection;
using GameReaderCommon;
using SimHub.Plugins;

namespace Fulcrum.Plugin
{
    [PluginDescription("Fulcrum Suite telemetry engine")]
    [PluginAuthor("FulcrumTTV")]
    [PluginName("Fulcrum Suite")]
    public class FulcrumPlugin : IDataPlugin, IWPFSettingsV2
    {
        private TelemetryReader telemetryReader;
        private TelemetryPublisher telemetryPublisher;
        private TelemetrySnapshot telemetrySnapshot;

        private UpdateScheduler updateScheduler;

        private SessionModule sessionModule;
        private RelativeModule relativeModule;
        private RelativePredictionModule relativePredictionModule;
        private RadarModule radarModule;
        private DeltaModule deltaModule;
        private FuelModule fuelModule;
        private DamageModule damageModule;
        private IntelligenceModule intelligenceModule;
        private StrategyModule strategyModule;
        private PitWindowModule pitWindowModule;
        private SpotterModule spotterModule;
        private StandingsModule standingsModule;
        private SuiteStatusModule suiteStatusModule;
        private RuntimeMonitorModule runtimeMonitorModule;
        private EventHubModule eventHubModule;
        private PerformanceModule performanceModule;
        private SessionDiagnosticsModule sessionDiagnosticsModule;
        private TimingReferenceDiagnosticModule timingReferenceDiagnosticModule;
        private ClassDeltaReferenceModule classDeltaReferenceModule;
        private long infrastructureHeartbeat;

        private RelativeSettingsPublisher relativeSettingsPublisher;
        private RelativeOverlaySettings relativeSettings;
        private DigiFlagsSettings digiFlagsSettings;
        private DigiFlagsPublisher digiFlagsPublisher;
        private TimingReferenceSettings timingReferenceSettings;

        public PluginManager PluginManager
        {
            private get;
            set;
        }

        public DigiFlagsSettings DigiFlagsSettings
        {
            get { if (digiFlagsSettings == null) digiFlagsSettings = DigiFlagsSettingsStore.Load(); return digiFlagsSettings; }
        }

        public TimingReferenceSettings TimingReferenceSettings
        {
            get
            {
                if (timingReferenceSettings == null)
                {
                    timingReferenceSettings = TimingReferenceSettingsStore.Load();
                }

                return timingReferenceSettings;
            }
        }

        public void NotifyTimingReferenceSettingsChanged()
        {
            if (timingReferenceSettings == null) return;

            timingReferenceSettings.Normalize();
            TimingReferenceSettingsStore.Save(timingReferenceSettings);

            if (classDeltaReferenceModule != null)
            {
                classDeltaReferenceModule.NotifyReferenceSettingsChanged();
            }
        }

        public void NotifyDigiFlagsSettingsChanged()
        {
            if (digiFlagsSettings == null) return;
            digiFlagsSettings.Normalize(); DigiFlagsSettingsStore.Save(digiFlagsSettings);
            if (digiFlagsPublisher != null) digiFlagsPublisher.PublishSettings(digiFlagsSettings);
        }

        public RelativeOverlaySettings RelativeSettings
        {
            get
            {
                if (relativeSettings == null)
                {
                    relativeSettings = RelativeSettingsStore.Load();
                }

                return relativeSettings;
            }
        }

        /// <summary>
        /// Text displayed in SimHub's left-side plugin menu.
        /// Required by IWPFSettingsV2.
        /// </summary>
        public string LeftMenuTitle
        {
            get { return "Fulcrum Suite"; }
        }

        /// <summary>
        /// Optional icon displayed in SimHub's left-side plugin menu.
        /// Returning null lets SimHub use its default plugin icon.
        /// Required by IWPFSettingsV2.
        /// </summary>
        public ImageSource PictureIcon
        {
            get
            {
                try
                {
                    Assembly assembly = typeof(FulcrumPlugin).Assembly;
                    using (Stream stream = assembly.GetManifestResourceStream(
                        "Fulcrum.Plugin.Resources.FS_TRANSPARENT_PREVIEW.png"))
                    {
                        if (stream == null) return null;

                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = stream;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        return bitmap;
                    }
                }
                catch
                {
                    return null;
                }
            }
        }

        public Control GetWPFSettingsControl(
            PluginManager pluginManager)
        {
            return new FulcrumSettingsControl(this);
        }

        public void NotifyRelativeSettingsChanged()
        {
            if (relativeSettings == null)
            {
                return;
            }

            relativeSettings.Normalize();
            RelativeSettingsStore.Save(relativeSettings);

            if (relativeSettingsPublisher != null)
            {
                relativeSettingsPublisher.Publish(relativeSettings);
            }
        }

        public void Init(
            PluginManager pluginManager)
        {
            PluginManager =
                pluginManager;

            telemetryReader =
                new TelemetryReader();

            telemetryPublisher =
                new TelemetryPublisher(
                    pluginManager,
                    GetType());

            telemetrySnapshot =
                new TelemetrySnapshot();

            updateScheduler =
                new UpdateScheduler();

            RegisterCoreProperties(
                pluginManager);

            pluginManager.SetPropertyValue(
                "Fulcrum.Infrastructure.Test",
                GetType(),
                1234);
            pluginManager.SetPropertyValue(
                "Fulcrum.Infrastructure.Status",
                GetType(),
                "REGISTERED");

            relativeSettings = RelativeSettingsStore.Load();
            relativeSettingsPublisher =
                new RelativeSettingsPublisher(
                    pluginManager,
                    GetType());
            relativeSettingsPublisher.Publish(relativeSettings);

            digiFlagsSettings = DigiFlagsSettingsStore.Load();
            digiFlagsPublisher = new DigiFlagsPublisher(pluginManager, GetType());
            digiFlagsPublisher.PublishSettings(digiFlagsSettings);

            timingReferenceSettings = TimingReferenceSettingsStore.Load();

            telemetryPublisher.RegisterProperties();

            sessionModule =
                new SessionModule(
                    pluginManager,
                    GetType(),
                    updateScheduler);

            relativeModule =
                new RelativeModule(
                    pluginManager,
                    GetType(),
                    updateScheduler,
                    sessionModule.Database,
                    relativeSettings);

            relativePredictionModule =
                new RelativePredictionModule(
                    pluginManager,
                    GetType(),
                    updateScheduler);

            radarModule =
                new RadarModule(
                    pluginManager,
                    GetType(),
                    updateScheduler);

            deltaModule =
                new DeltaModule(
                    pluginManager,
                    GetType(),
                    updateScheduler);

            fuelModule =
                new FuelModule(
                    pluginManager,
                    GetType(),
                    updateScheduler);

            damageModule =
                new DamageModule(
                    pluginManager,
                    GetType(),
                    updateScheduler);

            intelligenceModule =
                new IntelligenceModule(
                    pluginManager,
                    GetType(),
                    updateScheduler);

            strategyModule =
                new StrategyModule(
                    pluginManager,
                    GetType(),
                    updateScheduler);

            pitWindowModule =
                new PitWindowModule(
                    pluginManager,
                    GetType(),
                    updateScheduler);

            spotterModule =
                new SpotterModule(
                    pluginManager,
                    GetType(),
                    updateScheduler);

            eventHubModule =
                new EventHubModule(
                    pluginManager,
                    GetType(),
                    updateScheduler);

            standingsModule =
                new StandingsModule(
                    pluginManager,
                    GetType(),
                    updateScheduler,
                    relativeModule.ParticipantBuffer,
                    sessionModule.Database);

            runtimeMonitorModule =
                new RuntimeMonitorModule(
                    pluginManager,
                    GetType(),
                    updateScheduler);

            suiteStatusModule =
                new SuiteStatusModule(
                    pluginManager,
                    GetType(),
                    updateScheduler);

            sessionDiagnosticsModule =
                new SessionDiagnosticsModule(
                    pluginManager,
                    GetType(),
                    updateScheduler);

            timingReferenceDiagnosticModule =
                new TimingReferenceDiagnosticModule(
                    pluginManager,
                    GetType(),
                    updateScheduler,
                    relativeModule.ParticipantBuffer,
                    sessionModule.Database);

            classDeltaReferenceModule =
                new ClassDeltaReferenceModule(
                    pluginManager,
                    GetType(),
                    updateScheduler,
                    relativeModule.ParticipantBuffer,
                    sessionModule.Database,
                    timingReferenceSettings);

            performanceModule =
                new PerformanceModule(
                    pluginManager,
                    GetType(),
                    updateScheduler);

            telemetryReader.Reset(
                telemetrySnapshot,
                string.Empty);

            telemetryPublisher.Publish(
                telemetrySnapshot);

            sessionModule.Reset();
            relativeModule.Reset();
            relativePredictionModule.Reset();
            radarModule.Reset();
            deltaModule.Reset();
            fuelModule.Reset();
            damageModule.Reset();
            intelligenceModule.Reset();
            strategyModule.Reset();
            pitWindowModule.Reset();
            spotterModule.Reset();
            eventHubModule.Reset();
            standingsModule.Reset();
            runtimeMonitorModule.Reset();
            suiteStatusModule.Reset();
            sessionDiagnosticsModule.Reset();
            timingReferenceDiagnosticModule.Reset();
            classDeltaReferenceModule.Reset();
            performanceModule.PublishNow();

            pluginManager.SetPropertyValue(
                "Fulcrum.Status",
                GetType(),
                "Plugin loaded");
        }

        public void DataUpdate(
            PluginManager pluginManager,
            ref GameData data)
        {
            performanceModule.BeginUpdate();

            infrastructureHeartbeat++;
            pluginManager.SetPropertyValue(
                "Fulcrum.Infrastructure.Heartbeat",
                GetType(),
                infrastructureHeartbeat);
            pluginManager.SetPropertyValue(
                "Fulcrum.Infrastructure.Status",
                GetType(),
                "DATAUPDATE");

            try
            {
                UpdateGameProperties(
                    pluginManager,
                    ref data);

                if (!data.GameRunning)
                {
                    HandleGameNotRunning(
                        pluginManager,
                        data.GameName);

                    updateScheduler.Update();

                    return;
                }

                if (data.NewData == null)
                {
                    runtimeMonitorModule.SetFrameContext(
                        true,
                        false,
                        telemetrySnapshot);
                    sessionModule.SetFrameContext(
                        null,
                        true);
                    relativeModule.SetFrameContext(
                        null,
                        true,
                        telemetrySnapshot.PlayerCarIndex,
                        telemetrySnapshot.SessionType);
                    relativePredictionModule.SetFrameContext(
                        relativeModule.DisplaySnapshot,
                        true);
                    radarModule.SetFrameContext(
                        null,
                        true);
                    deltaModule.SetFrameContext(
                        null,
                        true);
                    fuelModule.SetFrameContext(
                        null,
                        telemetrySnapshot,
                        true);
                    damageModule.SetFrameContext(
                        null,
                        true);
                    intelligenceModule.SetFrameContext(
                        telemetrySnapshot,
                        relativeModule.DisplaySnapshot,
                        radarModule.Snapshot,
                        true);
                    strategyModule.SetFrameContext(
                        telemetrySnapshot,
                        fuelModule.Snapshot,
                        intelligenceModule.Snapshot,
                        relativeModule.DisplaySnapshot,
                        true);
                    pitWindowModule.SetFrameContext(
                        telemetrySnapshot,
                        fuelModule.Snapshot,
                        strategyModule.Snapshot,
                        true);
                    spotterModule.SetFrameContext(
                        telemetrySnapshot,
                        radarModule.Snapshot,
                        intelligenceModule.Snapshot,
                        damageModule.Snapshot,
                        true);
                    eventHubModule.SetFrameContext(
                        true,
                        spotterModule.Snapshot,
                        damageModule.Snapshot,
                        fuelModule.Snapshot,
                        strategyModule.Snapshot,
                        pitWindowModule.Snapshot);
                    standingsModule.SetFrameContext(
                        true,
                        telemetrySnapshot.PlayerCarIndex);
                    suiteStatusModule.SetFrameContext(
                        true,
                        relativeModule.DisplaySnapshot,
                        radarModule.Snapshot,
                        fuelModule.Snapshot,
                        damageModule.Snapshot,
                        deltaModule.Snapshot,
                        spotterModule.Snapshot,
                        pitWindowModule.Snapshot,
                        strategyModule.Snapshot,
                        standingsModule.Snapshot);
                    sessionDiagnosticsModule.SetFrameContext(
                        null,
                        true);
                    timingReferenceDiagnosticModule.SetFrameContext(
                        null,
                        true,
                        telemetrySnapshot.PlayerCarIndex);
                    classDeltaReferenceModule.SetFrameContext(
                        null,
                        true,
                        telemetrySnapshot.PlayerCarIndex);

                    pluginManager.SetPropertyValue(
                        "Fulcrum.Status",
                        GetType(),
                        "Waiting for telemetry");

                    updateScheduler.Update();

                    return;
                }

                object rawData =
                    data.NewData.GetRawDataObject();

                UpdateRawTypeProperty(
                    pluginManager,
                    rawData);

                telemetryReader.Update(
                    rawData,
                    data.GameRunning,
                    data.GameName,
                    telemetrySnapshot);

                telemetryPublisher.Publish(
                    telemetrySnapshot);

                runtimeMonitorModule.SetFrameContext(
                    true,
                    true,
                    telemetrySnapshot);

                sessionModule.SetFrameContext(
                    rawData,
                    true);
                relativeModule.SetFrameContext(
                    rawData,
                    true,
                    telemetrySnapshot.PlayerCarIndex,
                    telemetrySnapshot.SessionType);
                relativePredictionModule.SetFrameContext(
                    relativeModule.DisplaySnapshot,
                    true);
                radarModule.SetFrameContext(
                    rawData,
                    true);
                deltaModule.SetFrameContext(
                    rawData,
                    true);
                // Fuel uses SimHub's normalized game frame as its primary source.
                // iRacing raw telemetry does not expose the same Fuel/MaxFuel aliases
                // used by dashboards, while data.NewData does from the first frame.
                fuelModule.SetFrameContext(
                    data.NewData,
                    telemetrySnapshot,
                    true);
                damageModule.SetFrameContext(
                    rawData,
                    true);
                intelligenceModule.SetFrameContext(
                    telemetrySnapshot,
                    relativeModule.DisplaySnapshot,
                    radarModule.Snapshot,
                    true);
                strategyModule.SetFrameContext(
                    telemetrySnapshot,
                    fuelModule.Snapshot,
                    intelligenceModule.Snapshot,
                    relativeModule.DisplaySnapshot,
                    true);
                pitWindowModule.SetFrameContext(
                    telemetrySnapshot,
                    fuelModule.Snapshot,
                    strategyModule.Snapshot,
                    true);
                spotterModule.SetFrameContext(
                    telemetrySnapshot,
                    radarModule.Snapshot,
                    intelligenceModule.Snapshot,
                    damageModule.Snapshot,
                    true);
                standingsModule.SetFrameContext(
                    true,
                    telemetrySnapshot.PlayerCarIndex);
                suiteStatusModule.SetFrameContext(
                    true,
                    relativeModule.DisplaySnapshot,
                    radarModule.Snapshot,
                    fuelModule.Snapshot,
                    damageModule.Snapshot,
                    deltaModule.Snapshot,
                    spotterModule.Snapshot,
                    pitWindowModule.Snapshot,
                    strategyModule.Snapshot,
                    standingsModule.Snapshot);
                sessionDiagnosticsModule.SetFrameContext(
                    rawData,
                    true);
                timingReferenceDiagnosticModule.SetFrameContext(
                    rawData,
                    true,
                    telemetrySnapshot.PlayerCarIndex);
                classDeltaReferenceModule.SetFrameContext(
                    rawData,
                    true,
                    telemetrySnapshot.PlayerCarIndex);

                pluginManager.SetPropertyValue(
                    "Fulcrum.Telemetry.DirectLookup",
                    GetType(),
                    telemetryReader.IsUsingDirectLookup);

                pluginManager.SetPropertyValue(
                    "Fulcrum.Status",
                    GetType(),
                    "Telemetry active");

                updateScheduler.Update();
                digiFlagsPublisher.Update(rawData, telemetrySnapshot.SessionFlags, damageModule.Snapshot.IncidentDelta, digiFlagsSettings);
            }
            finally
            {
                performanceModule.EndUpdate();
            }
        }

        public void End(
            PluginManager pluginManager)
        {
            if (telemetryReader != null &&
                telemetrySnapshot != null)
            {
                telemetryReader.Reset(
                    telemetrySnapshot,
                    string.Empty);
            }

            if (telemetryPublisher != null &&
                telemetrySnapshot != null)
            {
                telemetryPublisher.Publish(
                    telemetrySnapshot);
            }

            if (sessionModule != null)
            {
                sessionModule.Reset();
            }

            if (relativeModule != null)
            {
                relativeModule.Reset();
            }

            if (relativePredictionModule != null)
            {
                relativePredictionModule.Reset();
            }

            if (radarModule != null)
            {
                radarModule.Reset();
            }

            if (deltaModule != null)
            {
                deltaModule.Reset();
            }

            if (fuelModule != null)
            {
                fuelModule.Reset();
            }

            if (damageModule != null)
            {
                damageModule.Reset();
            }

            if (intelligenceModule != null)
            {
                intelligenceModule.Reset();
            }

            if (strategyModule != null)
            {
                strategyModule.Reset();
            }

            if (pitWindowModule != null)
            {
                pitWindowModule.Reset();
            }

            if (spotterModule != null)
            {
                spotterModule.Reset();
            }

            if (eventHubModule != null)
            {
                eventHubModule.Reset();
            }

            if (standingsModule != null)
            {
                standingsModule.Reset();
            }

            if (runtimeMonitorModule != null)
            {
                runtimeMonitorModule.Reset();
            }

            if (suiteStatusModule != null)
            {
                suiteStatusModule.Reset();
            }

            if (sessionDiagnosticsModule != null)
            {
                sessionDiagnosticsModule.Reset();
            }

            if (timingReferenceDiagnosticModule != null)
            {
                timingReferenceDiagnosticModule.Reset();
            }

            if (classDeltaReferenceModule != null)
            {
                classDeltaReferenceModule.Reset();
            }

            if (performanceModule != null)
            {
                performanceModule.PublishNow();
            }

            if (updateScheduler != null)
            {
                updateScheduler.Clear();
            }

            if (relativeSettings != null)
            {
                RelativeSettingsStore.Save(relativeSettings);
            }

            if (timingReferenceSettings != null)
            {
                TimingReferenceSettingsStore.Save(timingReferenceSettings);
            }

            pluginManager.SetPropertyValue(
                "Fulcrum.GameRunning",
                GetType(),
                false);

            pluginManager.SetPropertyValue(
                "Fulcrum.Telemetry.DirectLookup",
                GetType(),
                false);

            pluginManager.SetPropertyValue(
                "Fulcrum.Status",
                GetType(),
                "Plugin stopped");
        }

        private void RegisterCoreProperties(
            PluginManager pluginManager)
        {
            pluginManager.AddProperty(
                "Fulcrum.Version",
                GetType(),
                "4.1.57",
                "Fulcrum Suite plugin version");

            pluginManager.AddProperty(
                "Fulcrum.Status",
                GetType(),
                "Initializing",
                "Current Fulcrum Suite status");

            pluginManager.AddProperty(
                "Fulcrum.GameRunning",
                GetType(),
                false,
                "True while a supported game is running");

            pluginManager.AddProperty(
                "Fulcrum.GameName",
                GetType(),
                string.Empty,
                "Current game name");

            pluginManager.AddProperty(
                "Fulcrum.RawType",
                GetType(),
                string.Empty,
                "Full type name of the raw telemetry object");

            pluginManager.AddProperty(
                "Fulcrum.Diagnostics.Enabled",
                GetType(),
                false,
                "True when development diagnostics are enabled");

            pluginManager.AddProperty(
                "Fulcrum.Telemetry.DirectLookup",
                GetType(),
                false,
                "True when telemetry supports direct key lookup");

            pluginManager.AddProperty(
                "Fulcrum.Infrastructure.Test",
                GetType(),
                0,
                "Infrastructure property registration test");

            pluginManager.AddProperty(
                "Fulcrum.Infrastructure.Heartbeat",
                GetType(),
                0L,
                "DataUpdate heartbeat counter");

            pluginManager.AddProperty(
                "Fulcrum.Infrastructure.Status",
                GetType(),
                "INITIALIZING",
                "Infrastructure registration status");
        }

        private void HandleGameNotRunning(
            PluginManager pluginManager,
            string gameName)
        {
            telemetryReader.Reset(
                telemetrySnapshot,
                gameName);

            telemetryPublisher.Publish(
                telemetrySnapshot);

            runtimeMonitorModule.SetFrameContext(
                false,
                false,
                telemetrySnapshot);

            sessionModule.SetFrameContext(
                null,
                false);

            relativeModule.SetFrameContext(
                null,
                false,
                -1);

            relativePredictionModule.SetFrameContext(
                relativeModule.DisplaySnapshot,
                false);

            radarModule.SetFrameContext(
                null,
                false);

            deltaModule.SetFrameContext(
                null,
                false);

            fuelModule.SetFrameContext(
                null,
                telemetrySnapshot,
                false);

            damageModule.SetFrameContext(
                null,
                false);

            intelligenceModule.SetFrameContext(
                telemetrySnapshot,
                relativeModule.DisplaySnapshot,
                radarModule.Snapshot,
                false);

            strategyModule.SetFrameContext(
                telemetrySnapshot,
                fuelModule.Snapshot,
                intelligenceModule.Snapshot,
                relativeModule.DisplaySnapshot,
                false);

            pitWindowModule.SetFrameContext(
                telemetrySnapshot,
                fuelModule.Snapshot,
                strategyModule.Snapshot,
                false);

            spotterModule.SetFrameContext(
                telemetrySnapshot,
                radarModule.Snapshot,
                intelligenceModule.Snapshot,
                damageModule.Snapshot,
                false);
            eventHubModule.SetFrameContext(
                false,
                spotterModule.Snapshot,
                damageModule.Snapshot,
                fuelModule.Snapshot,
                strategyModule.Snapshot,
                pitWindowModule.Snapshot);

            standingsModule.SetFrameContext(
                false,
                -1);

            sessionModule.Reset();
            relativeModule.Reset();
            relativePredictionModule.Reset();
            radarModule.Reset();
            deltaModule.Reset();
            fuelModule.Reset();
            damageModule.Reset();
            intelligenceModule.Reset();
            strategyModule.Reset();
            pitWindowModule.Reset();
            spotterModule.Reset();
            eventHubModule.Reset();
            standingsModule.Reset();
            runtimeMonitorModule.Reset();
            suiteStatusModule.Reset();

            sessionDiagnosticsModule.SetFrameContext(
                null,
                false);
            timingReferenceDiagnosticModule.SetFrameContext(
                null,
                false,
                -1);
            classDeltaReferenceModule.SetFrameContext(
                null,
                false,
                -1);

            sessionDiagnosticsModule.Reset();
            timingReferenceDiagnosticModule.Reset();
            classDeltaReferenceModule.Reset();

            pluginManager.SetPropertyValue(
                "Fulcrum.RawType",
                GetType(),
                string.Empty);

            pluginManager.SetPropertyValue(
                "Fulcrum.Telemetry.DirectLookup",
                GetType(),
                false);

            if (digiFlagsPublisher != null)
            {
                digiFlagsPublisher.ResetRuntime();
            }

            pluginManager.SetPropertyValue(
                "Fulcrum.Status",
                GetType(),
                "Waiting for game");
        }

        private void UpdateGameProperties(
            PluginManager pluginManager,
            ref GameData data)
        {
            pluginManager.SetPropertyValue(
                "Fulcrum.GameRunning",
                GetType(),
                data.GameRunning);

            pluginManager.SetPropertyValue(
                "Fulcrum.GameName",
                GetType(),
                data.GameName ?? string.Empty);
        }

        private void UpdateRawTypeProperty(
            PluginManager pluginManager,
            object rawData)
        {
            string rawType =
                rawData != null
                    ? rawData.GetType().FullName
                    : "NULL";

            pluginManager.SetPropertyValue(
                "Fulcrum.RawType",
                GetType(),
                rawType);
        }
    }
}
