using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Fulcrum.Core.Relative
{
    /// <summary>
    /// Reads iRacing participant telemetry arrays into a reusable
    /// ParticipantBuffer without allocating objects during updates.
    /// </summary>
    public sealed class ParticipantTelemetryReader
    {
        private Type cachedRawDataType;
        private Func<object, object> telemetryGetter;

        public bool IsUsingDirectLookup
        {
            get;
            private set;
        }

        public void Update(
            object rawData,
            int playerCarIndex,
            ParticipantBuffer participantBuffer)
        {
            if (participantBuffer == null)
            {
                throw new ArgumentNullException(
                    nameof(participantBuffer));
            }

            participantBuffer.Reset();

            IsUsingDirectLookup = false;

            if (rawData == null)
            {
                return;
            }

            EnsureRawDataAccessor(
                rawData.GetType());

            object telemetryObject =
                GetValueSafely(
                    telemetryGetter,
                    rawData);

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
                    playerCarIndex,
                    participantBuffer);

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
                    playerCarIndex,
                    participantBuffer);

                return;
            }

            IDictionary nonGenericDictionary =
                telemetryObject as IDictionary;

            if (nonGenericDictionary != null)
            {
                IsUsingDirectLookup = true;

                ReadNonGenericDictionary(
                    nonGenericDictionary,
                    playerCarIndex,
                    participantBuffer);
            }
        }

        public void Reset(
            ParticipantBuffer participantBuffer)
        {
            IsUsingDirectLookup = false;

            if (participantBuffer != null)
            {
                participantBuffer.Reset();
            }
        }

        private static void ReadGenericDictionary(
            IDictionary<string, object> telemetry,
            int playerCarIndex,
            ParticipantBuffer participantBuffer)
        {
            object carIdxLap;
            object carIdxLapCompleted;
            object carIdxLapDistPct;
            object carIdxPosition;
            object carIdxClassPosition;
            object carIdxClass;
            object carIdxTrackSurface;
            object carIdxOnPitRoad;
            object carIdxRpm;
            object carIdxGear;
            object carIdxF2Time;
            object carIdxEstTime;
            object carIdxLastLapTime;
            object carIdxBestLapTime;
            object carIdxTireCompound;
            object carIdxSessionFlags;
            object carIdxP2PCount;
            object carIdxP2PStatus;
            object playerP2PCount;
            object playerPushToPass;

            telemetry.TryGetValue(
                "CarIdxLap",
                out carIdxLap);

            telemetry.TryGetValue(
                "CarIdxLapCompleted",
                out carIdxLapCompleted);

            telemetry.TryGetValue(
                "CarIdxLapDistPct",
                out carIdxLapDistPct);

            telemetry.TryGetValue(
                "CarIdxPosition",
                out carIdxPosition);

            telemetry.TryGetValue(
                "CarIdxClassPosition",
                out carIdxClassPosition);

            telemetry.TryGetValue(
                "CarIdxClass",
                out carIdxClass);

            telemetry.TryGetValue(
                "CarIdxTrackSurface",
                out carIdxTrackSurface);

            telemetry.TryGetValue(
                "CarIdxOnPitRoad",
                out carIdxOnPitRoad);

            telemetry.TryGetValue(
                "CarIdxRPM",
                out carIdxRpm);

            telemetry.TryGetValue(
                "CarIdxGear",
                out carIdxGear);

            telemetry.TryGetValue(
                "CarIdxF2Time",
                out carIdxF2Time);

            telemetry.TryGetValue(
                "CarIdxEstTime",
                out carIdxEstTime);

            telemetry.TryGetValue(
                "CarIdxLastLapTime",
                out carIdxLastLapTime);

            telemetry.TryGetValue(
                "CarIdxBestLapTime",
                out carIdxBestLapTime);

            telemetry.TryGetValue(
                "CarIdxTireCompound",
                out carIdxTireCompound);

            telemetry.TryGetValue(
                "CarIdxSessionFlags",
                out carIdxSessionFlags);

            telemetry.TryGetValue(
                "CarIdxP2P_Count",
                out carIdxP2PCount);

            telemetry.TryGetValue(
                "CarIdxP2P_Status",
                out carIdxP2PStatus);

            telemetry.TryGetValue(
                "P2P_Count",
                out playerP2PCount);

            telemetry.TryGetValue(
                "PushToPass",
                out playerPushToPass);

            PopulateBuffer(
                participantBuffer,
                playerCarIndex,
                carIdxLap,
                carIdxLapCompleted,
                carIdxLapDistPct,
                carIdxPosition,
                carIdxClassPosition,
                carIdxClass,
                carIdxTrackSurface,
                carIdxOnPitRoad,
                carIdxRpm,
                carIdxGear,
                carIdxF2Time,
                carIdxEstTime,
                carIdxLastLapTime,
                carIdxBestLapTime,
                carIdxTireCompound,
                carIdxSessionFlags,
                carIdxP2PCount,
                carIdxP2PStatus,
                playerP2PCount,
                playerPushToPass);
        }

        private static void ReadReadOnlyDictionary(
            IReadOnlyDictionary<string, object> telemetry,
            int playerCarIndex,
            ParticipantBuffer participantBuffer)
        {
            object carIdxLap;
            object carIdxLapCompleted;
            object carIdxLapDistPct;
            object carIdxPosition;
            object carIdxClassPosition;
            object carIdxClass;
            object carIdxTrackSurface;
            object carIdxOnPitRoad;
            object carIdxRpm;
            object carIdxGear;
            object carIdxF2Time;
            object carIdxEstTime;
            object carIdxLastLapTime;
            object carIdxBestLapTime;
            object carIdxTireCompound;
            object carIdxSessionFlags;
            object carIdxP2PCount;
            object carIdxP2PStatus;
            object playerP2PCount;
            object playerPushToPass;

            telemetry.TryGetValue(
                "CarIdxLap",
                out carIdxLap);

            telemetry.TryGetValue(
                "CarIdxLapCompleted",
                out carIdxLapCompleted);

            telemetry.TryGetValue(
                "CarIdxLapDistPct",
                out carIdxLapDistPct);

            telemetry.TryGetValue(
                "CarIdxPosition",
                out carIdxPosition);

            telemetry.TryGetValue(
                "CarIdxClassPosition",
                out carIdxClassPosition);

            telemetry.TryGetValue(
                "CarIdxClass",
                out carIdxClass);

            telemetry.TryGetValue(
                "CarIdxTrackSurface",
                out carIdxTrackSurface);

            telemetry.TryGetValue(
                "CarIdxOnPitRoad",
                out carIdxOnPitRoad);

            telemetry.TryGetValue(
                "CarIdxRPM",
                out carIdxRpm);

            telemetry.TryGetValue(
                "CarIdxGear",
                out carIdxGear);

            telemetry.TryGetValue(
                "CarIdxF2Time",
                out carIdxF2Time);

            telemetry.TryGetValue(
                "CarIdxEstTime",
                out carIdxEstTime);

            telemetry.TryGetValue(
                "CarIdxLastLapTime",
                out carIdxLastLapTime);

            telemetry.TryGetValue(
                "CarIdxBestLapTime",
                out carIdxBestLapTime);

            telemetry.TryGetValue(
                "CarIdxTireCompound",
                out carIdxTireCompound);

            telemetry.TryGetValue(
                "CarIdxSessionFlags",
                out carIdxSessionFlags);

            telemetry.TryGetValue(
                "CarIdxP2P_Count",
                out carIdxP2PCount);

            telemetry.TryGetValue(
                "CarIdxP2P_Status",
                out carIdxP2PStatus);

            telemetry.TryGetValue(
                "P2P_Count",
                out playerP2PCount);

            telemetry.TryGetValue(
                "PushToPass",
                out playerPushToPass);

            PopulateBuffer(
                participantBuffer,
                playerCarIndex,
                carIdxLap,
                carIdxLapCompleted,
                carIdxLapDistPct,
                carIdxPosition,
                carIdxClassPosition,
                carIdxClass,
                carIdxTrackSurface,
                carIdxOnPitRoad,
                carIdxRpm,
                carIdxGear,
                carIdxF2Time,
                carIdxEstTime,
                carIdxLastLapTime,
                carIdxBestLapTime,
                carIdxTireCompound,
                carIdxSessionFlags,
                carIdxP2PCount,
                carIdxP2PStatus,
                playerP2PCount,
                playerPushToPass);
        }

        private static void ReadNonGenericDictionary(
            IDictionary telemetry,
            int playerCarIndex,
            ParticipantBuffer participantBuffer)
        {
            PopulateBuffer(
                participantBuffer,
                playerCarIndex,
                GetDictionaryValue(
                    telemetry,
                    "CarIdxLap"),
                GetDictionaryValue(
                    telemetry,
                    "CarIdxLapCompleted"),
                GetDictionaryValue(
                    telemetry,
                    "CarIdxLapDistPct"),
                GetDictionaryValue(
                    telemetry,
                    "CarIdxPosition"),
                GetDictionaryValue(
                    telemetry,
                    "CarIdxClassPosition"),
                GetDictionaryValue(
                    telemetry,
                    "CarIdxClass"),
                GetDictionaryValue(
                    telemetry,
                    "CarIdxTrackSurface"),
                GetDictionaryValue(
                    telemetry,
                    "CarIdxOnPitRoad"),
                GetDictionaryValue(
                    telemetry,
                    "CarIdxRPM"),
                GetDictionaryValue(
                    telemetry,
                    "CarIdxGear"),
                GetDictionaryValue(
                    telemetry,
                    "CarIdxF2Time"),
                GetDictionaryValue(
                    telemetry,
                    "CarIdxEstTime"),
                GetDictionaryValue(
                    telemetry,
                    "CarIdxLastLapTime"),
                GetDictionaryValue(
                    telemetry,
                    "CarIdxBestLapTime"),
                GetDictionaryValue(
                    telemetry,
                    "CarIdxTireCompound"),
                GetDictionaryValue(
                    telemetry,
                    "CarIdxSessionFlags"),
                GetDictionaryValue(
                    telemetry,
                    "CarIdxP2P_Count"),
                GetDictionaryValue(
                    telemetry,
                    "CarIdxP2P_Status"),
                GetDictionaryValue(
                    telemetry,
                    "P2P_Count"),
                GetDictionaryValue(
                    telemetry,
                    "PushToPass"));
        }

        private static void PopulateBuffer(
            ParticipantBuffer participantBuffer,
            int playerCarIndex,
            object carIdxLap,
            object carIdxLapCompleted,
            object carIdxLapDistPct,
            object carIdxPosition,
            object carIdxClassPosition,
            object carIdxClass,
            object carIdxTrackSurface,
            object carIdxOnPitRoad,
            object carIdxRpm,
            object carIdxGear,
            object carIdxF2Time,
            object carIdxEstTime,
            object carIdxLastLapTime,
            object carIdxBestLapTime,
            object carIdxTireCompound,
            object carIdxSessionFlags,
            object carIdxP2PCount,
            object carIdxP2PStatus,
            object playerP2PCount,
            object playerPushToPass)
        {
            int capacity =
                participantBuffer.Capacity;

            for (int carIndex = 0;
                 carIndex < capacity;
                 carIndex++)
            {
                ParticipantSnapshot participant =
                    participantBuffer[carIndex];

                int lap =
                    GetIndexedInt(
                        carIdxLap,
                        carIndex,
                        -1);

                int lapCompleted =
                    GetIndexedInt(
                        carIdxLapCompleted,
                        carIndex,
                        -1);

                float lapDistancePercent =
                    GetIndexedFloat(
                        carIdxLapDistPct,
                        carIndex,
                        -1.0f);

                int overallPosition =
                    GetIndexedInt(
                        carIdxPosition,
                        carIndex,
                        0);

                int classPosition =
                    GetIndexedInt(
                        carIdxClassPosition,
                        carIndex,
                        0);

                int classId =
                    GetIndexedInt(
                        carIdxClass,
                        carIndex,
                        -1);

                int trackSurface =
                    GetIndexedInt(
                        carIdxTrackSurface,
                        carIndex,
                        -1);

                bool isPlayer =
                    carIndex == playerCarIndex;

                bool isInWorld =
                    trackSurface >= 0;

                bool hasValidTrackPosition =
                    lapDistancePercent >= 0.0f &&
                    lapDistancePercent <= 1.0f;

                participant.Lap =
                    lap;

                participant.LapCompleted =
                    lapCompleted;

                participant.LapDistancePercent =
                    lapDistancePercent;

                participant.OverallPosition =
                    overallPosition;

                participant.ClassPosition =
                    classPosition;

                participant.ClassId =
                    classId;

                participant.TrackSurface =
                    trackSurface;

                participant.IsOnPitRoad =
                    GetIndexedBool(
                        carIdxOnPitRoad,
                        carIndex,
                        false);

                participant.Rpm =
                    GetIndexedFloat(
                        carIdxRpm,
                        carIndex,
                        0.0f);

                participant.Gear =
                    GetIndexedInt(
                        carIdxGear,
                        carIndex,
                        0);

                participant.F2Time =
                    GetIndexedFloat(
                        carIdxF2Time,
                        carIndex,
                        0.0f);

                participant.EstimatedTime =
                    GetIndexedFloat(
                        carIdxEstTime,
                        carIndex,
                        0.0f);

                participant.LastLapTime =
                    GetIndexedFloat(
                        carIdxLastLapTime,
                        carIndex,
                        0.0f);

                participant.BestLapTime =
                    GetIndexedFloat(
                        carIdxBestLapTime,
                        carIndex,
                        0.0f);

                participant.TireCompound =
                    GetIndexedInt(
                        carIdxTireCompound,
                        carIndex,
                        -1);

                bool hasPerCarP2P =
                    carIdxP2PCount != null ||
                    carIdxP2PStatus != null;

                int rawP2PCount =
                    GetIndexedInt(
                        carIdxP2PCount,
                        carIndex,
                        0);

                int rawP2PStatus =
                    GetIndexedInt(
                        carIdxP2PStatus,
                        carIndex,
                        0);

                // iRacing exposes a direct player counter/status as well.
                // Prefer those for the local car when available; this is
                // especially useful for Super Formula where opponent count
                // encoding differs from the player's scalar value.
                if (isPlayer && playerP2PCount != null)
                {
                    rawP2PCount = GetScalarInt(playerP2PCount, rawP2PCount);
                }

                if (isPlayer && playerPushToPass != null)
                {
                    rawP2PStatus = GetScalarBool(playerPushToPass, rawP2PStatus > 0) ? 1 : 0;
                }

                participant.RawPushToPassCount = rawP2PCount;
                participant.RawPushToPassStatus = rawP2PStatus;
                participant.HasPushToPassTelemetry =
                    hasPerCarP2P ||
                    (isPlayer && (playerP2PCount != null || playerPushToPass != null));

                participant.SessionFlags =
                    GetIndexedLong(
                        carIdxSessionFlags,
                        carIndex,
                        0L);

                participant.IsPlayer =
                    isPlayer;

                /*
                 * A participant is eligible for the live Relative when:
                 *
                 * 1. iRacing reports the car as present in the world.
                 * 2. The car has a usable normalized track position.
                 *
                 * The player is retained during short telemetry
                 * transitions so the Relative does not briefly lose
                 * its reference car.
                 */
                bool hasRaceIdentity =
                    overallPosition > 0 ||
                    classPosition > 0 ||
                    lap >= 0;

                participant.IsValid =
                    (hasValidTrackPosition &&
                     (isInWorld || hasRaceIdentity)) ||
                    isPlayer;
            }

            participantBuffer.RefreshValidParticipantCount();
        }

        private static int GetScalarInt(object value, int fallback)
        {
            if (value == null) return fallback;
            try { return Convert.ToInt32(value); }
            catch { return fallback; }
        }

        private static bool GetScalarBool(object value, bool fallback)
        {
            if (value == null) return fallback;
            try { return Convert.ToBoolean(value); }
            catch { return fallback; }
        }

        private void EnsureRawDataAccessor(
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

        private static int GetIndexedInt(
            object collection,
            int index,
            int defaultValue)
        {
            object value =
                GetIndexedValue(
                    collection,
                    index);

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

            if (value is sbyte)
            {
                return (sbyte)value;
            }

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return defaultValue;
            }
        }

        private static bool GetIndexedBool(
            object collection,
            int index,
            bool defaultValue)
        {
            object value =
                GetIndexedValue(
                    collection,
                    index);

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
                return Convert.ToBoolean(value);
            }
            catch
            {
                return defaultValue;
            }
        }

        private static long GetIndexedLong(
            object source,
            int index,
            long fallback)
        {
            object value = GetIndexedValue(source, index);
            if (value == null) return fallback;

            try
            {
                return Convert.ToInt64(value);
            }
            catch
            {
                return fallback;
            }
        }

        private static float GetIndexedFloat(
            object collection,
            int index,
            float defaultValue)
        {
            object value =
                GetIndexedValue(
                    collection,
                    index);

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
                return Convert.ToSingle(value);
            }
            catch
            {
                return defaultValue;
            }
        }

        private static object GetIndexedValue(
            object collection,
            int index)
        {
            if (collection == null ||
                index < 0)
            {
                return null;
            }

            Array array =
                collection as Array;

            if (array != null)
            {
                if (index >= array.Length)
                {
                    return null;
                }

                return array.GetValue(index);
            }

            IList list =
                collection as IList;

            if (list != null)
            {
                if (index >= list.Count)
                {
                    return null;
                }

                return list[index];
            }

            return null;
        }
    }
}
