using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Fulcrum.Core.Damage
{
    public sealed class DamageTelemetryReader
    {
        private const long BlackFlagMask = 0x00010000L;
        private const long DisqualifyFlagMask = 0x00020000L;
        private const long RepairFlagMask = 0x00100000L;

        private Type cachedRawType;
        private PropertyInfo telemetryProperty;

        public bool TryRead(object rawData, DamageTelemetry result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            Clear(result);
            if (rawData == null) return false;

            EnsureProperties(rawData.GetType());
            object telemetry = GetValue(telemetryProperty, rawData);
            if (telemetry == null) return false;

            bool found = false;
            double doubleValue;
            int intValue;
            long longValue;
            bool boolValue;
            StringBuilder foundKeys = new StringBuilder(192);

            result.RequiredRepairTelemetryFound = ReadDouble(telemetry, "PitRepairLeft", out doubleValue);
            if (result.RequiredRepairTelemetryFound)
            {
                result.RequiredRepairSeconds = doubleValue;
                AddKey(foundKeys, "PitRepairLeft");
                found = true;
            }

            result.OptionalRepairTelemetryFound = ReadDouble(telemetry, "PitOptRepairLeft", out doubleValue);
            if (result.OptionalRepairTelemetryFound)
            {
                result.OptionalRepairSeconds = doubleValue;
                AddKey(foundKeys, "PitOptRepairLeft");
                found = true;
            }

            result.TowTelemetryFound = ReadDouble(telemetry, "PlayerCarTowTime", out doubleValue);
            if (result.TowTelemetryFound)
            {
                result.TowTimeSeconds = doubleValue;
                AddKey(foundKeys, "PlayerCarTowTime");
                found = true;
            }

            if (ReadBool(telemetry, "PlayerCarInPitStall", out boolValue))
            {
                result.IsInPitStall = boolValue;
                AddKey(foundKeys, "PlayerCarInPitStall");
                found = true;
            }

            if (ReadInt(telemetry, "PlayerCarPitSvStatus", out intValue))
            {
                result.PitServiceStatus = intValue;
                AddKey(foundKeys, "PlayerCarPitSvStatus");
                found = true;
            }

            if (ReadBool(telemetry, "FastRepairAvailable", out boolValue))
            {
                result.FastRepairAvailable = boolValue;
                AddKey(foundKeys, "FastRepairAvailable");
                found = true;
            }

            if (ReadBool(telemetry, "FastRepairUsed", out boolValue))
            {
                result.FastRepairUsed = boolValue;
                AddKey(foundKeys, "FastRepairUsed");
                found = true;
            }

            if (ReadInt(telemetry, "PlayerFastRepairsUsed", out intValue))
            {
                result.FastRepairsUsed = intValue;
                AddKey(foundKeys, "PlayerFastRepairsUsed");
                found = true;
            }

            if (ReadInt(telemetry, "PlayerCarDriverIncidentCount", out intValue))
            {
                result.DriverIncidentCount = intValue;
                AddKey(foundKeys, "PlayerCarDriverIncidentCount");
                found = true;
            }

            if (ReadInt(telemetry, "PlayerCarMyIncidentCount", out intValue))
            {
                result.MyIncidentCount = intValue;
                AddKey(foundKeys, "PlayerCarMyIncidentCount");
                found = true;
            }

            if (ReadInt(telemetry, "PlayerCarTeamIncidentCount", out intValue))
            {
                result.TeamIncidentCount = intValue;
                AddKey(foundKeys, "PlayerCarTeamIncidentCount");
                found = true;
            }

            result.SessionFlagsTelemetryFound = ReadLong(telemetry, "SessionFlags", out longValue);
            if (result.SessionFlagsTelemetryFound)
            {
                result.SessionFlagsRaw = longValue;
                result.HasRepairFlag = (longValue & RepairFlagMask) != 0;
                result.HasBlackFlag = (longValue & BlackFlagMask) != 0;
                result.HasDisqualifyFlag = (longValue & DisqualifyFlagMask) != 0;
                AddKey(foundKeys, "SessionFlags");
                found = true;
            }

            result.RequiredRepairSeconds = Math.Max(0.0, result.RequiredRepairSeconds);
            result.OptionalRepairSeconds = Math.Max(0.0, result.OptionalRepairSeconds);
            result.TowTimeSeconds = Math.Max(0.0, result.TowTimeSeconds);
            result.AvailableTelemetryKeys = foundKeys.ToString();
            result.Available = found;
            return found;
        }

        private void EnsureProperties(Type rawType)
        {
            if (rawType == cachedRawType) return;
            cachedRawType = rawType;
            telemetryProperty = rawType.GetProperty("Telemetry", BindingFlags.Instance | BindingFlags.Public);
        }

        private static void Clear(DamageTelemetry value)
        {
            value.Available = false;
            value.RequiredRepairSeconds = 0.0;
            value.OptionalRepairSeconds = 0.0;
            value.TowTimeSeconds = 0.0;
            value.IsInPitStall = false;
            value.PitServiceStatus = 0;
            value.FastRepairAvailable = false;
            value.FastRepairUsed = false;
            value.FastRepairsUsed = 0;
            value.DriverIncidentCount = 0;
            value.MyIncidentCount = 0;
            value.TeamIncidentCount = 0;
            value.SessionFlagsRaw = 0L;
            value.HasRepairFlag = false;
            value.HasBlackFlag = false;
            value.HasDisqualifyFlag = false;
            value.RequiredRepairTelemetryFound = false;
            value.OptionalRepairTelemetryFound = false;
            value.TowTelemetryFound = false;
            value.SessionFlagsTelemetryFound = false;
            value.AvailableTelemetryKeys = string.Empty;
        }

        private static void AddKey(StringBuilder builder, string key)
        {
            if (builder.Length > 0) builder.Append(", ");
            builder.Append(key);
        }

        private static bool ReadDouble(object telemetry, string key, out double destination)
        {
            object value;
            if (!TryGetTelemetryValue(telemetry, key, out value)) { destination = 0.0; return false; }
            destination = ToDouble(value); return true;
        }

        private static bool ReadInt(object telemetry, string key, out int destination)
        {
            object value;
            if (!TryGetTelemetryValue(telemetry, key, out value)) { destination = 0; return false; }
            destination = ToInt(value); return true;
        }

        private static bool ReadLong(object telemetry, string key, out long destination)
        {
            object value;
            if (!TryGetTelemetryValue(telemetry, key, out value)) { destination = 0L; return false; }
            destination = ToLong(value); return true;
        }

        private static bool ReadBool(object telemetry, string key, out bool destination)
        {
            object value;
            if (!TryGetTelemetryValue(telemetry, key, out value)) { destination = false; return false; }
            destination = ToBool(value); return true;
        }

        private static object GetValue(PropertyInfo property, object target)
        {
            if (property == null || target == null) return null;
            try { return property.GetValue(target, null); } catch { return null; }
        }

        private static bool TryGetTelemetryValue(object telemetry, string key, out object value)
        {
            value = null;
            IDictionary<string, object> generic = telemetry as IDictionary<string, object>;
            if (generic != null) return generic.TryGetValue(key, out value);
            IDictionary dictionary = telemetry as IDictionary;
            if (dictionary != null && dictionary.Contains(key)) { value = dictionary[key]; return true; }
            IEnumerable enumerable = telemetry as IEnumerable;
            if (enumerable == null) return false;
            foreach (object item in enumerable)
            {
                if (item == null) continue;
                Type type = item.GetType();
                PropertyInfo keyProperty = type.GetProperty("Key") ?? type.GetProperty("Name");
                PropertyInfo valueProperty = type.GetProperty("Value");
                if (keyProperty == null || valueProperty == null) continue;
                string itemKey = Convert.ToString(GetValue(keyProperty, item), CultureInfo.InvariantCulture);
                if (!string.Equals(itemKey, key, StringComparison.OrdinalIgnoreCase)) continue;
                value = GetValue(valueProperty, item); return true;
            }
            return false;
        }

        private static double ToDouble(object value)
        {
            if (value == null) return 0.0;
            try { return Convert.ToDouble(value, CultureInfo.InvariantCulture); } catch { return 0.0; }
        }

        private static int ToInt(object value)
        {
            if (value == null) return 0;
            try { return Convert.ToInt32(value, CultureInfo.InvariantCulture); } catch { return 0; }
        }

        private static long ToLong(object value)
        {
            if (value == null) return 0L;
            try { return Convert.ToInt64(value, CultureInfo.InvariantCulture); } catch { return 0L; }
        }

        private static bool ToBool(object value)
        {
            if (value == null) return false;
            if (value is bool) return (bool)value;
            try { return Convert.ToInt32(value, CultureInfo.InvariantCulture) != 0; }
            catch { bool parsed; return bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out parsed) && parsed; }
        }
    }
}
