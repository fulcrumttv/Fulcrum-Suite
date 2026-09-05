using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using Fulcrum.Core.Performance;
using Fulcrum.Core.Relative;
using Fulcrum.Core.Relative.Display;
using Fulcrum.Core.Relative.Gap;
using Fulcrum.Core.Session;
using Fulcrum.Plugin.Publishing;
using Fulcrum.Plugin.Settings;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Modules
{
    /// <summary>
    /// Coordinates participant telemetry, Relative calculations
    /// and construction of the Relative display model.
    ///
    /// SimHub property registration and publication are delegated
    /// to RelativePublisher.
    /// </summary>
    public sealed class RelativeModule
    {
        private readonly ParticipantTelemetryReader participantReader;
        private readonly ParticipantBuffer participantBuffer;

        private readonly RelativeCalculator relativeCalculator;
        private readonly ClassPositionResolver classPositions = new ClassPositionResolver();
        private string latestSessionType = string.Empty;
        private int previousRelativePlayer = -1;
        private readonly GapCalculator gapCalculator;
        private readonly GapFilter gapFilter;
        private readonly RelativeSnapshot relativeSnapshot;
        private readonly StintTracker stintTracker;

        private readonly SessionDatabase sessionDatabase;

        private readonly RelativeDisplayBuilder displayBuilder;
        private readonly RelativeDisplaySnapshot displaySnapshot;

        private readonly RelativePublisher relativePublisher;
        private readonly RelativeDisplayPublisher relativeDisplayPublisher;
        private readonly RelativeTablePublisher relativeTablePublisher;
        private readonly ScheduledTask updateTask;

        private object latestRawData;
        private bool latestGameRunning;
        private int latestPlayerCarIndex;

        // Gap caches are session-specific. iRacing can change track/session
        // without briefly reporting GameRunning=false, so detect context
        // changes from SessionNum and SessionTime as well.
        private bool hasGapContext;
        private int lastGapSessionNumber;
        private double lastGapSessionTime;
        private int lastGapPlayerCarIndex;

        private bool hasRelativeSessionState;
        private int lastRelativeSessionState;
        private int lastRelativeSessionNumber;
        private double lastRelativeSessionTime;
        private string lastRelativeSessionType = string.Empty;

        // Per-car P2P state cache. Needed for Super Formula where opponent
        // activation is best inferred from the decrementing count.
        private readonly int[] lastOvertakeCount = new int[SessionDatabase.Capacity];
        private readonly double[] lastOvertakeChangeTime = new double[SessionDatabase.Capacity];
        private readonly DateTime[] overtakeActiveUntilUtc = new DateTime[SessionDatabase.Capacity];
        private readonly bool[] hasOvertakeCount = new bool[SessionDatabase.Capacity];

        public RelativeModule(
            PluginManager pluginManager,
            Type pluginType,
            UpdateScheduler updateScheduler,
            SessionDatabase sessionDatabase,
            RelativeOverlaySettings relativeSettings)
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

            if (sessionDatabase == null)
            {
                throw new ArgumentNullException(
                    nameof(sessionDatabase));
            }

            if (relativeSettings == null)
            {
                throw new ArgumentNullException(
                    nameof(relativeSettings));
            }

            participantReader =
                new ParticipantTelemetryReader();

            participantBuffer =
                new ParticipantBuffer();

            relativeCalculator =
                new RelativeCalculator();

            gapCalculator =
                new GapCalculator();

            gapFilter =
                new GapFilter();

            relativeSnapshot =
                new RelativeSnapshot();

            stintTracker =
                new StintTracker();

            this.sessionDatabase =
                sessionDatabase;

            displayBuilder =
                new RelativeDisplayBuilder();

            displaySnapshot =
                new RelativeDisplaySnapshot();

            relativePublisher =
                new RelativePublisher(
                    pluginManager,
                    pluginType);

            relativeDisplayPublisher =
                new RelativeDisplayPublisher(
                    pluginManager,
                    pluginType);

            relativeTablePublisher =
                new RelativeTablePublisher(
                    pluginManager,
                    pluginType,
                    relativeSettings);

            latestRawData = null;
            latestGameRunning = false;
            latestPlayerCarIndex = -1;
            hasGapContext = false;
            lastGapSessionNumber = -1;
            lastGapSessionTime = -1.0;
            lastGapPlayerCarIndex = -1;
            hasRelativeSessionState = false;
            lastRelativeSessionState = -1;
            lastRelativeSessionNumber = -1;
            lastRelativeSessionTime = -1.0;
            lastRelativeSessionType = string.Empty;
            ResetOvertakeState();

            updateTask =
                updateScheduler.RegisterTask(
                    "Relative Module",
                    UpdateRates.RelativeHz,
                    UpdateScheduled,
                    false);

            Reset();
        }

        public RelativeDisplaySnapshot DisplaySnapshot
        {
            get { return displaySnapshot; }
        }

        public ParticipantBuffer ParticipantBuffer
        {
            get { return participantBuffer; }
        }

        public void SetFrameContext(
            object rawData,
            bool gameRunning,
            int playerCarIndex,
            string sessionType = "")
        {
            latestSessionType = sessionType ?? string.Empty;
            latestRawData = rawData;
            latestGameRunning = gameRunning;
            latestPlayerCarIndex = playerCarIndex;
        }

        public void Reset()
        {
            latestRawData = null;
            latestGameRunning = false;
            latestPlayerCarIndex = -1;
            hasGapContext = false;
            lastGapSessionNumber = -1;
            lastGapSessionTime = -1.0;
            lastGapPlayerCarIndex = -1;
            hasRelativeSessionState = false;
            lastRelativeSessionState = -1;
            lastRelativeSessionNumber = -1;
            lastRelativeSessionTime = -1.0;
            lastRelativeSessionType = string.Empty;
            ResetOvertakeState();

            participantReader.Reset(
                participantBuffer);

            relativeSnapshot.Reset();
            displaySnapshot.Reset();
            gapCalculator.Reset();
            gapFilter.Reset();
            stintTracker.Reset();
            relativeCalculator.Reset();
            classPositions.Reset();

            relativeTablePublisher.PublishContext(string.Empty, -1, false);

            PublishCurrentState();
        }

        private void UpdateScheduled()
        {
            if (!latestGameRunning ||
                latestRawData == null ||
                latestPlayerCarIndex < 0)
            {
                participantReader.Reset(
                    participantBuffer);

                relativeSnapshot.Reset();
                displaySnapshot.Reset();
                gapCalculator.Reset();
                gapFilter.Reset();

                PublishCurrentState();

                return;
            }

            ResetGapStateIfContextChanged();

            participantReader.Update(
                latestRawData,
                latestPlayerCarIndex,
                participantBuffer);

            EnrichParticipantsFromSessionInfo();
            UpdateOvertakeState();

            UpdateRelativeRaceContext();

            stintTracker.Update(
                participantBuffer);

            relativeCalculator.Calculate(
                participantBuffer,
                relativeSnapshot);

            gapCalculator.Calculate(
                participantBuffer,
                relativeSnapshot);

            double sessionTime = ReadDoubleValue(latestRawData, "SessionTime", -1.0);
            float playerSpeed = (float)ReadDoubleValue(latestRawData, "Speed", 0.0);

            gapFilter.Apply(
                participantBuffer,
                relativeSnapshot,
                sessionTime,
                playerSpeed);

            displayBuilder.Build(
                participantBuffer,
                relativeSnapshot,
                sessionDatabase,
                stintTracker,
                displaySnapshot);

            PublishCurrentState();
        }

        private void UpdateRelativeRaceContext()
        {
            int state = RelativeSessionReader.State(latestRawData);
            int number = RelativeSessionReader.Integer(RelativeSessionReader.Telemetry(latestRawData, "SessionNum"), -1);
            double time = RelativeSessionReader.Number(RelativeSessionReader.Telemetry(latestRawData, "SessionTime"), -1.0);
            string sessionType = RelativeSessionReader.SessionType(latestRawData, latestSessionType);
            bool isRace = sessionType.IndexOf("race", StringComparison.OrdinalIgnoreCase) >= 0;
            bool classModeChanged = hasRelativeSessionState && lastRelativeSessionType.Length > 0 &&
                (lastRelativeSessionType.IndexOf("race", StringComparison.OrdinalIgnoreCase) >= 0) != isRace;
            bool reset = !hasRelativeSessionState ||
                (number >= 0 && number != lastRelativeSessionNumber) ||
                (time >= 0.0 && lastRelativeSessionTime >= 0.0 && time + 2.0 < lastRelativeSessionTime) ||
                previousRelativePlayer != latestPlayerCarIndex ||
                (state >= 0 && state < 4 && lastRelativeSessionState >= 4);
            if (reset)
            {
                relativeCalculator.Reset();
                classPositions.Reset();
                stintTracker.Reset();
            }
            else if (classModeChanged)
            {
                // Only the +/- reference changes here. Do not disturb lap-color
                // or stint state if a telemetry bridge changes the session label.
                classPositions.Reset();
            }
            // This runs even when attaching AFTER green; no observed start
            // transition is required for correct lap colors or class positions.
            relativeCalculator.SetLapColorContext(isRace && state >= 4 && state <= 6, time);
            relativeTablePublisher.PublishContext(sessionType, state, isRace && state >= 4 && state <= 6);
            stintTracker.SetContext(isRace, state, time);
            classPositions.Update(participantBuffer, sessionDatabase, isRace, state, latestRawData);
            hasRelativeSessionState = true;
            lastRelativeSessionState = state;
            lastRelativeSessionNumber = number;
            lastRelativeSessionTime = time;
            if (sessionType.Length > 0) lastRelativeSessionType = sessionType;
            previousRelativePlayer = latestPlayerCarIndex;
        }

        private void EnrichParticipantsFromSessionInfo()
        {
            for (int carIndex = 0;
                 carIndex < participantBuffer.Capacity;
                 carIndex++)
            {
                ParticipantSnapshot participant = participantBuffer[carIndex];
                if (participant == null || !participant.IsValid)
                {
                    continue;
                }

                DriverIdentity identity;
                if (sessionDatabase.TryGet(carIndex, out identity) &&
                    identity != null)
                {
                    participant.CarId = identity.CarId;
                    participant.CarClassEstimatedLapTime =
                        identity.CarClassEstimatedLapTime;
                }
            }
        }


        private void UpdateOvertakeState()
        {
            double sessionTime = ReadDoubleValue(latestRawData, "SessionTime", -1.0);

            for (int carIndex = 0;
                 carIndex < participantBuffer.Capacity;
                 carIndex++)
            {
                ParticipantSnapshot participant = participantBuffer[carIndex];
                if (participant == null || !participant.IsValid)
                {
                    continue;
                }

                DriverIdentity identity;
                if (!sessionDatabase.TryGet(carIndex, out identity) || identity == null)
                {
                    continue;
                }

                string carPath = NormalizeCarPath(identity.CarPath);
                bool isSuperFormula =
                    carPath.Contains("superformulasf23");
                bool isIndyNxt =
                    carPath.Contains("dallarail15");
                bool isIndyCar =
                    carPath.Contains("dallarair18");

                bool compatibleCar =
                    isSuperFormula ||
                    isIndyNxt ||
                    isIndyCar;

                if (!compatibleCar || !participant.HasPushToPassTelemetry)
                {
                    participant.OvertakeSupported = false;
                    participant.OvertakeActive = false;
                    participant.OvertakeRemaining = 0;
                    continue;
                }

                int count = participant.RawPushToPassCount;

                // iRacing's per-opponent SF23 count can arrive as the raw
                // integer bits of a float. The local player's scalar P2P_Count
                // is already normal, so decode only remote SF23 cars.
                if (isSuperFormula && !participant.IsPlayer)
                {
                    count = DecodeSuperFormulaP2PCount(count);
                }

                if (count < 0) count = 0;
                if (count > 999) count = 999;

                bool active = participant.RawPushToPassStatus > 0;
                DateTime nowUtc = DateTime.UtcNow;

                // The local PushToPass scalar is only a momentary button state on
                // some iRacing cars. The remaining counter is the authoritative
                // signal that an overtake event is actually consuming. Each
                // decrement extends a wall-clock latch long enough to bridge the
                // interval until the next integer counter update. This deliberately
                // does NOT depend on SessionTime, because some SimHub frame routes
                // do not expose that scalar reliably for the local car.
                if (hasOvertakeCount[carIndex] &&
                    count < lastOvertakeCount[carIndex])
                {
                    lastOvertakeChangeTime[carIndex] = sessionTime;
                    overtakeActiveUntilUtc[carIndex] = nowUtc.AddSeconds(2.25);
                }

                // The button can light the pill immediately on the activation
                // frame. After release, the consumption latch keeps it blue until
                // the counter stops decreasing for long enough to prove the event
                // has ended.
                if (active)
                {
                    overtakeActiveUntilUtc[carIndex] = nowUtc.AddSeconds(2.25);
                }
                else if (overtakeActiveUntilUtc[carIndex] > nowUtc)
                {
                    active = true;
                }

                lastOvertakeCount[carIndex] = count;
                hasOvertakeCount[carIndex] = true;

                participant.OvertakeSupported = true;
                participant.OvertakeActive = active && count > 0;
                participant.OvertakeRemaining = count;
            }
        }

        private static int DecodeSuperFormulaP2PCount(int rawValue)
        {
            // Some telemetry bridges may already normalize the array to seconds.
            if (rawValue >= 0 && rawValue <= 999) return rawValue;

            try
            {
                byte[] bytes = BitConverter.GetBytes(rawValue);
                float decoded = BitConverter.ToSingle(bytes, 0) * 10.0f;
                if (float.IsNaN(decoded) || float.IsInfinity(decoded)) return 0;
                return (int)Math.Round(decoded, MidpointRounding.AwayFromZero);
            }
            catch
            {
                return 0;
            }
        }

        private static string NormalizeCarPath(string value)
        {
            return (value ?? string.Empty)
                .Trim()
                .ToLowerInvariant()
                .Replace("\\", string.Empty)
                .Replace("/", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty);
        }

        private void ResetOvertakeState()
        {
            for (int index = 0; index < lastOvertakeCount.Length; index++)
            {
                lastOvertakeCount[index] = 0;
                lastOvertakeChangeTime[index] = -1.0;
                overtakeActiveUntilUtc[index] = DateTime.MinValue;
                hasOvertakeCount[index] = false;
            }
        }

        private void ResetGapStateIfContextChanged()
        {
            int sessionNumber = ReadIntValue(latestRawData, "SessionNum", -1);
            double sessionTime = ReadDoubleValue(latestRawData, "SessionTime", -1.0);

            bool contextChanged = false;

            if (hasGapContext)
            {
                if (sessionNumber >= 0 && lastGapSessionNumber >= 0 &&
                    sessionNumber != lastGapSessionNumber)
                {
                    contextChanged = true;
                }

                // A new track/session often restarts SessionTime while SimHub
                // keeps the game-running state continuously true.
                if (sessionTime >= 0.0 && lastGapSessionTime >= 0.0 &&
                    sessionTime + 2.0 < lastGapSessionTime)
                {
                    contextChanged = true;
                }

                if (lastGapPlayerCarIndex >= 0 &&
                    latestPlayerCarIndex != lastGapPlayerCarIndex)
                {
                    contextChanged = true;
                }
            }

            if (contextChanged)
            {
                gapCalculator.Reset();
                gapFilter.Reset();
                ResetOvertakeState();
            }

            hasGapContext = true;
            lastGapSessionNumber = sessionNumber;
            lastGapSessionTime = sessionTime;
            lastGapPlayerCarIndex = latestPlayerCarIndex;
        }

        private static int ReadIntValue(object source, string name, int fallback)
        {
            object value = ReadRawValue(source, name);
            if (value == null) return fallback;
            try { return Convert.ToInt32(value, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }

        private static double ReadDoubleValue(object source, string name, double fallback)
        {
            object value = ReadRawValue(source, name);
            if (value == null) return fallback;
            try { return Convert.ToDouble(value, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }

        private static object ReadRawValue(object source, string name)
        {
            if (source == null || string.IsNullOrEmpty(name)) return null;

            IDictionary dictionary = source as IDictionary;
            if (dictionary != null && dictionary.Contains(name))
                return dictionary[name];

            Type type = source.GetType();
            PropertyInfo property = type.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property != null)
            {
                try { return property.GetValue(source, null); }
                catch { }
            }

            FieldInfo field = type.GetField(
                name,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (field != null)
            {
                try { return field.GetValue(source); }
                catch { }
            }

            return null;
        }

        private void PublishCurrentState()
        {
            relativePublisher.Publish(
                participantReader,
                participantBuffer,
                relativeSnapshot,
                updateTask);

            relativeDisplayPublisher.Publish(
                displaySnapshot);

            relativeTablePublisher.Publish(
                displaySnapshot);
        }
    }
}
