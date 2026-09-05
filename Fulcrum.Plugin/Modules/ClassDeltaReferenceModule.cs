using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Fulcrum.Core.Performance;
using Fulcrum.Core.Relative;
using Fulcrum.Core.Session;
using Fulcrum.Plugin.Settings;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Modules
{
    /// <summary>
    /// Experimental multiclass-aware live delta reference.
    ///
    /// iRacing's native LapDeltaToSessionBestLap always follows the absolute
    /// session best.  In multiclass sessions that can be a faster class.  This
    /// module identifies the player's class, finds the best lap in that class,
    /// then builds a class-specific live reference using either:
    ///   1) native session delta when the overall best is already same-class;
    ///   2) a sampled same-class lap trace, scaled to the official class best;
    ///   3) iRacing CarIdxEstTime / CarClassEstimatedLapTime as an immediate
    ///      class-shaped fallback while a real trace is being learned.
    ///
    /// It lives entirely in Fulcrum.Plugin so the user's installed Core DLL can
    /// remain unchanged during validation.
    /// </summary>
    public sealed class ClassDeltaReferenceModule
    {
        private const int Capacity = SessionDatabase.Capacity;
        private const int TraceBuckets = 241; // ~0.42% track resolution
        private const float LapTolerance = 0.035f;
        private const float DeltaSmoothing = 0.34f;
        private const float MinimumTraceCoverage = 0.62f;
        private const float MaximumTraceScaleError = 0.18f;

        // SPEED DELTA is intentionally filtered separately from the main time delta.
        // The time delta must stay responsive; the speed readout is a secondary cue
        // and benefits from more visual stability.
        private const float SpeedDeltaDeadbandKmh = 1.0f;
        private const float SpeedDeltaExitDeadbandKmh = 2.0f;
        private const float SpeedDeltaHysteresisKmh = 1.6f;
        private const float SpeedDeltaTauSeconds = 0.30f;
        private const float SpeedDeltaFastTauSeconds = 0.12f;

        private readonly PluginManager manager;
        private readonly Type pluginType;
        private readonly ParticipantBuffer participants;
        private readonly SessionDatabase database;
        private readonly TimingReferenceSettings timingReferenceSettings;
        private readonly LapTraceState[] traceStates;
        private readonly int[] classIdByCarIndex;

        private object latestRawData;
        private bool latestGameRunning;
        private int latestPlayerCarIndex;

        private int lastSessionNumber;
        private bool hasSessionNumber;
        private string activePlayerClassKey;

        private float[] referenceTrace;
        private float referenceTraceLap;
        private string referenceTraceDriver;
        private int referenceTraceCarIndex;
        private float referenceTraceCoverage;
        private bool referenceTraceTrusted;

        private bool filterInitialized;
        private float filteredDelta;
        private float previousFilteredDelta;
        private DateTime previousFilterUtc;
        private float previousPlayerLapDist;

        private bool speedDeltaFilterInitialized;
        private float filteredSpeedDeltaKmh;
        private int displayedSpeedDeltaKmh;
        private DateTime previousSpeedFilterUtc;

        private bool personalSpeedDeltaFilterInitialized;
        private float personalFilteredSpeedDeltaKmh;
        private int personalDisplayedSpeedDeltaKmh;
        private DateTime previousPersonalSpeedFilterUtc;

        // Shared sector capture. Both Sectors dashboards consume these exact
        // plugin-published values so the Pop-Up and Table cannot disagree due
        // to independent SimHub expression timing.
        private readonly float[] sectorStarts = new float[16];
        private int sectorStartCount;
        private bool sectorCaptureInitialized;
        private int sectorPrev;
        private double sectorStartSessionTime;
        private float sectorStartDelta;
        private int sectorPrevLap;
        private float sectorPrevDist;
        private float sectorPrevDelta;
        private bool sectorPrevDeltaOk;
        private string sectorReferenceMode;
        private float sectorFinishDelta;
        private bool sectorFinishOk;
        private int sectorHistoryCount;
        private readonly int[] sectorHistorySector = new int[3];
        private readonly float[] sectorHistoryTime = new float[3];
        private readonly float[] sectorHistoryDelta = new float[3];
        private readonly bool[] sectorHistoryValid = new bool[3];
        private int sectorLastSector;
        private float sectorLastTime;
        private float sectorLastDelta;
        private bool sectorLastValid;
        private DateTime sectorPopupUntilUtc;

        public ClassDeltaReferenceModule(
            PluginManager manager,
            Type pluginType,
            UpdateScheduler scheduler,
            ParticipantBuffer participants,
            SessionDatabase database,
            TimingReferenceSettings timingReferenceSettings)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.pluginType = pluginType ?? throw new ArgumentNullException(nameof(pluginType));
            this.participants = participants ?? throw new ArgumentNullException(nameof(participants));
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            this.timingReferenceSettings = timingReferenceSettings ?? new TimingReferenceSettings();
            if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));

            traceStates = new LapTraceState[Capacity];
            classIdByCarIndex = new int[Capacity];
            for (int i = 0; i < Capacity; i++)
            {
                traceStates[i] = new LapTraceState(TraceBuckets);
                classIdByCarIndex[i] = -1;
            }

            referenceTrace = new float[TraceBuckets];
            RegisterProperties();
            scheduler.RegisterTask("Class Delta Reference", UpdateRates.DeltaHz, UpdateScheduled, false);
            Reset();
        }

        public void SetFrameContext(object rawData, bool gameRunning, int playerCarIndex)
        {
            latestRawData = rawData;
            latestGameRunning = gameRunning;
            latestPlayerCarIndex = playerCarIndex;
        }

        public void NotifyReferenceSettingsChanged()
        {
            ResetPersonalSpeedDeltaFilter();
            ResetFilter();
            ResetSectorCapture(true);

            Set("Fulcrum.Delta.SelectedReferenceMode", ReferenceModeName());
            Set("Fulcrum.Delta.SelectedReferenceLabel", ReferenceLabel());
        }

        public void Reset()
        {
            latestRawData = null;
            latestGameRunning = false;
            latestPlayerCarIndex = -1;
            hasSessionNumber = false;
            lastSessionNumber = -1;
            activePlayerClassKey = string.Empty;
            ResetTraceLearning();
            ResetFilter();
            ResetSectorCapture(true);
            PublishUnavailable("WAITING FOR IRACING");
            Set("Fulcrum.Delta.PlayerClass", string.Empty);
            Set("Fulcrum.Delta.PlayerClassId", -1);
            Set("Fulcrum.Delta.ClassCarCount", 0);
            Set("Fulcrum.Delta.ClassBestCarIndex", -1);
            Set("Fulcrum.Delta.ClassBestDriver", string.Empty);
            Set("Fulcrum.Delta.ClassBestSeconds", 0.0f);
            Set("Fulcrum.Delta.ClassBestText", "--:--.---");
            Set("Fulcrum.Delta.OverallBestSeconds", 0.0f);
            Set("Fulcrum.Delta.OverallBestDriver", string.Empty);
            Set("Fulcrum.Delta.ClassBestIsOverallBest", false);
            Set("Fulcrum.Delta.ClassTraceReady", false);
            Set("Fulcrum.Delta.ClassTraceDriver", string.Empty);
            Set("Fulcrum.Delta.ClassTraceCarIndex", -1);
            Set("Fulcrum.Delta.ClassTraceLapSeconds", 0.0f);
            Set("Fulcrum.Delta.ClassTraceLapText", "--:--.---");
            Set("Fulcrum.Delta.ClassTraceCoverage", 0.0f);
            Set("Fulcrum.Delta.ClassTraceScale", 0.0f);
            Set("Fulcrum.Delta.ClassTraceTrusted", false);
        }

        private void ResetTraceLearning()
        {
            for (int i = 0; i < traceStates.Length; i++) traceStates[i].ResetAll();
            for (int i = 0; i < classIdByCarIndex.Length; i++) classIdByCarIndex[i] = -1;
            for (int i = 0; i < referenceTrace.Length; i++) referenceTrace[i] = float.NaN;
            referenceTraceLap = 0.0f;
            referenceTraceDriver = string.Empty;
            referenceTraceCarIndex = -1;
            referenceTraceCoverage = 0.0f;
            referenceTraceTrusted = false;
        }

        private void ResetFilter()
        {
            filterInitialized = false;
            filteredDelta = 0.0f;
            previousFilteredDelta = 0.0f;
            previousFilterUtc = DateTime.MinValue;
            previousPlayerLapDist = -1.0f;

            speedDeltaFilterInitialized = false;
            filteredSpeedDeltaKmh = 0.0f;
            displayedSpeedDeltaKmh = 0;
            previousSpeedFilterUtc = DateTime.MinValue;

            ResetPersonalSpeedDeltaFilter();
        }

        private void RegisterProperties()
        {
            Add("Fulcrum.Delta.ClassReady", false, "True when a same-class session-best reference is known");
            Add("Fulcrum.Delta.ClassStatus", "WAITING FOR IRACING", "Class delta reference state");
            Add("Fulcrum.Delta.PlayerClass", string.Empty, "Player class label from session data");
            Add("Fulcrum.Delta.PlayerClassId", -1, "Player iRacing CarIdxClass identifier");
            Add("Fulcrum.Delta.ClassCarCount", 0, "Cars detected in player's class");
            Add("Fulcrum.Delta.ClassBestCarIndex", -1, "CarIdx holding the class session best");
            Add("Fulcrum.Delta.ClassBestDriver", string.Empty, "Driver holding the class session best");
            Add("Fulcrum.Delta.ClassBestSeconds", 0.0f, "Best lap in player's class, seconds");
            Add("Fulcrum.Delta.ClassBestText", "--:--.---", "Best lap in player's class, formatted");
            Add("Fulcrum.Delta.OverallBestSeconds", 0.0f, "Absolute session best, seconds");
            Add("Fulcrum.Delta.OverallBestDriver", string.Empty, "Absolute session-best driver");
            Add("Fulcrum.Delta.ClassBestIsOverallBest", false, "True when class best and overall best are the same reference");
            Add("Fulcrum.Delta.ClassLiveValid", false, "True when class-aware live delta is usable");
            Add("Fulcrum.Delta.ClassLiveSeconds", 0.0f, "Live delta to class best; negative is faster");
            Add("Fulcrum.Delta.ClassLiveText", "--.---", "Formatted live delta to class best");
            Add("Fulcrum.Delta.ClassLiveRate", 0.0f, "Rate of class delta change, compatible with speed-delta estimate");
            Add("Fulcrum.Delta.ClassSpeedDeltaRawKmh", 0.0f, "Unfiltered inferred speed difference to class reference");
            Add("Fulcrum.Delta.ClassSpeedDeltaFilteredKmh", 0.0f, "Low-pass filtered inferred speed difference to class reference");
            Add("Fulcrum.Delta.ClassSpeedDeltaKmh", 0, "Stable integer speed difference to class reference with deadband and hysteresis");
            Add("Fulcrum.Delta.ClassLiveMode", "Unavailable", "NativeSameClass, ClassTrace, ClassEstimate, LapGuard or Unavailable");
            Add("Fulcrum.Delta.ClassReferenceTime", 0.0f, "Reference elapsed time at player's current lap position");
            Add("Fulcrum.Delta.ClassTraceReady", false, "True after a complete same-class lap trace has been learned");
            Add("Fulcrum.Delta.ClassTraceDriver", string.Empty, "Driver used to shape the learned class trace");
            Add("Fulcrum.Delta.ClassTraceCarIndex", -1, "CarIdx used to shape the learned class trace");
            Add("Fulcrum.Delta.ClassTraceLapSeconds", 0.0f, "Observed lap time used to shape the learned class trace");
            Add("Fulcrum.Delta.ClassTraceLapText", "--:--.---", "Observed trace lap time, formatted");
            Add("Fulcrum.Delta.ClassTraceCoverage", 0.0f, "Fraction of track buckets observed in the learned trace");
            Add("Fulcrum.Delta.ClassTraceScale", 0.0f, "Scale from observed trace lap to official class best");
            Add("Fulcrum.Delta.SelectedReferenceMode", "ClassBest", "PersonalBest or ClassBest");
            Add("Fulcrum.Delta.SelectedReferenceLabel", "CLASS BEST", "Human-readable selected timing reference");
            Add("Fulcrum.Delta.SelectedBestSeconds", 0.0f, "Lap time used by Delta and Sectors");
            Add("Fulcrum.Delta.SelectedBestText", "--:--.---", "Formatted lap time used by Delta and Sectors");
            Add("Fulcrum.Delta.SelectedBestDriver", string.Empty, "Driver owning the selected reference");
            Add("Fulcrum.Delta.SelectedLiveValid", false, "True when selected live timing reference is valid");
            Add("Fulcrum.Delta.SelectedLiveSeconds", 0.0f, "Display delta to selected reference");
            Add("Fulcrum.Delta.SelectedRawSeconds", 0.0f, "Unsmoothed cumulative delta used by sector calculations");
            Add("Fulcrum.Delta.SelectedLiveText", "--.---", "Formatted delta to selected reference");
            Add("Fulcrum.Delta.SelectedLiveRate", 0.0f, "Rate of change of selected live delta");
            Add("Fulcrum.Delta.SelectedSpeedDeltaRawKmh", 0.0f, "Raw inferred speed delta to selected reference");
            Add("Fulcrum.Delta.SelectedSpeedDeltaFilteredKmh", 0.0f, "Filtered speed delta to selected reference");
            Add("Fulcrum.Delta.SelectedSpeedDeltaKmh", 0, "Stable integer speed delta to selected reference");
            Add("Fulcrum.Delta.SelectedLiveMode", "Unavailable", "NativePersonalBest or class-reference mode");
            Add("Fulcrum.Delta.SelectedReferenceTime", 0.0f, "Reference elapsed time at current track position");
            Add("Fulcrum.Delta.ClassTraceTrusted", false, "True when trace lap matched that driver's best lap");

            Add("Fulcrum.Sectors.ReferenceMode", "ClassBest", "Timing reference used by shared sector capture");
            Add("Fulcrum.Sectors.Count", 0, "Number of completed sectors retained, maximum three");
            Add("Fulcrum.Sectors.PopupVisible", false, "True during the sector pop-up hold window");
            Add("Fulcrum.Sectors.LastSector", 0, "Most recently completed sector number");
            Add("Fulcrum.Sectors.LastTimeSeconds", 0.0f, "Most recently completed sector elapsed time");
            Add("Fulcrum.Sectors.LastDeltaSeconds", 0.0f, "Most recently completed sector delta to selected reference");
            Add("Fulcrum.Sectors.LastValid", false, "True when the latest shared sector result is valid");

            for (int i = 1; i <= 3; i++)
            {
                Add("Fulcrum.Sectors.Slot" + i.ToString(CultureInfo.InvariantCulture) + ".Sector", 0, "Completed sector slot");
                Add("Fulcrum.Sectors.Slot" + i.ToString(CultureInfo.InvariantCulture) + ".TimeSeconds", 0.0f, "Completed sector time");
                Add("Fulcrum.Sectors.Slot" + i.ToString(CultureInfo.InvariantCulture) + ".DeltaSeconds", 0.0f, "Completed sector delta");
                Add("Fulcrum.Sectors.Slot" + i.ToString(CultureInfo.InvariantCulture) + ".Valid", false, "Completed sector validity");
            }
        }

        private void UpdateScheduled()
        {
            if (!latestGameRunning || latestRawData == null || latestPlayerCarIndex < 0 || latestPlayerCarIndex >= Capacity)
            {
                PublishUnavailable("WAITING FOR IRACING");
                return;
            }

            object telemetry = Get(latestRawData, "CurrentTelemetry") ?? Get(latestRawData, "Telemetry");
            if (telemetry == null)
            {
                PublishUnavailable("WAITING FOR TELEMETRY");
                return;
            }

            int sessionNumber = ToInt(ReadTelemetryValue(telemetry, "SessionNum"), -1);
            double sessionTime = ToDouble(ReadTelemetryValue(telemetry, "SessionTime"), 0.0);
            if (!hasSessionNumber || sessionNumber != lastSessionNumber)
            {
                hasSessionNumber = true;
                lastSessionNumber = sessionNumber;
                activePlayerClassKey = string.Empty;
                ResetTraceLearning();
                ResetFilter();
                ResetSectorCapture(true);
            }

            ReadClassIds(telemetry);

            ParticipantSnapshot player = participants[latestPlayerCarIndex];
            if (player == null || !player.IsValid)
            {
                PublishUnavailable("WAITING FOR PLAYER");
                return;
            }

            DriverIdentity playerIdentity;
            bool hasPlayerIdentity = database.TryGet(latestPlayerCarIndex, out playerIdentity) && playerIdentity != null;
            string playerClassName = hasPlayerIdentity ? (playerIdentity.ClassName ?? string.Empty) : string.Empty;
            int playerClassId = classIdByCarIndex[latestPlayerCarIndex];
            string playerClassKey = BuildClassKey(playerClassId, playerClassName);

            if (string.IsNullOrWhiteSpace(playerClassKey))
            {
                PublishUnavailable("WAITING FOR CLASS INFO");
                Set("Fulcrum.Delta.PlayerClass", playerClassName);
                Set("Fulcrum.Delta.PlayerClassId", playerClassId);
                return;
            }

            if (!string.Equals(activePlayerClassKey, playerClassKey, StringComparison.OrdinalIgnoreCase))
            {
                activePlayerClassKey = playerClassKey;
                ResetTraceLearning();
                ResetFilter();
                ResetSectorCapture(true);
                ReadClassIds(telemetry);
            }

            int classCarCount = 0;
            int classBestCarIndex = -1;
            string classBestDriver = string.Empty;
            float classBestLap = 0.0f;
            int overallBestCarIndex = -1;
            string overallBestDriver = string.Empty;
            float overallBestLap = 0.0f;

            for (int carIndex = 0; carIndex < Capacity; carIndex++)
            {
                ParticipantSnapshot p = participants[carIndex];
                DriverIdentity identity;
                bool hasIdentity = database.TryGet(carIndex, out identity) && identity != null;
                string className = hasIdentity ? (identity.ClassName ?? string.Empty) : string.Empty;
                string classKey = BuildClassKey(classIdByCarIndex[carIndex], className);
                bool sameClass = string.Equals(classKey, playerClassKey, StringComparison.OrdinalIgnoreCase);
                if (sameClass) classCarCount++;

                if (p == null || !p.IsValid) continue;
                float best = ValidLap(p.BestLapTime);
                if (best <= 0.0f) continue;

                if (overallBestLap <= 0.0f || best < overallBestLap)
                {
                    overallBestLap = best;
                    overallBestCarIndex = carIndex;
                    overallBestDriver = hasIdentity ? (identity.DriverName ?? string.Empty) : ("CarIdx " + carIndex.ToString(CultureInfo.InvariantCulture));
                }

                if (sameClass && (classBestLap <= 0.0f || best < classBestLap))
                {
                    classBestLap = best;
                    classBestCarIndex = carIndex;
                    classBestDriver = hasIdentity ? (identity.DriverName ?? string.Empty) : ("CarIdx " + carIndex.ToString(CultureInfo.InvariantCulture));
                }
            }

            LearnClassTraces(playerClassKey, sessionTime);

            bool sameAsOverall = classBestCarIndex >= 0 && classBestCarIndex == overallBestCarIndex &&
                classBestLap > 0.0f && overallBestLap > 0.0f && Math.Abs(classBestLap - overallBestLap) <= LapTolerance;

            float currentLapTime = ToFloat(ReadTelemetryValue(telemetry, "LapCurrentLapTime"), 0.0f);
            float playerLapDist = Clamp(player.LapDistancePercent, 0.0f, 1.0f);
            float personalBestLap = ValidLap(player.BestLapTime);
            string personalBestDriver = hasPlayerIdentity
                ? (playerIdentity.DriverName ?? string.Empty)
                : string.Empty;

            float nativePersonalDelta;
            bool nativePersonalOk =
                TryReadDelta(
                    telemetry,
                    "LapDeltaToBestLap",
                    "LapDeltaToBestLap_OK",
                    out nativePersonalDelta);

            float nativePersonalRate =
                ToFloat(ReadTelemetryValue(telemetry, "LapDeltaToBestLap_DD"), float.NaN);

            bool liveValid = false;
            float rawLiveDelta = 0.0f;
            float referenceTime = 0.0f;
            float directRate = float.NaN;
            string liveMode = "Unavailable";

            if (classBestLap > 0.0f && currentLapTime >= 0.0f)
            {
                float nativeDelta;
                bool nativeOk = TryReadDelta(telemetry, "LapDeltaToSessionBestLap", "LapDeltaToSessionBestLap_OK", out nativeDelta);

                // IMPORTANT: LapDeltaToSessionBestLap_OK is used here as a lap-context
                // validity gate, even when the absolute session best belongs to another
                // class.  When iRacing marks its native delta invalid (tow/reset, invalid
                // out-lap context, before a clean line crossing, etc.), LapCurrentLapTime
                // can still keep counting.  Comparing that stale/partial timer against a
                // learned class trace produced absurd +10s...+80s deltas.
                //
                // Therefore ClassTrace/ClassEstimate are only allowed while iRacing says
                // a live delta context is valid.  The VALUE of the native delta is ignored
                // in multiclass; only its OK flag is used as the guard.
                if (!nativeOk)
                {
                    liveMode = "LapGuard";
                    liveValid = false;
                    referenceTime = 0.0f;
                }
                else if (classBestCarIndex == latestPlayerCarIndex && nativePersonalOk)
                {
                    liveValid = true;
                    rawLiveDelta = nativePersonalDelta;
                    liveMode = "NativePlayerClassBest";
                    directRate = nativePersonalRate;
                    referenceTime = Math.Max(0.0f, currentLapTime - nativePersonalDelta);
                }
                else
                {
                    float traceScale = referenceTraceLap > 0.0f ? classBestLap / referenceTraceLap : 0.0f;
                    bool traceScaleReasonable = referenceTraceLap > 0.0f && Math.Abs(traceScale - 1.0f) <= MaximumTraceScaleError;
                    if (referenceTraceLap > 0.0f && referenceTraceCoverage >= MinimumTraceCoverage && traceScaleReasonable)
                    {
                        float traceTime = InterpolateTrace(referenceTrace, playerLapDist);
                        if (IsFinite(traceTime) && traceTime >= 0.0f)
                        {
                            referenceTime = traceTime * traceScale;
                            rawLiveDelta = currentLapTime - referenceTime;
                            liveValid = IsFinite(rawLiveDelta);
                            liveMode = liveValid ? "ClassTrace" : "Unavailable";
                        }
                    }

                    if (!liveValid)
                    {
                        float classEstimate = player.CarClassEstimatedLapTime;
                        float estimatedAtPosition = player.EstimatedTime;
                        if (classEstimate > 5.0f && IsFinite(estimatedAtPosition) && estimatedAtPosition >= 0.0f)
                        {
                            float scale = classBestLap / classEstimate;
                            referenceTime = estimatedAtPosition * scale;
                            rawLiveDelta = currentLapTime - referenceTime;
                            liveValid = IsFinite(rawLiveDelta) && Math.Abs(rawLiveDelta) < 120.0f;
                            liveMode = liveValid ? "ClassEstimate" : "Unavailable";
                        }
                    }
                }
            }

            float liveDelta = 0.0f;
            float liveRate = 0.0f;
            if (liveValid)
            {
                DateTime now = DateTime.UtcNow;
                bool lapWrapped = previousPlayerLapDist >= 0.0f && playerLapDist + 0.45f < previousPlayerLapDist;
                if (!filterInitialized || lapWrapped || Math.Abs(rawLiveDelta - filteredDelta) > 8.0f)
                {
                    filterInitialized = true;
                    filteredDelta = rawLiveDelta;
                    previousFilteredDelta = rawLiveDelta;
                    previousFilterUtc = now;
                }
                else
                {
                    previousFilteredDelta = filteredDelta;
                    filteredDelta += DeltaSmoothing * (rawLiveDelta - filteredDelta);
                }

                if (IsFinite(directRate))
                {
                    liveRate = directRate;
                }
                else
                {
                    double elapsed = previousFilterUtc == DateTime.MinValue ? 0.0 : (now - previousFilterUtc).TotalSeconds;
                    if (elapsed > 0.005)
                    {
                        liveRate = (float)((filteredDelta - previousFilteredDelta) / elapsed);
                        if (!IsFinite(liveRate) || Math.Abs(liveRate) > 2.0f) liveRate = 0.0f;
                    }
                }

                previousFilterUtc = now;
                liveDelta = filteredDelta;
            }
            else
            {
                ResetFilter();
            }
            previousPlayerLapDist = playerLapDist;

            float speedDeltaRawKmh = 0.0f;
            float speedDeltaFilteredKmh = 0.0f;
            int speedDeltaDisplayKmh = 0;
            if (liveValid)
            {
                float playerSpeedMps = ToFloat(ReadTelemetryValue(telemetry, "Speed"), float.NaN);
                float playerSpeedKmh = IsFinite(playerSpeedMps) ? playerSpeedMps * 3.6f : float.NaN;
                DateTime speedNow = DateTime.UtcNow;
                speedDeltaDisplayKmh = UpdateSpeedDelta(
                    liveRate,
                    playerSpeedKmh,
                    speedNow,
                    out speedDeltaRawKmh,
                    out speedDeltaFilteredKmh);
            }
            else
            {
                ResetSpeedDeltaFilter();
            }

            bool useClassBest =
                timingReferenceSettings == null ||
                timingReferenceSettings.ReferenceMode == TimingReferenceMode.ClassBest;

            bool selectedValid;
            float selectedBestLap;
            string selectedBestDriver;
            float selectedLiveDelta;
            float selectedRawDelta;
            float selectedLiveRate;
            string selectedLiveMode;
            float selectedReferenceTime;
            float selectedSpeedRawKmh = 0.0f;
            float selectedSpeedFilteredKmh = 0.0f;
            int selectedSpeedDisplayKmh = 0;

            if (useClassBest)
            {
                selectedValid = liveValid;
                selectedBestLap = classBestLap;
                selectedBestDriver = classBestDriver;
                selectedLiveDelta = liveDelta;
                selectedRawDelta = rawLiveDelta;
                selectedLiveRate = liveRate;
                selectedLiveMode = liveMode;
                selectedReferenceTime = referenceTime;
                selectedSpeedRawKmh = speedDeltaRawKmh;
                selectedSpeedFilteredKmh = speedDeltaFilteredKmh;
                selectedSpeedDisplayKmh = speedDeltaDisplayKmh;
                ResetPersonalSpeedDeltaFilter();
            }
            else
            {
                selectedBestLap = personalBestLap;
                selectedBestDriver = personalBestDriver;
                selectedValid =
                    personalBestLap > 0.0f &&
                    nativePersonalOk &&
                    currentLapTime >= 0.0f;

                selectedLiveDelta = selectedValid ? nativePersonalDelta : 0.0f;
                selectedRawDelta = selectedLiveDelta;
                selectedLiveRate =
                    selectedValid && IsFinite(nativePersonalRate)
                        ? nativePersonalRate
                        : 0.0f;
                selectedLiveMode = selectedValid ? "NativePersonalBest" : "LapGuard";
                selectedReferenceTime =
                    selectedValid
                        ? Math.Max(0.0f, currentLapTime - nativePersonalDelta)
                        : 0.0f;

                if (selectedValid)
                {
                    float playerSpeedMps =
                        ToFloat(ReadTelemetryValue(telemetry, "Speed"), float.NaN);
                    float playerSpeedKmh =
                        IsFinite(playerSpeedMps) ? playerSpeedMps * 3.6f : float.NaN;

                    selectedSpeedDisplayKmh =
                        UpdatePersonalSpeedDelta(
                            selectedLiveRate,
                            playerSpeedKmh,
                            DateTime.UtcNow,
                            out selectedSpeedRawKmh,
                            out selectedSpeedFilteredKmh);
                }
                else
                {
                    ResetPersonalSpeedDeltaFilter();
                }
            }

            UpdateSharedSectorCapture(
                latestRawData,
                telemetry,
                sessionTime,
                playerLapDist,
                selectedRawDelta,
                selectedValid,
                ReferenceModeName());

            string displayClass = FriendlyClassName(playerClassName, playerClassId);
            float traceScalePublished = referenceTraceLap > 0.0f && classBestLap > 0.0f ? classBestLap / referenceTraceLap : 0.0f;

            Set("Fulcrum.Delta.ClassReady", classBestLap > 0.0f);
            Set("Fulcrum.Delta.ClassStatus", classBestLap > 0.0f ? (liveValid ? "ACTIVE" : "REFERENCE READY - LIVE MAP WAITING") : "WAITING FOR CLASS BEST");
            Set("Fulcrum.Delta.PlayerClass", displayClass);
            Set("Fulcrum.Delta.PlayerClassId", playerClassId);
            Set("Fulcrum.Delta.ClassCarCount", classCarCount);
            Set("Fulcrum.Delta.ClassBestCarIndex", classBestCarIndex);
            Set("Fulcrum.Delta.ClassBestDriver", classBestDriver);
            Set("Fulcrum.Delta.ClassBestSeconds", classBestLap);
            Set("Fulcrum.Delta.ClassBestText", FormatLap(classBestLap));
            Set("Fulcrum.Delta.OverallBestSeconds", overallBestLap);
            Set("Fulcrum.Delta.OverallBestDriver", overallBestDriver);
            Set("Fulcrum.Delta.ClassBestIsOverallBest", sameAsOverall);
            Set("Fulcrum.Delta.ClassLiveValid", liveValid);
            Set("Fulcrum.Delta.ClassLiveSeconds", liveValid ? liveDelta : 0.0f);
            Set("Fulcrum.Delta.ClassLiveText", liveValid ? FormatDelta(liveDelta) : "--.---");
            Set("Fulcrum.Delta.ClassLiveRate", liveValid ? liveRate : 0.0f);
            Set("Fulcrum.Delta.ClassSpeedDeltaRawKmh", liveValid ? speedDeltaRawKmh : 0.0f);
            Set("Fulcrum.Delta.ClassSpeedDeltaFilteredKmh", liveValid ? speedDeltaFilteredKmh : 0.0f);
            Set("Fulcrum.Delta.ClassSpeedDeltaKmh", liveValid ? speedDeltaDisplayKmh : 0);
            Set("Fulcrum.Delta.ClassLiveMode", liveMode);
            Set("Fulcrum.Delta.ClassReferenceTime", liveValid ? referenceTime : 0.0f);

            Set("Fulcrum.Delta.SelectedReferenceMode", ReferenceModeName());
            Set("Fulcrum.Delta.SelectedReferenceLabel", ReferenceLabel());
            Set("Fulcrum.Delta.SelectedBestSeconds", selectedBestLap);
            Set("Fulcrum.Delta.SelectedBestText", FormatLap(selectedBestLap));
            Set("Fulcrum.Delta.SelectedBestDriver", selectedBestDriver ?? string.Empty);
            Set("Fulcrum.Delta.SelectedLiveValid", selectedValid);
            Set("Fulcrum.Delta.SelectedLiveSeconds", selectedValid ? selectedLiveDelta : 0.0f);
            Set("Fulcrum.Delta.SelectedRawSeconds", selectedValid ? selectedRawDelta : 0.0f);
            Set("Fulcrum.Delta.SelectedLiveText", selectedValid ? FormatDelta(selectedLiveDelta) : "--.---");
            Set("Fulcrum.Delta.SelectedLiveRate", selectedValid ? selectedLiveRate : 0.0f);
            Set("Fulcrum.Delta.SelectedSpeedDeltaRawKmh", selectedValid ? selectedSpeedRawKmh : 0.0f);
            Set("Fulcrum.Delta.SelectedSpeedDeltaFilteredKmh", selectedValid ? selectedSpeedFilteredKmh : 0.0f);
            Set("Fulcrum.Delta.SelectedSpeedDeltaKmh", selectedValid ? selectedSpeedDisplayKmh : 0);
            Set("Fulcrum.Delta.SelectedLiveMode", selectedLiveMode);
            Set("Fulcrum.Delta.SelectedReferenceTime", selectedValid ? selectedReferenceTime : 0.0f);

            Set("Fulcrum.Delta.ClassTraceReady", referenceTraceLap > 0.0f && referenceTraceCoverage >= MinimumTraceCoverage);
            Set("Fulcrum.Delta.ClassTraceDriver", referenceTraceDriver ?? string.Empty);
            Set("Fulcrum.Delta.ClassTraceCarIndex", referenceTraceCarIndex);
            Set("Fulcrum.Delta.ClassTraceLapSeconds", referenceTraceLap);
            Set("Fulcrum.Delta.ClassTraceLapText", FormatLap(referenceTraceLap));
            Set("Fulcrum.Delta.ClassTraceCoverage", referenceTraceCoverage);
            Set("Fulcrum.Delta.ClassTraceScale", traceScalePublished);
            Set("Fulcrum.Delta.ClassTraceTrusted", referenceTraceTrusted);
        }

        private void ResetSpeedDeltaFilter()
        {
            speedDeltaFilterInitialized = false;
            filteredSpeedDeltaKmh = 0.0f;
            displayedSpeedDeltaKmh = 0;
            previousSpeedFilterUtc = DateTime.MinValue;
        }

        private int UpdateSpeedDelta(
            float liveRate,
            float playerSpeedKmh,
            DateTime now,
            out float rawSpeedDeltaKmh,
            out float smoothedSpeedDeltaKmh)
        {
            rawSpeedDeltaKmh = 0.0f;
            smoothedSpeedDeltaKmh = 0.0f;

            if (!IsFinite(liveRate) || !IsFinite(playerSpeedKmh) || playerSpeedKmh < 0.0f)
            {
                ResetSpeedDeltaFilter();
                return 0;
            }

            float denominator = 1.0f - liveRate;
            if (!IsFinite(denominator) || Math.Abs(denominator) < 0.05f)
            {
                ResetSpeedDeltaFilter();
                return 0;
            }

            float referenceSpeedKmh = playerSpeedKmh / denominator;
            rawSpeedDeltaKmh = playerSpeedKmh - referenceSpeedKmh;
            if (!IsFinite(rawSpeedDeltaKmh) || Math.Abs(rawSpeedDeltaKmh) > 150.0f)
            {
                ResetSpeedDeltaFilter();
                rawSpeedDeltaKmh = 0.0f;
                return 0;
            }

            if (!speedDeltaFilterInitialized)
            {
                speedDeltaFilterInitialized = true;
                filteredSpeedDeltaKmh = rawSpeedDeltaKmh;
                previousSpeedFilterUtc = now;
            }
            else
            {
                double elapsed = previousSpeedFilterUtc == DateTime.MinValue
                    ? 0.05
                    : (now - previousSpeedFilterUtc).TotalSeconds;
                if (elapsed < 0.005) elapsed = 0.005;
                if (elapsed > 0.25) elapsed = 0.25;

                float error = Math.Abs(rawSpeedDeltaKmh - filteredSpeedDeltaKmh);
                float tau = error >= 12.0f ? SpeedDeltaFastTauSeconds : SpeedDeltaTauSeconds;
                float alpha = 1.0f - (float)Math.Exp(-elapsed / tau);
                filteredSpeedDeltaKmh += alpha * (rawSpeedDeltaKmh - filteredSpeedDeltaKmh);
                previousSpeedFilterUtc = now;
            }

            smoothedSpeedDeltaKmh = filteredSpeedDeltaKmh;

            // Deadband around zero so tiny telemetry noise does not flash +/-1 km/h.
            if (Math.Abs(filteredSpeedDeltaKmh) <= SpeedDeltaDeadbandKmh)
            {
                displayedSpeedDeltaKmh = 0;
                return 0;
            }

            int desired = (int)Math.Round(filteredSpeedDeltaKmh, MidpointRounding.AwayFromZero);

            // Leaving zero requires a little more evidence than entering it.
            if (displayedSpeedDeltaKmh == 0)
            {
                if (Math.Abs(filteredSpeedDeltaKmh) >= SpeedDeltaExitDeadbandKmh)
                    displayedSpeedDeltaKmh = desired;
                return displayedSpeedDeltaKmh;
            }

            // Crossing sign also requires clearing the exit threshold.
            if (Math.Sign(desired) != Math.Sign(displayedSpeedDeltaKmh))
            {
                if (Math.Abs(filteredSpeedDeltaKmh) >= SpeedDeltaExitDeadbandKmh)
                    displayedSpeedDeltaKmh = desired;
                else
                    displayedSpeedDeltaKmh = 0;
                return displayedSpeedDeltaKmh;
            }

            // Hysteresis prevents -14/-15/-14/-15 flicker. With the chosen threshold,
            // the visible number tends to move in calmer ~2 km/h steps while the
            // underlying filtered value remains continuous.
            if (Math.Abs(filteredSpeedDeltaKmh - displayedSpeedDeltaKmh) >= SpeedDeltaHysteresisKmh)
                displayedSpeedDeltaKmh = desired;

            return displayedSpeedDeltaKmh;
        }

        private void ResetSectorCapture(bool clearHistory)
        {
            sectorStartCount = 0;
            sectorCaptureInitialized = false;
            sectorPrev = 0;
            sectorStartSessionTime = 0.0;
            sectorStartDelta = float.NaN;
            sectorPrevLap = 0;
            sectorPrevDist = float.NaN;
            sectorPrevDelta = float.NaN;
            sectorPrevDeltaOk = false;
            sectorReferenceMode = string.Empty;
            sectorFinishDelta = float.NaN;
            sectorFinishOk = false;
            sectorPopupUntilUtc = DateTime.MinValue;

            if (clearHistory)
            {
                sectorHistoryCount = 0;
                sectorLastSector = 0;
                sectorLastTime = 0.0f;
                sectorLastDelta = 0.0f;
                sectorLastValid = false;

                for (int i = 0; i < 3; i++)
                {
                    sectorHistorySector[i] = 0;
                    sectorHistoryTime[i] = 0.0f;
                    sectorHistoryDelta[i] = 0.0f;
                    sectorHistoryValid[i] = false;
                }
            }

            PublishSharedSectorState();
        }

        private void ArmSectorCapture(
            int sector,
            double sessionTime,
            int lap,
            float dist,
            float delta,
            bool ok,
            string referenceMode)
        {
            sectorCaptureInitialized = true;
            sectorPrev = sector;
            sectorStartSessionTime = sessionTime;
            sectorStartDelta = IsFinite(delta) ? delta : float.NaN;
            sectorPrevLap = lap;
            sectorPrevDist = dist;
            sectorPrevDelta = IsFinite(delta) ? delta : float.NaN;
            sectorPrevDeltaOk = ok;
            sectorReferenceMode = referenceMode ?? string.Empty;
        }

        private void ClearSectorHistoryAndArm(
            int sector,
            double sessionTime,
            int lap,
            float dist,
            float delta,
            bool ok,
            string referenceMode)
        {
            sectorHistoryCount = 0;
            sectorLastSector = 0;
            sectorLastTime = 0.0f;
            sectorLastDelta = 0.0f;
            sectorLastValid = false;
            sectorPopupUntilUtc = DateTime.MinValue;
            sectorFinishDelta = float.NaN;
            sectorFinishOk = false;

            for (int i = 0; i < 3; i++)
            {
                sectorHistorySector[i] = 0;
                sectorHistoryTime[i] = 0.0f;
                sectorHistoryDelta[i] = 0.0f;
                sectorHistoryValid[i] = false;
            }

            ArmSectorCapture(sector, sessionTime, lap, dist, delta, ok, referenceMode);
            PublishSharedSectorState();
        }

        private void UpdateSharedSectorCapture(
            object rawData,
            object telemetry,
            double sessionTime,
            float lapDistPct,
            float selectedRawDelta,
            bool selectedValid,
            string referenceMode)
        {
            EnsureSectorStarts(rawData);

            int currentSector = ResolveSectorNumber(lapDistPct);
            int lap = ToInt(ReadTelemetryValue(telemetry, "Lap"), 0);
            bool onPitRoad = ToBool(ReadTelemetryValue(telemetry, "OnPitRoad"), false);

            if (currentSector <= 0 || sectorStartCount <= 0 || double.IsNaN(sessionTime) || double.IsInfinity(sessionTime))
            {
                sectorCaptureInitialized = false;
                sectorPopupUntilUtc = DateTime.MinValue;
                PublishSharedSectorState();
                return;
            }

            string mode = referenceMode ?? string.Empty;

            if (!sectorCaptureInitialized)
            {
                ArmSectorCapture(
                    currentSector,
                    sessionTime,
                    lap,
                    lapDistPct,
                    selectedRawDelta,
                    selectedValid,
                    mode);

                PublishSharedSectorState();
                return;
            }

            if (!string.Equals(sectorReferenceMode, mode, StringComparison.Ordinal))
            {
                ClearSectorHistoryAndArm(
                    currentSector,
                    sessionTime,
                    lap,
                    lapDistPct,
                    selectedRawDelta,
                    selectedValid,
                    mode);
                return;
            }

            float prevDist = sectorPrevDist;
            int prevLap = sectorPrevLap;
            float prevDelta = sectorPrevDelta;
            bool prevDeltaOk = sectorPrevDeltaOk;

            bool lapAdvanced = lap == prevLap + 1;
            bool sectorWrapped = sectorPrev > currentSector;
            bool lapDistWrapped =
                IsFinite(lapDistPct) &&
                IsFinite(prevDist) &&
                prevDist > 0.80f &&
                lapDistPct < 0.20f;

            bool normalLapWrap =
                lapDistWrapped &&
                (lapAdvanced || sectorWrapped);

            // At start/finish the selected cumulative delta can reset before the
            // next scheduler tick. Preserve the final value from the preceding tick.
            if (normalLapWrap && IsFinite(prevDelta))
            {
                sectorFinishDelta = prevDelta;
                sectorFinishOk = prevDeltaOk;
            }

            bool lapJump = Math.Abs(lap - prevLap) > 1;
            bool backwardJump =
                IsFinite(lapDistPct) &&
                IsFinite(prevDist) &&
                lapDistPct < prevDist - 0.20f &&
                !normalLapWrap;

            bool forwardJump =
                IsFinite(lapDistPct) &&
                IsFinite(prevDist) &&
                lapDistPct > prevDist + 0.45f;

            if (onPitRoad || lapJump || backwardJump || forwardJump)
            {
                ClearSectorHistoryAndArm(
                    currentSector,
                    sessionTime,
                    lap,
                    lapDistPct,
                    selectedRawDelta,
                    selectedValid,
                    mode);
                return;
            }

            if (currentSector != sectorPrev)
            {
                float elapsed =
                    (float)(sessionTime - sectorStartSessionTime);

                bool closingLap =
                    sectorPrev > currentSector ||
                    sectorWrapped ||
                    normalLapWrap;

                float endDelta = selectedRawDelta;
                bool endOk = selectedValid;

                if (closingLap && IsFinite(sectorFinishDelta))
                {
                    endDelta = sectorFinishDelta;
                    endOk = sectorFinishOk;
                }

                float sectorDelta =
                    IsFinite(endDelta) && IsFinite(sectorStartDelta)
                        ? endDelta - sectorStartDelta
                        : float.NaN;

                bool plausible =
                    elapsed > 0.25f &&
                    elapsed < 300.0f;

                if (plausible && endOk && IsFinite(sectorDelta))
                {
                    PushSharedSector(
                        sectorPrev,
                        elapsed,
                        sectorDelta);
                }

                ArmSectorCapture(
                    currentSector,
                    sessionTime,
                    lap,
                    lapDistPct,
                    selectedRawDelta,
                    selectedValid,
                    mode);

                sectorFinishDelta = float.NaN;
                sectorFinishOk = false;
            }
            else
            {
                sectorPrevLap = lap;
                sectorPrevDist = lapDistPct;
                sectorPrevDelta =
                    IsFinite(selectedRawDelta)
                        ? selectedRawDelta
                        : sectorPrevDelta;
                sectorPrevDeltaOk = selectedValid;
            }

            PublishSharedSectorState();
        }

        private void PushSharedSector(
            int sector,
            float time,
            float delta)
        {
            if (sectorHistoryCount < 3)
            {
                int index = sectorHistoryCount;
                sectorHistorySector[index] = sector;
                sectorHistoryTime[index] = time;
                sectorHistoryDelta[index] = delta;
                sectorHistoryValid[index] = true;
                sectorHistoryCount++;
            }
            else
            {
                sectorHistorySector[0] = sectorHistorySector[1];
                sectorHistoryTime[0] = sectorHistoryTime[1];
                sectorHistoryDelta[0] = sectorHistoryDelta[1];
                sectorHistoryValid[0] = sectorHistoryValid[1];

                sectorHistorySector[1] = sectorHistorySector[2];
                sectorHistoryTime[1] = sectorHistoryTime[2];
                sectorHistoryDelta[1] = sectorHistoryDelta[2];
                sectorHistoryValid[1] = sectorHistoryValid[2];

                sectorHistorySector[2] = sector;
                sectorHistoryTime[2] = time;
                sectorHistoryDelta[2] = delta;
                sectorHistoryValid[2] = true;
            }

            sectorLastSector = sector;
            sectorLastTime = time;
            sectorLastDelta = delta;
            sectorLastValid = true;
            sectorPopupUntilUtc = DateTime.UtcNow.AddMilliseconds(3200.0);
        }

        private void EnsureSectorStarts(object rawData)
        {
            if (sectorStartCount > 0) return;

            object sessionData =
                Get(rawData, "SessionData") ??
                Get(rawData, "AllSessionData");

            object splitInfo = Get(sessionData, "SplitTimeInfo");
            object sectorsObject = Get(splitInfo, "Sectors");
            IEnumerable sectors = sectorsObject as IEnumerable;
            if (sectors == null) return;

            List<float> starts = new List<float>();

            foreach (object sector in sectors)
            {
                if (sector == null) continue;

                float pct =
                    ToFloat(
                        Get(sector, "SectorStartPct"),
                        float.NaN);

                if (!IsFinite(pct) || pct < 0.0f || pct >= 1.0f)
                {
                    continue;
                }

                bool duplicate = false;
                for (int i = 0; i < starts.Count; i++)
                {
                    if (Math.Abs(starts[i] - pct) < 0.0001f)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                {
                    starts.Add(pct);
                }

                if (starts.Count >= sectorStarts.Length)
                {
                    break;
                }
            }

            if (starts.Count == 0) return;

            bool hasZero = false;
            for (int i = 0; i < starts.Count; i++)
            {
                if (Math.Abs(starts[i]) < 0.0001f)
                {
                    hasZero = true;
                    break;
                }
            }

            if (!hasZero && starts.Count < sectorStarts.Length)
            {
                starts.Add(0.0f);
            }

            starts.Sort();

            sectorStartCount =
                Math.Min(
                    starts.Count,
                    sectorStarts.Length);

            for (int i = 0; i < sectorStartCount; i++)
            {
                sectorStarts[i] = starts[i];
            }
        }

        private int ResolveSectorNumber(float lapDistPct)
        {
            if (sectorStartCount <= 0 || !IsFinite(lapDistPct))
            {
                return -1;
            }

            int sector = 1;

            for (int i = 0; i < sectorStartCount; i++)
            {
                if (lapDistPct + 0.00001f >= sectorStarts[i])
                {
                    sector = i + 1;
                }
                else
                {
                    break;
                }
            }

            return sector;
        }

        private void PublishSharedSectorState()
        {
            bool popupVisible =
                sectorLastValid &&
                sectorPopupUntilUtc != DateTime.MinValue &&
                DateTime.UtcNow < sectorPopupUntilUtc;

            Set("Fulcrum.Sectors.ReferenceMode", ReferenceModeName());
            Set("Fulcrum.Sectors.Count", sectorHistoryCount);
            Set("Fulcrum.Sectors.PopupVisible", popupVisible);
            Set("Fulcrum.Sectors.LastSector", sectorLastSector);
            Set("Fulcrum.Sectors.LastTimeSeconds", sectorLastValid ? sectorLastTime : 0.0f);
            Set("Fulcrum.Sectors.LastDeltaSeconds", sectorLastValid ? sectorLastDelta : 0.0f);
            Set("Fulcrum.Sectors.LastValid", sectorLastValid);

            for (int i = 0; i < 3; i++)
            {
                int slot = i + 1;
                string prefix =
                    "Fulcrum.Sectors.Slot" +
                    slot.ToString(CultureInfo.InvariantCulture) +
                    ".";

                bool valid =
                    i < sectorHistoryCount &&
                    sectorHistoryValid[i];

                Set(prefix + "Sector", valid ? sectorHistorySector[i] : 0);
                Set(prefix + "TimeSeconds", valid ? sectorHistoryTime[i] : 0.0f);
                Set(prefix + "DeltaSeconds", valid ? sectorHistoryDelta[i] : 0.0f);
                Set(prefix + "Valid", valid);
            }
        }

        private void ResetPersonalSpeedDeltaFilter()
        {
            personalSpeedDeltaFilterInitialized = false;
            personalFilteredSpeedDeltaKmh = 0.0f;
            personalDisplayedSpeedDeltaKmh = 0;
            previousPersonalSpeedFilterUtc = DateTime.MinValue;
        }

        private int UpdatePersonalSpeedDelta(
            float liveRate,
            float playerSpeedKmh,
            DateTime now,
            out float rawSpeedDeltaKmh,
            out float smoothedSpeedDeltaKmh)
        {
            rawSpeedDeltaKmh = 0.0f;
            smoothedSpeedDeltaKmh = 0.0f;

            if (!IsFinite(liveRate) || !IsFinite(playerSpeedKmh) || playerSpeedKmh < 0.0f)
            {
                ResetPersonalSpeedDeltaFilter();
                return 0;
            }

            float denominator = 1.0f - liveRate;
            if (!IsFinite(denominator) || Math.Abs(denominator) < 0.05f)
            {
                ResetPersonalSpeedDeltaFilter();
                return 0;
            }

            float referenceSpeedKmh = playerSpeedKmh / denominator;
            rawSpeedDeltaKmh = playerSpeedKmh - referenceSpeedKmh;
            if (!IsFinite(rawSpeedDeltaKmh) || Math.Abs(rawSpeedDeltaKmh) > 150.0f)
            {
                ResetPersonalSpeedDeltaFilter();
                rawSpeedDeltaKmh = 0.0f;
                return 0;
            }

            if (!personalSpeedDeltaFilterInitialized)
            {
                personalSpeedDeltaFilterInitialized = true;
                personalFilteredSpeedDeltaKmh = rawSpeedDeltaKmh;
                previousPersonalSpeedFilterUtc = now;
            }
            else
            {
                double elapsed =
                    previousPersonalSpeedFilterUtc == DateTime.MinValue
                        ? 0.05
                        : (now - previousPersonalSpeedFilterUtc).TotalSeconds;

                if (elapsed < 0.005) elapsed = 0.005;
                if (elapsed > 0.25) elapsed = 0.25;

                float error =
                    Math.Abs(rawSpeedDeltaKmh - personalFilteredSpeedDeltaKmh);
                float tau =
                    error >= 12.0f
                        ? SpeedDeltaFastTauSeconds
                        : SpeedDeltaTauSeconds;

                float alpha =
                    1.0f - (float)Math.Exp(-elapsed / tau);

                personalFilteredSpeedDeltaKmh +=
                    alpha *
                    (rawSpeedDeltaKmh - personalFilteredSpeedDeltaKmh);

                previousPersonalSpeedFilterUtc = now;
            }

            smoothedSpeedDeltaKmh = personalFilteredSpeedDeltaKmh;

            if (Math.Abs(personalFilteredSpeedDeltaKmh) <= SpeedDeltaDeadbandKmh)
            {
                personalDisplayedSpeedDeltaKmh = 0;
                return 0;
            }

            int desired =
                (int)Math.Round(
                    personalFilteredSpeedDeltaKmh,
                    MidpointRounding.AwayFromZero);

            if (personalDisplayedSpeedDeltaKmh == 0)
            {
                if (Math.Abs(personalFilteredSpeedDeltaKmh) >= SpeedDeltaExitDeadbandKmh)
                {
                    personalDisplayedSpeedDeltaKmh = desired;
                }

                return personalDisplayedSpeedDeltaKmh;
            }

            if (Math.Sign(desired) != Math.Sign(personalDisplayedSpeedDeltaKmh))
            {
                if (Math.Abs(personalFilteredSpeedDeltaKmh) >= SpeedDeltaExitDeadbandKmh)
                {
                    personalDisplayedSpeedDeltaKmh = desired;
                }
                else
                {
                    personalDisplayedSpeedDeltaKmh = 0;
                }

                return personalDisplayedSpeedDeltaKmh;
            }

            if (Math.Abs(
                    personalFilteredSpeedDeltaKmh -
                    personalDisplayedSpeedDeltaKmh) >=
                SpeedDeltaHysteresisKmh)
            {
                personalDisplayedSpeedDeltaKmh = desired;
            }

            return personalDisplayedSpeedDeltaKmh;
        }

        private string ReferenceModeName()
        {
            return timingReferenceSettings != null &&
                   timingReferenceSettings.ReferenceMode == TimingReferenceMode.PersonalBest
                ? "PersonalBest"
                : "ClassBest";
        }

        private string ReferenceLabel()
        {
            return timingReferenceSettings != null &&
                   timingReferenceSettings.ReferenceMode == TimingReferenceMode.PersonalBest
                ? "MY BEST"
                : "CLASS BEST";
        }

        private void LearnClassTraces(string playerClassKey, double sessionTime)
        {
            for (int carIndex = 0; carIndex < Capacity; carIndex++)
            {
                ParticipantSnapshot p = participants[carIndex];
                LapTraceState state = traceStates[carIndex];

                if (p == null || !p.IsValid)
                {
                    state.ResetAll();
                    continue;
                }

                DriverIdentity identity;
                bool hasIdentity = database.TryGet(carIndex, out identity) && identity != null;
                string className = hasIdentity ? (identity.ClassName ?? string.Empty) : string.Empty;
                string classKey = BuildClassKey(classIdByCarIndex[carIndex], className);
                bool sameClass = string.Equals(classKey, playerClassKey, StringComparison.OrdinalIgnoreCase);

                if (state.LastLapCompleted == int.MinValue)
                {
                    state.LastLapCompleted = p.LapCompleted;
                    state.LastDistance = Clamp(p.LapDistancePercent, 0.0f, 1.0f);
                    continue;
                }

                bool completedLap = p.LapCompleted > state.LastLapCompleted;
                if (completedLap)
                {
                    if (sameClass) FinalizeTraceCandidate(carIndex, p, identity, state);
                    state.LastLapCompleted = p.LapCompleted;
                    if (sameClass) state.StartLap(sessionTime, Clamp(p.LapDistancePercent, 0.0f, 1.0f), p.IsOnPitRoad);
                    else state.ResetLapOnly();
                    continue;
                }

                state.LastLapCompleted = p.LapCompleted;
                if (!sameClass)
                {
                    state.ResetLapOnly();
                    continue;
                }

                float dist = Clamp(p.LapDistancePercent, 0.0f, 1.0f);
                if (!state.Tracking)
                {
                    // Wait for a real start/finish transition so we do not commit a
                    // partial lap just because the module joined mid-lap.
                    state.LastDistance = dist;
                    continue;
                }

                if (p.IsOnPitRoad) state.SawPit = true;
                state.Sample(sessionTime, dist);
            }
        }

        private void FinalizeTraceCandidate(int carIndex, ParticipantSnapshot p, DriverIdentity identity, LapTraceState state)
        {
            if (!state.Tracking || state.SawPit) return;
            float lap = ValidLap(p.LastLapTime);
            if (lap <= 0.0f) return;

            float coverage = state.Coverage;
            if (coverage < MinimumTraceCoverage) return;

            bool trusted = p.BestLapTime > 0.0f && Math.Abs(lap - p.BestLapTime) <= 0.060f;

            // Prefer a trusted PB trace.  Otherwise keep the quickest complete
            // same-class shape observed so far.
            bool replace = referenceTraceLap <= 0.0f ||
                (trusted && !referenceTraceTrusted) ||
                (trusted == referenceTraceTrusted && lap < referenceTraceLap - 0.001f);
            if (!replace) return;

            state.BuildCompletedTrace(lap, referenceTrace);
            referenceTraceLap = lap;
            referenceTraceCoverage = coverage;
            referenceTraceTrusted = trusted;
            referenceTraceCarIndex = carIndex;
            referenceTraceDriver = identity != null && !string.IsNullOrWhiteSpace(identity.DriverName)
                ? identity.DriverName
                : ("CarIdx " + carIndex.ToString(CultureInfo.InvariantCulture));
        }

        private void ReadClassIds(object telemetry)
        {
            object array = ReadTelemetryValue(telemetry, "CarIdxClass");
            for (int i = 0; i < Capacity; i++) classIdByCarIndex[i] = ReadIndexedInt(array, i, -1);
        }

        private static string BuildClassKey(int classId, string className)
        {
            if (classId > 0) return "ID:" + classId.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(className)) return "NAME:" + className.Trim();
            return string.Empty;
        }

        private static string FriendlyClassName(string className, int classId)
        {
            string text = (className ?? string.Empty).Trim();
            string upper = text.ToUpperInvariant();
            if (upper.Contains("GTP")) return "GTP";
            if (upper.Contains("LMP2") || upper.Contains("DALLARA P217") || classId == 2523) return "LMP2";
            if (upper.Contains("LMP3") || upper.Contains("LIGIER JS P320")) return "LMP3";
            if (upper.Contains("GT3") || upper.Contains("IMSA23")) return "GT3";
            if (upper.Contains("GT4")) return "GT4";
            if (upper.Contains("TCR")) return "TCR";
            return text;
        }

        private void PublishUnavailable(string status)
        {
            Set("Fulcrum.Delta.ClassReady", false);
            Set("Fulcrum.Delta.ClassStatus", status ?? "Unavailable");
            Set("Fulcrum.Delta.ClassLiveValid", false);
            Set("Fulcrum.Delta.ClassLiveSeconds", 0.0f);
            Set("Fulcrum.Delta.ClassLiveText", "--.---");
            Set("Fulcrum.Delta.ClassLiveRate", 0.0f);
            Set("Fulcrum.Delta.ClassSpeedDeltaRawKmh", 0.0f);
            Set("Fulcrum.Delta.ClassSpeedDeltaFilteredKmh", 0.0f);
            Set("Fulcrum.Delta.ClassSpeedDeltaKmh", 0);
            Set("Fulcrum.Delta.ClassLiveMode", "Unavailable");
            Set("Fulcrum.Delta.ClassReferenceTime", 0.0f);

            Set("Fulcrum.Delta.SelectedReferenceMode", ReferenceModeName());
            Set("Fulcrum.Delta.SelectedReferenceLabel", ReferenceLabel());
            Set("Fulcrum.Delta.SelectedBestSeconds", 0.0f);
            Set("Fulcrum.Delta.SelectedBestText", "--:--.---");
            Set("Fulcrum.Delta.SelectedBestDriver", string.Empty);
            Set("Fulcrum.Delta.SelectedLiveValid", false);
            Set("Fulcrum.Delta.SelectedLiveSeconds", 0.0f);
            Set("Fulcrum.Delta.SelectedRawSeconds", 0.0f);
            Set("Fulcrum.Delta.SelectedLiveText", "--.---");
            Set("Fulcrum.Delta.SelectedLiveRate", 0.0f);
            Set("Fulcrum.Delta.SelectedSpeedDeltaRawKmh", 0.0f);
            Set("Fulcrum.Delta.SelectedSpeedDeltaFilteredKmh", 0.0f);
            Set("Fulcrum.Delta.SelectedSpeedDeltaKmh", 0);
            Set("Fulcrum.Delta.SelectedLiveMode", "Unavailable");
            Set("Fulcrum.Delta.SelectedReferenceTime", 0.0f);
        }

        private void Add(string name, object value, string description)
        {
            manager.AddProperty(name, pluginType, value, description);
        }

        private void Set(string name, object value)
        {
            manager.SetPropertyValue(name, pluginType, value);
        }

        private static bool TryReadDelta(object telemetry, string valueKey, string okKey, out float delta)
        {
            delta = 0.0f;
            object value = ReadTelemetryValue(telemetry, valueKey);
            if (value == null) return false;
            object ok = ReadTelemetryValue(telemetry, okKey);
            if (ok != null && !ToBool(ok, false)) return false;
            delta = ToFloat(value, 0.0f);
            return IsFinite(delta) && Math.Abs(delta) < 120.0f;
        }

        private static object ReadTelemetryValue(object telemetry, string key)
        {
            if (telemetry == null) return null;

            IDictionary<string, object> generic = telemetry as IDictionary<string, object>;
            if (generic != null)
            {
                object value;
                if (generic.TryGetValue(key, out value)) return value;
            }

            IReadOnlyDictionary<string, object> readOnly = telemetry as IReadOnlyDictionary<string, object>;
            if (readOnly != null)
            {
                object value;
                if (readOnly.TryGetValue(key, out value)) return value;
            }

            IDictionary dictionary = telemetry as IDictionary;
            if (dictionary != null)
            {
                if (dictionary.Contains(key)) return dictionary[key];
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (string.Equals(Convert.ToString(entry.Key, CultureInfo.InvariantCulture), key, StringComparison.OrdinalIgnoreCase))
                        return entry.Value;
                }
            }

            IEnumerable enumerable = telemetry as IEnumerable;
            if (enumerable != null)
            {
                foreach (object item in enumerable)
                {
                    if (item == null) continue;
                    object itemKey = Get(item, "Key");
                    if (!string.Equals(Convert.ToString(itemKey, CultureInfo.InvariantCulture), key, StringComparison.OrdinalIgnoreCase)) continue;
                    return Get(item, "Value");
                }
            }

            return null;
        }

        private static int ReadIndexedInt(object array, int index, int fallback)
        {
            if (array == null || index < 0) return fallback;
            try
            {
                Array a = array as Array;
                if (a != null && index < a.Length) return ToInt(a.GetValue(index), fallback);
                IList list = array as IList;
                if (list != null && index < list.Count) return ToInt(list[index], fallback);
                IEnumerable enumerable = array as IEnumerable;
                if (enumerable != null)
                {
                    int i = 0;
                    foreach (object value in enumerable)
                    {
                        if (i == index) return ToInt(value, fallback);
                        i++;
                        if (i > index) break;
                    }
                }
            }
            catch { }
            return fallback;
        }

        private static object Get(object source, string name)
        {
            if (source == null) return null;
            IDictionary dict = source as IDictionary;
            if (dict != null)
            {
                if (dict.Contains(name)) return dict[name];
                foreach (DictionaryEntry entry in dict)
                {
                    if (string.Equals(Convert.ToString(entry.Key, CultureInfo.InvariantCulture), name, StringComparison.OrdinalIgnoreCase))
                        return entry.Value;
                }
            }
            PropertyInfo p = source.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (p != null && p.GetIndexParameters().Length == 0)
            {
                try { return p.GetValue(source, null); } catch { }
            }
            FieldInfo f = source.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (f != null)
            {
                try { return f.GetValue(source); } catch { }
            }
            return null;
        }

        private static float InterpolateTrace(float[] trace, float pct)
        {
            if (trace == null || trace.Length < 2) return float.NaN;
            pct = Clamp(pct, 0.0f, 1.0f);
            float position = pct * (trace.Length - 1);
            int a = (int)Math.Floor(position);
            int b = Math.Min(trace.Length - 1, a + 1);
            float ta = trace[a];
            float tb = trace[b];
            if (!IsFinite(ta) || !IsFinite(tb)) return float.NaN;
            float f = position - a;
            return ta + (tb - ta) * f;
        }

        private static float ValidLap(float value)
        {
            return IsFinite(value) && value > 1.0f && value < 86400.0f ? value : 0.0f;
        }

        private static string FormatLap(float seconds)
        {
            if (seconds <= 0.0f || !IsFinite(seconds)) return "--:--.---";
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            return ((int)t.TotalMinutes).ToString("00", CultureInfo.InvariantCulture) + ":" +
                t.Seconds.ToString("00", CultureInfo.InvariantCulture) + "." +
                t.Milliseconds.ToString("000", CultureInfo.InvariantCulture);
        }

        private static string FormatDelta(float seconds)
        {
            if (!IsFinite(seconds)) return "--.---";
            if (Math.Abs(seconds) < 0.0005f) return "0.000";
            return (seconds > 0.0f ? "+" : string.Empty) + seconds.ToString("0.000", CultureInfo.InvariantCulture);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static int ToInt(object value, int fallback)
        {
            try { return value == null ? fallback : Convert.ToInt32(value, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }

        private static float ToFloat(object value, float fallback)
        {
            try { return value == null ? fallback : Convert.ToSingle(value, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }

        private static double ToDouble(object value, double fallback)
        {
            try { return value == null ? fallback : Convert.ToDouble(value, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }

        private static bool ToBool(object value, bool fallback)
        {
            try { return value == null ? fallback : Convert.ToBoolean(value, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }

        private sealed class LapTraceState
        {
            private readonly float[] times;
            private int lastBucket;
            private float lastDistance;
            private float lastElapsed;

            public LapTraceState(int bucketCount)
            {
                times = new float[bucketCount];
                ResetAll();
            }

            public int LastLapCompleted { get; set; }
            public bool Tracking { get; private set; }
            public bool SawPit { get; set; }
            public double StartSessionTime { get; private set; }
            public float LastDistance
            {
                get { return lastDistance; }
                set { lastDistance = value; }
            }
            public float Coverage
            {
                get
                {
                    if (!Tracking || times.Length <= 1 || lastBucket < 0) return 0.0f;
                    return Clamp((float)lastBucket / (times.Length - 1), 0.0f, 1.0f);
                }
            }

            public void ResetAll()
            {
                LastLapCompleted = int.MinValue;
                ResetLapOnly();
            }

            public void ResetLapOnly()
            {
                Tracking = false;
                SawPit = false;
                StartSessionTime = 0.0;
                lastBucket = -1;
                lastDistance = 0.0f;
                lastElapsed = 0.0f;
                for (int i = 0; i < times.Length; i++) times[i] = float.NaN;
            }

            public void StartLap(double sessionTime, float distance, bool onPitRoad)
            {
                ResetLapOnly();
                Tracking = true;
                SawPit = onPitRoad;
                StartSessionTime = sessionTime;
                lastDistance = Clamp(distance, 0.0f, 1.0f);
                lastElapsed = 0.0f;
                lastBucket = 0;
                times[0] = 0.0f;
            }

            public void Sample(double sessionTime, float distance)
            {
                if (!Tracking) return;
                distance = Clamp(distance, 0.0f, 1.0f);
                float elapsed = (float)Math.Max(0.0, sessionTime - StartSessionTime);

                if (distance + 0.06f < lastDistance)
                {
                    // Teleport/replay discontinuity.  Wait for the next clean line crossing.
                    ResetLapOnly();
                    return;
                }

                int target = Math.Min(times.Length - 1, (int)Math.Floor(distance * (times.Length - 1)));
                if (target > lastBucket && distance > lastDistance + 0.000001f)
                {
                    for (int bucket = lastBucket + 1; bucket <= target; bucket++)
                    {
                        float bucketPct = (float)bucket / (times.Length - 1);
                        float f = (bucketPct - lastDistance) / (distance - lastDistance);
                        f = Clamp(f, 0.0f, 1.0f);
                        times[bucket] = lastElapsed + (elapsed - lastElapsed) * f;
                    }
                    lastBucket = target;
                }

                lastDistance = distance;
                lastElapsed = elapsed;
            }

            public void BuildCompletedTrace(float officialLapTime, float[] destination)
            {
                if (destination == null || destination.Length != times.Length) return;
                int knownEnd = Math.Max(0, Math.Min(lastBucket, times.Length - 2));
                float knownTime = knownEnd >= 0 && IsFinite(times[knownEnd]) ? times[knownEnd] : 0.0f;
                float knownPct = (float)knownEnd / (times.Length - 1);

                for (int i = 0; i <= knownEnd; i++)
                {
                    float t = times[i];
                    if (!IsFinite(t))
                    {
                        int prev = i - 1;
                        while (prev >= 0 && !IsFinite(times[prev])) prev--;
                        int next = i + 1;
                        while (next <= knownEnd && !IsFinite(times[next])) next++;
                        if (prev >= 0 && next <= knownEnd)
                        {
                            float f = (float)(i - prev) / (next - prev);
                            t = times[prev] + (times[next] - times[prev]) * f;
                        }
                        else if (prev >= 0) t = times[prev];
                        else t = 0.0f;
                    }
                    destination[i] = Math.Max(0.0f, t);
                }

                for (int i = knownEnd + 1; i < times.Length; i++)
                {
                    float pct = (float)i / (times.Length - 1);
                    float f = knownPct >= 0.9999f ? 1.0f : (pct - knownPct) / (1.0f - knownPct);
                    f = Clamp(f, 0.0f, 1.0f);
                    destination[i] = knownTime + (officialLapTime - knownTime) * f;
                }

                destination[0] = 0.0f;
                destination[destination.Length - 1] = officialLapTime;

                // Enforce monotonicity after interpolation/correction.
                for (int i = 1; i < destination.Length; i++)
                {
                    if (!IsFinite(destination[i]) || destination[i] < destination[i - 1])
                        destination[i] = destination[i - 1];
                }
            }
        }
    }
}
