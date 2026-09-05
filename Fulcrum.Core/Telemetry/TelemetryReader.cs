using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace Fulcrum.Core.Telemetry
{
    public sealed class TelemetryReader
    {
        private Type cachedRawDataType;
        private Func<object, object> telemetryGetter;
        private Func<object, object> currentSessionInfoGetter;

        private Type cachedTelemetryItemType;
        private Func<object, object> telemetryKeyGetter;
        private Func<object, object> telemetryValueGetter;

        private Type cachedSessionInfoType;
        private Func<object, object> sessionTypeGetter;

        public bool IsUsingDirectLookup
        {
            get;
            private set;
        }

        public void Update(
            object rawData,
            bool gameRunning,
            string gameName,
            TelemetrySnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(
                    nameof(snapshot));
            }

            ResetSnapshot(
                snapshot,
                gameRunning,
                gameName);

            IsUsingDirectLookup = false;

            if (rawData == null)
            {
                return;
            }

            EnsureRawDataAccessors(
                rawData.GetType());

            object telemetryObject =
                GetValueSafely(
                    telemetryGetter,
                    rawData);

            snapshot.SessionType =
                ReadSessionType(rawData);

            ReadTelemetry(
                telemetryObject,
                snapshot);
        }

        public void Reset(
            TelemetrySnapshot snapshot,
            string gameName)
        {
            if (snapshot == null)
            {
                return;
            }

            IsUsingDirectLookup = false;

            ResetSnapshot(
                snapshot,
                false,
                gameName);
        }

        private void ReadTelemetry(
            object telemetryObject,
            TelemetrySnapshot snapshot)
        {
            if (telemetryObject == null)
            {
                return;
            }

            IDictionary<string, object> genericDictionary =
                telemetryObject as IDictionary<string, object>;

            if (genericDictionary != null)
            {
                IsUsingDirectLookup = true;

                ReadGenericDictionary(
                    genericDictionary,
                    snapshot);

                return;
            }

            IReadOnlyDictionary<string, object> readOnlyDictionary =
                telemetryObject as
                    IReadOnlyDictionary<string, object>;

            if (readOnlyDictionary != null)
            {
                IsUsingDirectLookup = true;

                ReadReadOnlyDictionary(
                    readOnlyDictionary,
                    snapshot);

                return;
            }

            IDictionary nonGenericDictionary =
                telemetryObject as IDictionary;

            if (nonGenericDictionary != null)
            {
                IsUsingDirectLookup = true;

                ReadNonGenericDictionary(
                    nonGenericDictionary,
                    snapshot);

                return;
            }

            ReadEnumerableFallback(
                telemetryObject,
                snapshot);
        }

        private static void ReadGenericDictionary(
            IDictionary<string, object> telemetry,
            TelemetrySnapshot snapshot)
        {
            object value;

            if (telemetry.TryGetValue(
                    "SessionTime",
                    out value))
            {
                snapshot.SessionTime =
                    ToDouble(value, 0.0);
            }

            if (telemetry.TryGetValue(
                    "SessionTimeRemain",
                    out value))
            {
                snapshot.SessionTimeRemaining =
                    ToDouble(value, 0.0);
            }

            if (telemetry.TryGetValue("SessionNum", out value))
            {
                snapshot.SessionNumber = ToInt(value, -1);
            }

            if (telemetry.TryGetValue("SessionState", out value))
            {
                snapshot.SessionState = ToInt(value, 0);
            }

            if (telemetry.TryGetValue("SessionFlags", out value))
            {
                snapshot.SessionFlags = ToLong(value, 0L);
            }

            if (telemetry.TryGetValue("SessionLapsRemain", out value))
            {
                snapshot.SessionLapsRemaining = ToInt(value, 0);
            }

            if (telemetry.TryGetValue("SessionLapsTotal", out value))
            {
                snapshot.SessionLapsTotal = ToInt(value, 0);
            }

            if (telemetry.TryGetValue(
                    "PlayerCarIdx",
                    out value))
            {
                snapshot.PlayerCarIndex =
                    ToInt(value, -1);
            }

            if (telemetry.TryGetValue(
                    "PlayerCarPosition",
                    out value))
            {
                snapshot.PlayerPosition =
                    ToInt(value, 0);
            }

            if (telemetry.TryGetValue(
                    "PlayerCarClassPosition",
                    out value))
            {
                snapshot.PlayerClassPosition =
                    ToInt(value, 0);
            }

            if (telemetry.TryGetValue(
                    "PlayerCarClass",
                    out value))
            {
                snapshot.PlayerClassId =
                    ToInt(value, -1);
            }

            if (telemetry.TryGetValue(
                    "Lap",
                    out value))
            {
                snapshot.Lap =
                    ToInt(value, 0);
            }

            if (telemetry.TryGetValue(
                    "LapCompleted",
                    out value))
            {
                snapshot.LapCompleted =
                    ToInt(value, 0);
            }

            if (telemetry.TryGetValue(
                    "LapDistPct",
                    out value))
            {
                snapshot.LapDistancePercent =
                    ToFloat(value, 0.0f);
            }

            if (telemetry.TryGetValue(
                    "Speed",
                    out value))
            {
                snapshot.SpeedMetersPerSecond =
                    ToFloat(value, 0.0f);
            }

            if (telemetry.TryGetValue(
                    "Throttle",
                    out value))
            {
                snapshot.Throttle =
                    ToFloat(value, 0.0f);
            }

            if (telemetry.TryGetValue(
                    "Brake",
                    out value))
            {
                snapshot.Brake =
                    ToFloat(value, 0.0f);
            }

            if (telemetry.TryGetValue(
                    "Clutch",
                    out value))
            {
                snapshot.Clutch =
                    ToFloat(value, 0.0f);
            }

            if (telemetry.TryGetValue(
                    "Gear",
                    out value))
            {
                snapshot.Gear =
                    ToInt(value, 0);
            }

            if (telemetry.TryGetValue(
                    "RPM",
                    out value))
            {
                snapshot.Rpm =
                    ToFloat(value, 0.0f);
            }

            if (telemetry.TryGetValue(
                    "IsOnTrack",
                    out value))
            {
                snapshot.IsOnTrack =
                    ToBool(value, false);
            }

            if (telemetry.TryGetValue(
                    "OnPitRoad",
                    out value))
            {
                snapshot.IsOnPitRoad =
                    ToBool(value, false);
            }

            if (telemetry.TryGetValue(
                    "IsReplayPlaying",
                    out value))
            {
                snapshot.IsReplayPlaying =
                    ToBool(value, false);
            }

            if (telemetry.TryGetValue(
                    "TrackTemp",
                    out value))
            {
                snapshot.TrackTemperatureCelsius =
                    ToFloat(value, 0.0f);
            }

            if (telemetry.TryGetValue(
                    "AirTemp",
                    out value))
            {
                snapshot.AirTemperatureCelsius =
                    ToFloat(value, 0.0f);
            }

            if (telemetry.TryGetValue(
                    "FuelLevel",
                    out value))
            {
                snapshot.FuelLevelLiters =
                    ToDouble(value, 0.0);
            }

            if (telemetry.TryGetValue(
                    "FuelLevelPct",
                    out value))
            {
                double pct = ToDouble(value, 0.0);
                snapshot.FuelLevelPercent =
                    pct > 1.5 ? pct / 100.0 : pct;
            }

            object carIdxPositionValues = null;
            object carIdxClassPositionValues = null;

            telemetry.TryGetValue(
                "CarIdxPosition",
                out carIdxPositionValues);

            telemetry.TryGetValue(
                "CarIdxClassPosition",
                out carIdxClassPositionValues);

            UpdatePlayerPositions(
                snapshot,
                carIdxPositionValues,
                carIdxClassPositionValues);
        }

        private static void ReadReadOnlyDictionary(
            IReadOnlyDictionary<string, object> telemetry,
            TelemetrySnapshot snapshot)
        {
            object value;

            if (telemetry.TryGetValue(
                    "SessionTime",
                    out value))
            {
                snapshot.SessionTime =
                    ToDouble(value, 0.0);
            }

            if (telemetry.TryGetValue(
                    "SessionTimeRemain",
                    out value))
            {
                snapshot.SessionTimeRemaining =
                    ToDouble(value, 0.0);
            }

            if (telemetry.TryGetValue("SessionNum", out value))
            {
                snapshot.SessionNumber = ToInt(value, -1);
            }

            if (telemetry.TryGetValue("SessionState", out value))
            {
                snapshot.SessionState = ToInt(value, 0);
            }

            if (telemetry.TryGetValue("SessionFlags", out value))
            {
                snapshot.SessionFlags = ToLong(value, 0L);
            }

            if (telemetry.TryGetValue("SessionLapsRemain", out value))
            {
                snapshot.SessionLapsRemaining = ToInt(value, 0);
            }

            if (telemetry.TryGetValue("SessionLapsTotal", out value))
            {
                snapshot.SessionLapsTotal = ToInt(value, 0);
            }

            if (telemetry.TryGetValue(
                    "PlayerCarIdx",
                    out value))
            {
                snapshot.PlayerCarIndex =
                    ToInt(value, -1);
            }

            if (telemetry.TryGetValue(
                    "PlayerCarPosition",
                    out value))
            {
                snapshot.PlayerPosition =
                    ToInt(value, 0);
            }

            if (telemetry.TryGetValue(
                    "PlayerCarClassPosition",
                    out value))
            {
                snapshot.PlayerClassPosition =
                    ToInt(value, 0);
            }

            if (telemetry.TryGetValue(
                    "PlayerCarClass",
                    out value))
            {
                snapshot.PlayerClassId =
                    ToInt(value, -1);
            }

            if (telemetry.TryGetValue(
                    "Lap",
                    out value))
            {
                snapshot.Lap =
                    ToInt(value, 0);
            }

            if (telemetry.TryGetValue(
                    "LapCompleted",
                    out value))
            {
                snapshot.LapCompleted =
                    ToInt(value, 0);
            }

            if (telemetry.TryGetValue(
                    "LapDistPct",
                    out value))
            {
                snapshot.LapDistancePercent =
                    ToFloat(value, 0.0f);
            }

            if (telemetry.TryGetValue(
                    "Speed",
                    out value))
            {
                snapshot.SpeedMetersPerSecond =
                    ToFloat(value, 0.0f);
            }

            if (telemetry.TryGetValue(
                    "Throttle",
                    out value))
            {
                snapshot.Throttle =
                    ToFloat(value, 0.0f);
            }

            if (telemetry.TryGetValue(
                    "Brake",
                    out value))
            {
                snapshot.Brake =
                    ToFloat(value, 0.0f);
            }

            if (telemetry.TryGetValue(
                    "Clutch",
                    out value))
            {
                snapshot.Clutch =
                    ToFloat(value, 0.0f);
            }

            if (telemetry.TryGetValue(
                    "Gear",
                    out value))
            {
                snapshot.Gear =
                    ToInt(value, 0);
            }

            if (telemetry.TryGetValue(
                    "RPM",
                    out value))
            {
                snapshot.Rpm =
                    ToFloat(value, 0.0f);
            }

            if (telemetry.TryGetValue(
                    "IsOnTrack",
                    out value))
            {
                snapshot.IsOnTrack =
                    ToBool(value, false);
            }

            if (telemetry.TryGetValue(
                    "OnPitRoad",
                    out value))
            {
                snapshot.IsOnPitRoad =
                    ToBool(value, false);
            }

            if (telemetry.TryGetValue(
                    "IsReplayPlaying",
                    out value))
            {
                snapshot.IsReplayPlaying =
                    ToBool(value, false);
            }

            if (telemetry.TryGetValue(
                    "TrackTemp",
                    out value))
            {
                snapshot.TrackTemperatureCelsius =
                    ToFloat(value, 0.0f);
            }

            if (telemetry.TryGetValue(
                    "AirTemp",
                    out value))
            {
                snapshot.AirTemperatureCelsius =
                    ToFloat(value, 0.0f);
            }

            if (telemetry.TryGetValue(
                    "FuelLevel",
                    out value))
            {
                snapshot.FuelLevelLiters =
                    ToDouble(value, 0.0);
            }

            if (telemetry.TryGetValue(
                    "FuelLevelPct",
                    out value))
            {
                double pct = ToDouble(value, 0.0);
                snapshot.FuelLevelPercent =
                    pct > 1.5 ? pct / 100.0 : pct;
            }

            object carIdxPositionValues = null;
            object carIdxClassPositionValues = null;

            telemetry.TryGetValue(
                "CarIdxPosition",
                out carIdxPositionValues);

            telemetry.TryGetValue(
                "CarIdxClassPosition",
                out carIdxClassPositionValues);

            UpdatePlayerPositions(
                snapshot,
                carIdxPositionValues,
                carIdxClassPositionValues);
        }

        private static void ReadNonGenericDictionary(
            IDictionary telemetry,
            TelemetrySnapshot snapshot)
        {
            snapshot.SessionTime =
                ToDouble(
                    GetDictionaryValue(
                        telemetry,
                        "SessionTime"),
                    0.0);

            snapshot.SessionTimeRemaining =
                ToDouble(
                    GetDictionaryValue(
                        telemetry,
                        "SessionTimeRemain"),
                    0.0);

            snapshot.SessionNumber =
                ToInt(GetDictionaryValue(telemetry, "SessionNum"), -1);

            snapshot.SessionState =
                ToInt(GetDictionaryValue(telemetry, "SessionState"), 0);

            snapshot.SessionFlags =
                ToLong(GetDictionaryValue(telemetry, "SessionFlags"), 0L);

            snapshot.SessionLapsRemaining =
                ToInt(GetDictionaryValue(telemetry, "SessionLapsRemain"), 0);

            snapshot.SessionLapsTotal =
                ToInt(GetDictionaryValue(telemetry, "SessionLapsTotal"), 0);

            snapshot.PlayerCarIndex =
                ToInt(
                    GetDictionaryValue(
                        telemetry,
                        "PlayerCarIdx"),
                    -1);

            snapshot.PlayerPosition =
                ToInt(
                    GetDictionaryValue(
                        telemetry,
                        "PlayerCarPosition"),
                    0);

            snapshot.PlayerClassPosition =
                ToInt(
                    GetDictionaryValue(
                        telemetry,
                        "PlayerCarClassPosition"),
                    0);

            snapshot.PlayerClassId =
                ToInt(
                    GetDictionaryValue(
                        telemetry,
                        "PlayerCarClass"),
                    -1);

            snapshot.Lap =
                ToInt(
                    GetDictionaryValue(
                        telemetry,
                        "Lap"),
                    0);

            snapshot.LapCompleted =
                ToInt(
                    GetDictionaryValue(
                        telemetry,
                        "LapCompleted"),
                    0);

            snapshot.LapDistancePercent =
                ToFloat(
                    GetDictionaryValue(
                        telemetry,
                        "LapDistPct"),
                    0.0f);

            snapshot.SpeedMetersPerSecond =
                ToFloat(
                    GetDictionaryValue(
                        telemetry,
                        "Speed"),
                    0.0f);

            snapshot.Throttle =
                ToFloat(
                    GetDictionaryValue(
                        telemetry,
                        "Throttle"),
                    0.0f);

            snapshot.Brake =
                ToFloat(
                    GetDictionaryValue(
                        telemetry,
                        "Brake"),
                    0.0f);

            snapshot.Clutch =
                ToFloat(
                    GetDictionaryValue(
                        telemetry,
                        "Clutch"),
                    0.0f);

            snapshot.Gear =
                ToInt(
                    GetDictionaryValue(
                        telemetry,
                        "Gear"),
                    0);

            snapshot.Rpm =
                ToFloat(
                    GetDictionaryValue(
                        telemetry,
                        "RPM"),
                    0.0f);

            snapshot.IsOnTrack =
                ToBool(
                    GetDictionaryValue(
                        telemetry,
                        "IsOnTrack"),
                    false);

            snapshot.IsOnPitRoad =
                ToBool(
                    GetDictionaryValue(
                        telemetry,
                        "OnPitRoad"),
                    false);

            snapshot.IsReplayPlaying =
                ToBool(
                    GetDictionaryValue(
                        telemetry,
                        "IsReplayPlaying"),
                    false);

            snapshot.TrackTemperatureCelsius =
                ToFloat(
                    GetDictionaryValue(
                        telemetry,
                        "TrackTemp"),
                    0.0f);

            snapshot.AirTemperatureCelsius =
                ToFloat(
                    GetDictionaryValue(
                        telemetry,
                        "AirTemp"),
                    0.0f);

            snapshot.FuelLevelLiters =
                ToDouble(
                    GetDictionaryValue(
                        telemetry,
                        "FuelLevel"),
                    0.0);

            double fuelPct =
                ToDouble(
                    GetDictionaryValue(
                        telemetry,
                        "FuelLevelPct"),
                    0.0);

            snapshot.FuelLevelPercent =
                fuelPct > 1.5 ? fuelPct / 100.0 : fuelPct;

            UpdatePlayerPositions(
                snapshot,
                GetDictionaryValue(
                    telemetry,
                    "CarIdxPosition"),
                GetDictionaryValue(
                    telemetry,
                    "CarIdxClassPosition"));
        }

        private void ReadEnumerableFallback(
            object telemetryObject,
            TelemetrySnapshot snapshot)
        {
            IEnumerable telemetryCollection =
                telemetryObject as IEnumerable;

            if (telemetryCollection == null)
            {
                return;
            }

            object carIdxPositionValues = null;
            object carIdxClassPositionValues = null;

            foreach (object item in telemetryCollection)
            {
                if (item == null)
                {
                    continue;
                }

                EnsureTelemetryItemAccessors(
                    item.GetType());

                string key =
                    GetValueSafely(
                        telemetryKeyGetter,
                        item) as string;

                if (key == null)
                {
                    continue;
                }

                object value =
                    GetValueSafely(
                        telemetryValueGetter,
                        item);

                switch (key)
                {
                    case "SessionTime":
                        snapshot.SessionTime =
                            ToDouble(value, 0.0);
                        break;

                    case "SessionTimeRemain":
                        snapshot.SessionTimeRemaining =
                            ToDouble(value, 0.0);
                        break;

                    case "SessionNum":
                        snapshot.SessionNumber = ToInt(value, -1);
                        break;

                    case "SessionState":
                        snapshot.SessionState = ToInt(value, 0);
                        break;

                    case "SessionFlags":
                        snapshot.SessionFlags = ToLong(value, 0L);
                        break;

                    case "SessionLapsRemain":
                        snapshot.SessionLapsRemaining = ToInt(value, 0);
                        break;

                    case "SessionLapsTotal":
                        snapshot.SessionLapsTotal = ToInt(value, 0);
                        break;

                    case "PlayerCarIdx":
                        snapshot.PlayerCarIndex =
                            ToInt(value, -1);
                        break;

                    case "PlayerCarPosition":
                        snapshot.PlayerPosition =
                            ToInt(value, 0);
                        break;

                    case "PlayerCarClassPosition":
                        snapshot.PlayerClassPosition =
                            ToInt(value, 0);
                        break;

                    case "PlayerCarClass":
                        snapshot.PlayerClassId =
                            ToInt(value, -1);
                        break;

                    case "CarIdxPosition":
                        carIdxPositionValues =
                            value;
                        break;

                    case "CarIdxClassPosition":
                        carIdxClassPositionValues =
                            value;
                        break;

                    case "Lap":
                        snapshot.Lap =
                            ToInt(value, 0);
                        break;

                    case "LapCompleted":
                        snapshot.LapCompleted =
                            ToInt(value, 0);
                        break;

                    case "LapDistPct":
                        snapshot.LapDistancePercent =
                            ToFloat(value, 0.0f);
                        break;

                    case "Speed":
                        snapshot.SpeedMetersPerSecond =
                            ToFloat(value, 0.0f);
                        break;

                    case "Throttle":
                        snapshot.Throttle =
                            ToFloat(value, 0.0f);
                        break;

                    case "Brake":
                        snapshot.Brake =
                            ToFloat(value, 0.0f);
                        break;

                    case "Clutch":
                        snapshot.Clutch =
                            ToFloat(value, 0.0f);
                        break;

                    case "Gear":
                        snapshot.Gear =
                            ToInt(value, 0);
                        break;

                    case "RPM":
                        snapshot.Rpm =
                            ToFloat(value, 0.0f);
                        break;

                    case "IsOnTrack":
                        snapshot.IsOnTrack =
                            ToBool(value, false);
                        break;

                    case "OnPitRoad":
                        snapshot.IsOnPitRoad =
                            ToBool(value, false);
                        break;

                    case "IsReplayPlaying":
                        snapshot.IsReplayPlaying =
                            ToBool(value, false);
                        break;

                    case "TrackTemp":
                        snapshot.TrackTemperatureCelsius =
                            ToFloat(value, 0.0f);
                        break;

                    case "AirTemp":
                        snapshot.AirTemperatureCelsius =
                            ToFloat(value, 0.0f);
                        break;

                    case "FuelLevel":
                        snapshot.FuelLevelLiters =
                            ToDouble(value, 0.0);
                        break;

                    case "FuelLevelPct":
                        double pct = ToDouble(value, 0.0);
                        snapshot.FuelLevelPercent =
                            pct > 1.5 ? pct / 100.0 : pct;
                        break;
                }
            }

            UpdatePlayerPositions(
                snapshot,
                carIdxPositionValues,
                carIdxClassPositionValues);
        }

        private string ReadSessionType(
            object rawData)
        {
            object sessionInfo =
                GetValueSafely(
                    currentSessionInfoGetter,
                    rawData);

            if (sessionInfo == null)
            {
                return string.Empty;
            }

            EnsureSessionInfoAccessor(
                sessionInfo.GetType());

            object value =
                GetValueSafely(
                    sessionTypeGetter,
                    sessionInfo);

            return value == null
                ? string.Empty
                : Convert.ToString(
                    value,
                    CultureInfo.InvariantCulture)
                  ?? string.Empty;
        }

        private void EnsureRawDataAccessors(
            Type rawDataType)
        {
            if (rawDataType == cachedRawDataType)
            {
                return;
            }

            cachedRawDataType =
                rawDataType;

            telemetryGetter =
                CreatePropertyGetter(
                    rawDataType,
                    "Telemetry");

            currentSessionInfoGetter =
                CreatePropertyGetter(
                    rawDataType,
                    "CurrentSessionInfo");
        }

        private void EnsureTelemetryItemAccessors(
            Type itemType)
        {
            if (itemType == cachedTelemetryItemType)
            {
                return;
            }

            cachedTelemetryItemType =
                itemType;

            telemetryKeyGetter =
                CreatePropertyGetter(
                    itemType,
                    "Key");

            telemetryValueGetter =
                CreatePropertyGetter(
                    itemType,
                    "Value");
        }

        private void EnsureSessionInfoAccessor(
            Type sessionInfoType)
        {
            if (sessionInfoType == cachedSessionInfoType)
            {
                return;
            }

            cachedSessionInfoType =
                sessionInfoType;

            sessionTypeGetter =
                CreatePropertyGetter(
                    sessionInfoType,
                    "SessionType");
        }

        private static Func<object, object>
            CreatePropertyGetter(
                Type declaringType,
                string propertyName)
        {
            PropertyInfo property =
                declaringType.GetProperty(
                    propertyName,
                    BindingFlags.Public |
                    BindingFlags.Instance);

            if (property == null)
            {
                return null;
            }

            try
            {
                ParameterExpression targetParameter =
                    Expression.Parameter(
                        typeof(object),
                        "target");

                UnaryExpression convertedTarget =
                    Expression.Convert(
                        targetParameter,
                        declaringType);

                MemberExpression propertyAccess =
                    Expression.Property(
                        convertedTarget,
                        property);

                UnaryExpression convertedResult =
                    Expression.Convert(
                        propertyAccess,
                        typeof(object));

                return Expression
                    .Lambda<Func<object, object>>(
                        convertedResult,
                        targetParameter)
                    .Compile();
            }
            catch
            {
                return null;
            }
        }

        private static object GetValueSafely(
            Func<object, object> getter,
            object target)
        {
            if (getter == null ||
                target == null)
            {
                return null;
            }

            try
            {
                return getter(target);
            }
            catch
            {
                return null;
            }
        }

        private static object GetDictionaryValue(
            IDictionary dictionary,
            string key)
        {
            try
            {
                return dictionary.Contains(key)
                    ? dictionary[key]
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private static void UpdatePlayerPositions(
            TelemetrySnapshot snapshot,
            object carIdxPositionValues,
            object carIdxClassPositionValues)
        {
            int playerCarIndex =
                snapshot.PlayerCarIndex;

            if (playerCarIndex < 0)
            {
                return;
            }

            snapshot.PlayerPosition =
                GetIndexedInt(
                    carIdxPositionValues,
                    playerCarIndex,
                    snapshot.PlayerPosition);

            snapshot.PlayerClassPosition =
                GetIndexedInt(
                    carIdxClassPositionValues,
                    playerCarIndex,
                    snapshot.PlayerClassPosition);
        }

        private static int GetIndexedInt(
            object collection,
            int index,
            int defaultValue)
        {
            if (collection == null ||
                index < 0)
            {
                return defaultValue;
            }

            Array array =
                collection as Array;

            if (array != null)
            {
                if (index >= array.Length)
                {
                    return defaultValue;
                }

                return ToInt(
                    array.GetValue(index),
                    defaultValue);
            }

            IList list =
                collection as IList;

            if (list != null)
            {
                if (index >= list.Count)
                {
                    return defaultValue;
                }

                return ToInt(
                    list[index],
                    defaultValue);
            }

            return defaultValue;
        }

        private static int ToInt(
            object value,
            int defaultValue)
        {
            if (value == null)
            {
                return defaultValue;
            }

            if (value is int)
            {
                return (int)value;
            }

            if (value is short)
            {
                return (short)value;
            }

            if (value is byte)
            {
                return (byte)value;
            }

            if (value is long)
            {
                long longValue =
                    (long)value;

                if (longValue > int.MaxValue ||
                    longValue < int.MinValue)
                {
                    return defaultValue;
                }

                return (int)longValue;
            }

            try
            {
                return Convert.ToInt32(
                    value,
                    CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        private static float ToFloat(
            object value,
            float defaultValue)
        {
            if (value == null)
            {
                return defaultValue;
            }

            if (value is float)
            {
                return (float)value;
            }

            if (value is double)
            {
                return (float)(double)value;
            }

            if (value is int)
            {
                return (int)value;
            }

            try
            {
                return Convert.ToSingle(
                    value,
                    CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        private static double ToDouble(
            object value,
            double defaultValue)
        {
            if (value == null)
            {
                return defaultValue;
            }

            if (value is double)
            {
                return (double)value;
            }

            if (value is float)
            {
                return (float)value;
            }

            if (value is int)
            {
                return (int)value;
            }

            try
            {
                return Convert.ToDouble(
                    value,
                    CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        private static long ToLong(
            object value,
            long defaultValue)
        {
            if (value == null)
            {
                return defaultValue;
            }

            try
            {
                return Convert.ToInt64(
                    value,
                    CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        private static bool ToBool(
            object value,
            bool defaultValue)
        {
            if (value == null)
            {
                return defaultValue;
            }

            if (value is bool)
            {
                return (bool)value;
            }

            if (value is int)
            {
                return (int)value != 0;
            }

            try
            {
                return Convert.ToBoolean(
                    value,
                    CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        private static void ResetSnapshot(
            TelemetrySnapshot snapshot,
            bool gameRunning,
            string gameName)
        {
            snapshot.CapturedAt =
                DateTime.UtcNow;

            snapshot.GameRunning =
                gameRunning;

            snapshot.GameName =
                gameName ?? string.Empty;

            snapshot.SessionType =
                string.Empty;

            snapshot.SessionTime =
                0.0;

            snapshot.SessionTimeRemaining =
                0.0;

            snapshot.SessionNumber =
                -1;

            snapshot.SessionState =
                0;

            snapshot.SessionFlags =
                0L;

            snapshot.SessionLapsRemaining =
                0;

            snapshot.SessionLapsTotal =
                0;

            snapshot.PlayerCarIndex =
                -1;

            snapshot.PlayerPosition =
                0;

            snapshot.PlayerClassPosition =
                0;

            snapshot.PlayerClassId =
                -1;

            snapshot.Lap =
                0;

            snapshot.LapCompleted =
                0;

            snapshot.LapDistancePercent =
                0.0f;

            snapshot.SpeedMetersPerSecond =
                0.0f;

            snapshot.Throttle =
                0.0f;

            snapshot.Brake =
                0.0f;

            snapshot.Clutch =
                0.0f;

            snapshot.Gear =
                0;

            snapshot.Rpm =
                0.0f;

            snapshot.IsOnTrack =
                false;

            snapshot.IsOnPitRoad =
                false;

            snapshot.IsReplayPlaying =
                false;

            snapshot.TrackTemperatureCelsius =
                0.0f;

            snapshot.AirTemperatureCelsius =
                0.0f;

            snapshot.FuelLevelLiters =
                0.0;

            snapshot.FuelLevelPercent =
                0.0;
        }
    }
}