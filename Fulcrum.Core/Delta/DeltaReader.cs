using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace Fulcrum.Core.Delta
{
    /// <summary>
    /// Reads iRacing native lap-delta telemetry and applies a light, low-latency filter.
    /// </summary>
    public sealed class DeltaReader
    {
        private const float SmoothingFactor = 0.42f;
        private const float NeutralThresholdSeconds = 0.005f;
        private const float TrendThresholdSecondsPerSecond = 0.02f;
        private const float BarFullScaleSeconds = 2.0f;
        private const float MaximumUsableDeltaSeconds = 120.0f;

        private bool initialized;
        private float filteredDelta;
        private float previousFilteredDelta;
        private DateTime previousCaptureUtc;

        public bool HasTelemetry { get; private set; }
        public string Error { get; private set; }

        public DeltaReader()
        {
            ResetStatus();
        }

        public void Update(object rawData, DeltaSnapshot snapshot)
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
                    Error = "Telemetry object is unavailable";
                    return;
                }

                float rawDelta;
                string reference;
                bool valid;
                ReadBestDelta(telemetry, out rawDelta, out reference, out valid);

                float currentLap = ReadFloat(telemetry, "LapCurrentLapTime", 0.0f);
                float lastLap = ReadFloat(telemetry, "LapLastLapTime", 0.0f);
                float bestLap = ReadFloat(telemetry, "LapBestLapTime", 0.0f);

                DateTime now = DateTime.UtcNow;
                snapshot.CapturedAt = now;
                snapshot.CurrentLapTimeSeconds = SanitizeLapTime(currentLap);
                snapshot.LastLapTimeSeconds = SanitizeLapTime(lastLap);
                snapshot.BestLapTimeSeconds = SanitizeLapTime(bestLap);
                snapshot.CurrentLapTimeText = FormatLapTime(snapshot.CurrentLapTimeSeconds);
                snapshot.LastLapTimeText = FormatLapTime(snapshot.LastLapTimeSeconds);
                snapshot.BestLapTimeText = FormatLapTime(snapshot.BestLapTimeSeconds);
                snapshot.Reference = reference;
                snapshot.Ready = true;

                if (!valid || !IsUsableDelta(rawDelta))
                {
                    ClearDeltaValues(snapshot);
                    snapshot.Status = "NoReference";
                    HasTelemetry = true;
                    return;
                }

                if (!initialized || Math.Abs(rawDelta - filteredDelta) > 10.0f)
                {
                    initialized = true;
                    filteredDelta = rawDelta;
                    previousFilteredDelta = rawDelta;
                    previousCaptureUtc = now;
                }
                else
                {
                    previousFilteredDelta = filteredDelta;
                    filteredDelta += SmoothingFactor * (rawDelta - filteredDelta);
                }

                double elapsed = previousCaptureUtc == DateTime.MinValue
                    ? 0.0
                    : (now - previousCaptureUtc).TotalSeconds;

                float rate = elapsed > 0.001
                    ? (float)((filteredDelta - previousFilteredDelta) / elapsed)
                    : 0.0f;

                previousCaptureUtc = now;

                snapshot.IsValid = true;
                snapshot.RawDeltaSeconds = rawDelta;
                snapshot.DeltaSeconds = filteredDelta;
                snapshot.DeltaRateSecondsPerSecond = IsFinite(rate) ? rate : 0.0f;
                snapshot.BarValue = Clamp(filteredDelta / BarFullScaleSeconds, -1.0f, 1.0f);
                snapshot.DeltaText = FormatDelta(filteredDelta);
                snapshot.IsImproving = filteredDelta < -NeutralThresholdSeconds;
                snapshot.IsLosing = filteredDelta > NeutralThresholdSeconds;
                snapshot.IsNeutral = !snapshot.IsImproving && !snapshot.IsLosing;
                snapshot.Direction = snapshot.IsImproving
                    ? "Improving"
                    : snapshot.IsLosing ? "Losing" : "Neutral";
                snapshot.Trend = rate < -TrendThresholdSecondsPerSecond
                    ? "Gaining"
                    : rate > TrendThresholdSecondsPerSecond ? "Losing" : "Stable";
                snapshot.Status = "Active";
                HasTelemetry = true;
            }
            catch (Exception exception)
            {
                Reset(snapshot);
                Error = exception.GetType().Name + ": " + exception.Message;
            }
        }

        public void Reset(DeltaSnapshot snapshot)
        {
            initialized = false;
            filteredDelta = 0.0f;
            previousFilteredDelta = 0.0f;
            previousCaptureUtc = DateTime.MinValue;
            ResetStatus();

            if (snapshot != null)
            {
                snapshot.Reset();
            }
        }

        private static void ReadBestDelta(object telemetry, out float delta, out string reference, out bool valid)
        {
            delta = 0.0f;
            reference = "Unavailable";
            valid = false;

            if (TryReadDelta(telemetry, "LapDeltaToSessionBestLap", "LapDeltaToSessionBestLap_OK", out delta))
            {
                reference = "SessionBest";
                valid = true;
                return;
            }

            if (TryReadDelta(telemetry, "LapDeltaToBestLap", "LapDeltaToBestLap_OK", out delta))
            {
                reference = "PersonalBest";
                valid = true;
                return;
            }

            if (TryReadDelta(telemetry, "LapDeltaToSessionOptimalLap", "LapDeltaToSessionOptimalLap_OK", out delta))
            {
                reference = "SessionOptimal";
                valid = true;
                return;
            }

            if (TryReadDelta(telemetry, "LapDeltaToOptimalLap", "LapDeltaToOptimalLap_OK", out delta))
            {
                reference = "PersonalOptimal";
                valid = true;
            }
        }

        private static bool TryReadDelta(object telemetry, string valueKey, string okKey, out float delta)
        {
            delta = 0.0f;
            object value;
            if (!TryReadTelemetryValue(telemetry, valueKey, out value))
            {
                return false;
            }

            object okValue;
            if (TryReadTelemetryValue(telemetry, okKey, out okValue) && !ToBool(okValue, false))
            {
                return false;
            }

            delta = ToFloat(value, 0.0f);
            return IsUsableDelta(delta);
        }

        private static void ClearDeltaValues(DeltaSnapshot snapshot)
        {
            snapshot.IsValid = false;
            snapshot.RawDeltaSeconds = 0.0f;
            snapshot.DeltaSeconds = 0.0f;
            snapshot.DeltaRateSecondsPerSecond = 0.0f;
            snapshot.BarValue = 0.0f;
            snapshot.DeltaText = "--.---";
            snapshot.Direction = "Neutral";
            snapshot.Trend = "Stable";
            snapshot.IsImproving = false;
            snapshot.IsLosing = false;
            snapshot.IsNeutral = true;
        }

        private void ResetStatus()
        {
            HasTelemetry = false;
            Error = string.Empty;
        }

        private static float ReadFloat(object telemetry, string key, float fallback)
        {
            object value;
            return TryReadTelemetryValue(telemetry, key, out value)
                ? ToFloat(value, fallback)
                : fallback;
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
                if (!string.Equals(Convert.ToString(itemKey, CultureInfo.InvariantCulture), key, StringComparison.OrdinalIgnoreCase))
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

            return property.GetValue(source, null);
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

        private static float SanitizeLapTime(float value)
        {
            return IsFinite(value) && value > 0.0f && value < 86400.0f
                ? value
                : 0.0f;
        }

        private static bool IsUsableDelta(float value)
        {
            return IsFinite(value) && Math.Abs(value) <= MaximumUsableDeltaSeconds;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }

        private static string FormatDelta(float value)
        {
            if (!IsFinite(value))
            {
                return "--.---";
            }

            return value >= 0.0f
                ? "+" + value.ToString("0.000", CultureInfo.InvariantCulture)
                : value.ToString("0.000", CultureInfo.InvariantCulture);
        }

        private static string FormatLapTime(float seconds)
        {
            if (!IsFinite(seconds) || seconds <= 0.0f)
            {
                return "--:--.---";
            }

            TimeSpan time = TimeSpan.FromSeconds(seconds);
            int totalMinutes = (int)time.TotalMinutes;
            return totalMinutes.ToString("00", CultureInfo.InvariantCulture) + ":" +
                   time.Seconds.ToString("00", CultureInfo.InvariantCulture) + "." +
                   time.Milliseconds.ToString("000", CultureInfo.InvariantCulture);
        }
    }
}
