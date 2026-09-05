using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace Fulcrum.Core.Radar
{
    /// <summary>
    /// Native Fulcrum radar reader.
    ///
    /// Side state comes directly from iRacing CarLeftRight.  Longitudinal
    /// proximity is reconstructed from the native CarIdxLapDistPct array and
    /// WeekendInfo.TrackLength.  This intentionally replaces the
    /// iRacingExtraProperties DriverAhead/DriverBehind feed used by Radar
    /// v0.6.21 while preserving the sign convention expected by that dashboard:
    /// ahead = negative metres, behind = positive metres.
    /// </summary>
    public sealed class RadarReader
    {
        private static readonly TimeSpan SideStateHold =
            TimeSpan.FromMilliseconds(800.0);

        private const int MaxCars = 64;
        private const float NearZeroMeters = 0.005f;
        private const float NearbyWakeAheadMeters = 10.0f;
        private const float NearbyWakeBehindMeters = 20.0f;

        private int lastActiveState;
        private DateTime lastActiveAtUtc;
        private float cachedTrackLengthMeters;

        public bool HasTelemetry { get; private set; }
        public string Error { get; private set; }

        public RadarReader()
        {
            lastActiveState = 0;
            lastActiveAtUtc = DateTime.MinValue;
            cachedTrackLengthMeters = 0.0f;
            ResetStatus();
        }

        public void Update(object rawData, RadarSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            ResetStatus();

            if (rawData == null)
            {
                Reset(snapshot);
                return;
            }

            try
            {
                object telemetry = ReadProperty(rawData, "CurrentTelemetry");
                if (telemetry == null)
                {
                    telemetry = ReadProperty(rawData, "Telemetry");
                }

                if (telemetry == null)
                {
                    Reset(snapshot);
                    Error = "iRacing telemetry is unavailable";
                    return;
                }

                object value;
                if (!TryReadTelemetryValue(telemetry, "CarLeftRight", out value))
                {
                    Reset(snapshot);
                    Error = "CarLeftRight telemetry is unavailable";
                    return;
                }

                int rawState = ToInt(value, 0);
                DateTime nowUtc = DateTime.UtcNow;

                bool isOnTrack = false;
                if (TryReadTelemetryValue(telemetry, "IsOnTrack", out value))
                {
                    isOnTrack = ToBool(value, false);
                }

                bool isReplayPlaying = false;
                if (TryReadTelemetryValue(telemetry, "IsReplayPlaying", out value))
                {
                    isReplayPlaying = ToBool(value, false);
                }

                bool isOnPitRoad = false;
                if (TryReadTelemetryValue(telemetry, "OnPitRoad", out value))
                {
                    isOnPitRoad = ToBool(value, false);
                }

                snapshot.Reset();
                snapshot.CapturedAt = nowUtc;
                snapshot.RawState = rawState;
                snapshot.IsOnTrack = isOnTrack;
                snapshot.IsReplayPlaying = isReplayPlaying;
                snapshot.IsOnPitRoad = isOnPitRoad;

                // v0.6.21 could remain useful while the player was stopped or
                // slightly off-track.  Do not make IsOnTrack a visibility gate.
                // Replay is the only raw-data context that invalidates proximity.
                snapshot.ContextValid = !isReplayPlaying;

                PopulateNativeProximity(rawData, telemetry, snapshot);

                if (isReplayPlaying)
                {
                    lastActiveState = 0;
                    lastActiveAtUtc = DateTime.MinValue;
                    PopulateStable(0, snapshot);
                    snapshot.ShouldShow = false;
                    snapshot.InputSource = "ReplayBlocked";
                    HasTelemetry = true;
                    return;
                }

                if (IsActiveSideState(rawState))
                {
                    lastActiveState = rawState;
                    lastActiveAtUtc = nowUtc;
                    PopulateStable(rawState, snapshot);
                }
                else
                {
                    TimeSpan sinceActive =
                        lastActiveAtUtc == DateTime.MinValue
                            ? TimeSpan.MaxValue
                            : nowUtc - lastActiveAtUtc;

                    if (IsActiveSideState(lastActiveState) &&
                        sinceActive >= TimeSpan.Zero &&
                        sinceActive < SideStateHold)
                    {
                        PopulateStable(lastActiveState, snapshot);
                        snapshot.IsHeld = true;
                        snapshot.HoldRemainingMilliseconds =
                            Math.Max(0.0, (SideStateHold - sinceActive).TotalMilliseconds);
                    }
                    else
                    {
                        PopulateStable(rawState, snapshot);
                        lastActiveState = 0;
                        lastActiveAtUtc = DateTime.MinValue;
                    }
                }

                // Diagnostic/native gate only.  The migrated v0.6.21 dashboard
                // keeps its original visibility formula, now fed by Fulcrum's
                // native distances.
                snapshot.ShouldShow =
                    snapshot.ContextValid &&
                    !snapshot.IsOnPitRoad &&
                    (snapshot.IsActive || snapshot.HasNearbyLongitudinalContact);

                snapshot.InputSource = snapshot.NativeDistanceReady
                    ? "NativeCarIdxLapDistPct"
                    : "NativeCarLeftRightOnly";

                HasTelemetry = true;
            }
            catch (Exception exception)
            {
                Reset(snapshot);
                Error = exception.GetType().Name + ": " + exception.Message;
            }
        }

        public void Reset(RadarSnapshot snapshot)
        {
            ResetStatus();
            lastActiveState = 0;
            lastActiveAtUtc = DateTime.MinValue;
            cachedTrackLengthMeters = 0.0f;

            if (snapshot != null)
            {
                snapshot.Reset();
            }
        }

        private void ResetStatus()
        {
            HasTelemetry = false;
            Error = string.Empty;
        }

        private static bool IsActiveSideState(int state)
        {
            return state >= 2 && state <= 6;
        }

        private static void PopulateStable(int stableState, RadarSnapshot snapshot)
        {
            snapshot.StableState = stableState;

            switch (stableState)
            {
                case 1:
                    snapshot.State = "Clear";
                    snapshot.Callout = "CLEAR";
                    break;
                case 2:
                    snapshot.State = "CarLeft";
                    snapshot.HasCarLeft = true;
                    snapshot.LeftCarCount = 1;
                    snapshot.Callout = "CAR LEFT";
                    break;
                case 3:
                    snapshot.State = "CarRight";
                    snapshot.HasCarRight = true;
                    snapshot.RightCarCount = 1;
                    snapshot.Callout = "CAR RIGHT";
                    break;
                case 4:
                    snapshot.State = "CarsBothSides";
                    snapshot.HasCarLeft = true;
                    snapshot.HasCarRight = true;
                    snapshot.HasCarsBothSides = true;
                    snapshot.LeftCarCount = 1;
                    snapshot.RightCarCount = 1;
                    snapshot.Callout = "THREE WIDE";
                    break;
                case 5:
                    snapshot.State = "TwoCarsLeft";
                    snapshot.HasCarLeft = true;
                    snapshot.HasTwoCarsLeft = true;
                    snapshot.LeftCarCount = 2;
                    snapshot.Callout = "TWO LEFT";
                    break;
                case 6:
                    snapshot.State = "TwoCarsRight";
                    snapshot.HasCarRight = true;
                    snapshot.HasTwoCarsRight = true;
                    snapshot.RightCarCount = 2;
                    snapshot.Callout = "TWO RIGHT";
                    break;
                default:
                    snapshot.State = stableState == 0 ? "Off" : "Unknown";
                    snapshot.Callout = string.Empty;
                    break;
            }

            snapshot.TotalCarCount = snapshot.LeftCarCount + snapshot.RightCarCount;
            snapshot.IsActive = snapshot.TotalCarCount > 0;
            snapshot.Severity = snapshot.HasCarsBothSides || snapshot.TotalCarCount >= 2
                ? 2
                : snapshot.IsActive ? 1 : 0;
        }

        private void PopulateNativeProximity(
            object rawData,
            object telemetry,
            RadarSnapshot snapshot)
        {
            object value;
            int playerCarIndex = -1;

            if (TryReadTelemetryValue(telemetry, "PlayerCarIdx", out value))
            {
                playerCarIndex = ToInt(value, -1);
            }

            snapshot.PlayerCarIndex = playerCarIndex;

            object carIdxLapDistPct;
            if (!TryReadTelemetryValue(telemetry, "CarIdxLapDistPct", out carIdxLapDistPct) ||
                carIdxLapDistPct == null ||
                playerCarIndex < 0 || playerCarIndex >= MaxCars)
            {
                return;
            }

            object carIdxTrackSurface = null;
            object carIdxOnPitRoad = null;
            TryReadTelemetryValue(telemetry, "CarIdxTrackSurface", out carIdxTrackSurface);
            TryReadTelemetryValue(telemetry, "CarIdxOnPitRoad", out carIdxOnPitRoad);

            float playerPct = GetIndexedFloat(carIdxLapDistPct, playerCarIndex, -1.0f);
            if (!IsValidLapPct(playerPct) && TryReadTelemetryValue(telemetry, "LapDistPct", out value))
            {
                playerPct = ToFloat(value, -1.0f);
            }

            snapshot.PlayerLapDistancePercent = playerPct;
            if (!IsValidLapPct(playerPct))
            {
                return;
            }

            float parsedTrackLength = ResolveTrackLengthMeters(rawData);
            if (parsedTrackLength > 100.0f)
            {
                cachedTrackLengthMeters = parsedTrackLength;
            }

            float trackLengthMeters = cachedTrackLengthMeters;
            snapshot.TrackLengthMeters = trackLengthMeters;
            if (trackLengthMeters <= 100.0f)
            {
                return;
            }

            snapshot.NativeDistanceReady = true;

            float ahead0Magnitude = float.MaxValue;
            float ahead1Magnitude = float.MaxValue;
            float behind0Magnitude = float.MaxValue;
            float behind1Magnitude = float.MaxValue;

            for (int carIndex = 0; carIndex < MaxCars; carIndex++)
            {
                if (carIndex == playerCarIndex)
                {
                    continue;
                }

                float otherPct = GetIndexedFloat(carIdxLapDistPct, carIndex, -1.0f);
                if (!IsValidLapPct(otherPct))
                {
                    continue;
                }

                int trackSurface = GetIndexedInt(carIdxTrackSurface, carIndex, 3);
                if (trackSurface < 0)
                {
                    continue;
                }

                bool isOnPitRoad = GetIndexedBool(carIdxOnPitRoad, carIndex, false);
                bool isInPit = isOnPitRoad || trackSurface == 1 || trackSurface == 2;

                float deltaPct = otherPct - playerPct;
                if (deltaPct > 0.5f)
                {
                    deltaPct -= 1.0f;
                }
                else if (deltaPct < -0.5f)
                {
                    deltaPct += 1.0f;
                }

                // Dashboard convention inherited from v0.6.21:
                // ahead negative; behind positive.
                float signedDistance = -deltaPct * trackLengthMeters;
                if (!IsFinite(signedDistance))
                {
                    continue;
                }

                // Exact equality is possible for a true side-by-side overlap.
                // Give it a tiny ahead sign so v0.6.21's != 0 lateral formulas
                // still render a car while the distance remains visually centred.
                if (Math.Abs(signedDistance) < NearZeroMeters)
                {
                    signedDistance = -NearZeroMeters;
                }

                if (signedDistance < 0.0f)
                {
                    float magnitude = -signedDistance;
                    if (magnitude < ahead0Magnitude)
                    {
                        ahead1Magnitude = ahead0Magnitude;
                        snapshot.Ahead01CarIndex = snapshot.Ahead00CarIndex;
                        snapshot.Ahead01DistanceMeters = snapshot.Ahead00DistanceMeters;
                        snapshot.Ahead01IsInPit = snapshot.Ahead00IsInPit;

                        ahead0Magnitude = magnitude;
                        snapshot.Ahead00CarIndex = carIndex;
                        snapshot.Ahead00DistanceMeters = signedDistance;
                        snapshot.Ahead00IsInPit = isInPit;
                    }
                    else if (magnitude < ahead1Magnitude)
                    {
                        ahead1Magnitude = magnitude;
                        snapshot.Ahead01CarIndex = carIndex;
                        snapshot.Ahead01DistanceMeters = signedDistance;
                        snapshot.Ahead01IsInPit = isInPit;
                    }
                }
                else
                {
                    float magnitude = signedDistance;
                    if (magnitude < behind0Magnitude)
                    {
                        behind1Magnitude = behind0Magnitude;
                        snapshot.Behind01CarIndex = snapshot.Behind00CarIndex;
                        snapshot.Behind01DistanceMeters = snapshot.Behind00DistanceMeters;
                        snapshot.Behind01IsInPit = snapshot.Behind00IsInPit;

                        behind0Magnitude = magnitude;
                        snapshot.Behind00CarIndex = carIndex;
                        snapshot.Behind00DistanceMeters = signedDistance;
                        snapshot.Behind00IsInPit = isInPit;
                    }
                    else if (magnitude < behind1Magnitude)
                    {
                        behind1Magnitude = magnitude;
                        snapshot.Behind01CarIndex = carIndex;
                        snapshot.Behind01DistanceMeters = signedDistance;
                        snapshot.Behind01IsInPit = isInPit;
                    }
                }
            }

            snapshot.AheadDistanceAvailable = snapshot.Ahead00CarIndex >= 0;
            snapshot.BehindDistanceAvailable = snapshot.Behind00CarIndex >= 0;
            snapshot.HasLongitudinalData =
                snapshot.AheadDistanceAvailable || snapshot.BehindDistanceAvailable;

            snapshot.AheadDistanceMeters = snapshot.AheadDistanceAvailable
                ? snapshot.Ahead00DistanceMeters
                : 0.0f;
            snapshot.BehindDistanceMeters = snapshot.BehindDistanceAvailable
                ? snapshot.Behind00DistanceMeters
                : 0.0f;

            bool aheadNearby =
                snapshot.Ahead00CarIndex >= 0 &&
                !snapshot.Ahead00IsInPit &&
                snapshot.Ahead00DistanceMeters < 0.0f &&
                snapshot.Ahead00DistanceMeters >= -NearbyWakeAheadMeters;

            bool behindNearby =
                snapshot.Behind00CarIndex >= 0 &&
                !snapshot.Behind00IsInPit &&
                snapshot.Behind00DistanceMeters > 0.0f &&
                snapshot.Behind00DistanceMeters < NearbyWakeBehindMeters;

            snapshot.HasNearbyLongitudinalContact = aheadNearby || behindNearby;
        }

        private static float ResolveTrackLengthMeters(object rawData)
        {
            object sessionData = GetMemberValue(rawData, "SessionData");
            if (sessionData == null)
            {
                sessionData = GetMemberValue(rawData, "AllSessionData");
            }

            object weekendInfo = GetMemberValue(sessionData, "WeekendInfo");
            object trackLength = GetMemberValue(weekendInfo, "TrackLength");

            float parsed = ParseTrackLengthMeters(trackLength);
            if (parsed > 100.0f)
            {
                return parsed;
            }

            // SimHub also exposes flattened session dictionaries on some builds.
            object flat = GetMemberValue(rawData, "SessionDataDict");
            trackLength = GetDictionaryValueIgnoreCase(flat, "WeekendInfo:TrackLength");
            if (trackLength == null)
            {
                trackLength = GetDictionaryValueIgnoreCase(flat, "WeekendInfo.TrackLength");
            }

            parsed = ParseTrackLengthMeters(trackLength);
            if (parsed > 100.0f)
            {
                return parsed;
            }

            trackLength = GetDictionaryValueIgnoreCase(sessionData, "WeekendInfo:TrackLength");
            return ParseTrackLengthMeters(trackLength);
        }

        private static float ParseTrackLengthMeters(object value)
        {
            if (value == null)
            {
                return 0.0f;
            }

            if (value is float || value is double || value is decimal ||
                value is int || value is long || value is short)
            {
                float numeric = ToFloat(value, 0.0f);
                if (!IsFinite(numeric) || numeric <= 0.0f)
                {
                    return 0.0f;
                }

                // iRacing WeekendInfo normally represents TrackLength in km.
                return numeric <= 100.0f ? numeric * 1000.0f : numeric;
            }

            string text = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0.0f;
            }

            string normalized = text.Trim().ToLowerInvariant().Replace(',', '.');
            int end = 0;
            while (end < normalized.Length)
            {
                char ch = normalized[end];
                if ((ch >= '0' && ch <= '9') || ch == '.' || ch == '-' || ch == '+')
                {
                    end++;
                }
                else
                {
                    break;
                }
            }

            if (end <= 0)
            {
                return 0.0f;
            }

            float number;
            if (!float.TryParse(
                    normalized.Substring(0, end),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out number) ||
                !IsFinite(number) ||
                number <= 0.0f)
            {
                return 0.0f;
            }

            if (normalized.Contains("mi"))
            {
                return number * 1609.344f;
            }

            if (normalized.Contains("km"))
            {
                return number * 1000.0f;
            }

            if (normalized.Contains(" m"))
            {
                return number;
            }

            return number <= 100.0f ? number * 1000.0f : number;
        }

        private static bool IsValidLapPct(float value)
        {
            return IsFinite(value) && value >= 0.0f && value <= 1.0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static object GetDictionaryValueIgnoreCase(object source, string key)
        {
            if (source == null || string.IsNullOrEmpty(key))
            {
                return null;
            }

            IDictionary dictionary = source as IDictionary;
            if (dictionary == null)
            {
                return null;
            }

            if (dictionary.Contains(key))
            {
                return dictionary[key];
            }

            foreach (DictionaryEntry entry in dictionary)
            {
                if (string.Equals(
                        Convert.ToString(entry.Key, CultureInfo.InvariantCulture),
                        key,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Value;
                }
            }

            return null;
        }

        private static object GetMemberValue(object source, string name)
        {
            if (source == null)
            {
                return null;
            }

            object dictionaryValue = GetDictionaryValueIgnoreCase(source, name);
            if (dictionaryValue != null)
            {
                return dictionaryValue;
            }

            const BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.IgnoreCase;

            Type type = source.GetType();
            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null && property.GetIndexParameters().Length == 0)
            {
                try
                {
                    return property.GetValue(source, null);
                }
                catch
                {
                    return null;
                }
            }

            FieldInfo field = type.GetField(name, flags);
            if (field != null)
            {
                try
                {
                    return field.GetValue(source);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        private static bool TryReadTelemetryValue(object telemetry, string key, out object value)
        {
            value = null;
            if (telemetry == null)
            {
                return false;
            }

            IDictionary<string, object> generic = telemetry as IDictionary<string, object>;
            if (generic != null)
            {
                return generic.TryGetValue(key, out value);
            }

            IReadOnlyDictionary<string, object> readOnly = telemetry as IReadOnlyDictionary<string, object>;
            if (readOnly != null)
            {
                return readOnly.TryGetValue(key, out value);
            }

            IDictionary dictionary = telemetry as IDictionary;
            if (dictionary != null && dictionary.Contains(key))
            {
                value = dictionary[key];
                return true;
            }

            IEnumerable enumerable = telemetry as IEnumerable;
            if (enumerable == null)
            {
                return false;
            }

            foreach (object item in enumerable)
            {
                if (item == null)
                {
                    continue;
                }

                object itemKey = ReadProperty(item, "Key");
                if (!string.Equals(
                        Convert.ToString(itemKey, CultureInfo.InvariantCulture),
                        key,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                value = ReadProperty(item, "Value");
                return true;
            }

            return false;
        }

        private static object ReadProperty(object source, string name)
        {
            if (source == null)
            {
                return null;
            }

            PropertyInfo property = source.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

            if (property == null || property.GetIndexParameters().Length != 0)
            {
                return null;
            }

            try
            {
                return property.GetValue(source, null);
            }
            catch
            {
                return null;
            }
        }

        private static object GetIndexedValue(object collection, int index)
        {
            if (collection == null || index < 0)
            {
                return null;
            }

            Array array = collection as Array;
            if (array != null)
            {
                return index < array.Length ? array.GetValue(index) : null;
            }

            IList list = collection as IList;
            if (list != null)
            {
                return index < list.Count ? list[index] : null;
            }

            return null;
        }

        private static int GetIndexedInt(object collection, int index, int fallback)
        {
            object value = GetIndexedValue(collection, index);
            return value == null ? fallback : ToInt(value, fallback);
        }

        private static float GetIndexedFloat(object collection, int index, float fallback)
        {
            object value = GetIndexedValue(collection, index);
            return value == null ? fallback : ToFloat(value, fallback);
        }

        private static bool GetIndexedBool(object collection, int index, bool fallback)
        {
            object value = GetIndexedValue(collection, index);
            return value == null ? fallback : ToBool(value, fallback);
        }

        private static int ToInt(object value, int fallback)
        {
            if (value == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static float ToFloat(object value, float fallback)
        {
            if (value == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToSingle(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static bool ToBool(object value, bool fallback)
        {
            if (value == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }
    }
}
