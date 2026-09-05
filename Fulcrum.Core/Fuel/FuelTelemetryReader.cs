using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace Fulcrum.Core.Fuel
{
    public sealed class FuelTelemetryReader
    {
        private Type cachedRawType;
        private PropertyInfo telemetryProperty;
        private PropertyInfo sessionInfoProperty;
        private double cachedCapacityLiters;

        public bool TryRead(object rawData, out double fuelLiters, out double fuelPercent, out double capacityLiters)
        {
            fuelLiters = 0.0;
            fuelPercent = 0.0;
            capacityLiters = 0.0;

            if (rawData == null)
            {
                return false;
            }

            EnsureProperties(rawData.GetType());

            object telemetry = GetValue(telemetryProperty, rawData);
            object value;

            if (TryGetTelemetryValue(telemetry, "FuelLevel", out value))
            {
                fuelLiters = ToDouble(value);
            }

            if (TryGetTelemetryValue(telemetry, "FuelLevelPct", out value))
            {
                fuelPercent = NormalizePercent(ToDouble(value));
            }

            if (cachedCapacityLiters <= 0.0)
            {
                object sessionInfo = GetValue(sessionInfoProperty, rawData);
                cachedCapacityLiters = FindNumericProperty(sessionInfo, "DriverCarFuelMaxLtr", 5);
            }


            // Direct and recursively nested aliases used by SimHub/GameReader builds.
            if (fuelLiters <= 0.0)
            {
                fuelLiters = FirstPositive(
                    FindNumericProperty(rawData, "FuelLevel", 6),
                    FindNumericProperty(rawData, "Fuel", 6),
                    FindNumericProperty(rawData, "CurrentFuel", 6));
            }

            if (fuelPercent <= 0.0)
            {
                fuelPercent = NormalizePercent(FirstPositive(
                    FindNumericProperty(rawData, "FuelLevelPct", 6),
                    FindNumericProperty(rawData, "FuelPercent", 6)));
            }

            if (cachedCapacityLiters <= 0.0)
            {
                cachedCapacityLiters = FirstPositive(
                    FindNumericProperty(rawData, "DriverCarFuelMaxLtr", 7),
                    FindNumericProperty(rawData, "MaxFuel", 6),
                    FindNumericProperty(rawData, "FuelCapacity", 6),
                    FindNumericProperty(rawData, "FuelMax", 6));
            }

            capacityLiters = cachedCapacityLiters;

            if (capacityLiters <= 0.0 && fuelLiters > 0.0 && fuelPercent > 0.0)
            {
                capacityLiters = fuelLiters / fuelPercent;
            }

            if (fuelPercent <= 0.0 && capacityLiters > 0.0)
            {
                fuelPercent = Clamp01(fuelLiters / capacityLiters);
            }

            return fuelLiters > 0.0 || capacityLiters > 0.0;
        }

        private void EnsureProperties(Type rawType)
        {
            if (rawType == cachedRawType)
            {
                return;
            }

            cachedRawType = rawType;
            cachedCapacityLiters = 0.0;
            telemetryProperty = rawType.GetProperty("Telemetry", BindingFlags.Instance | BindingFlags.Public);
            sessionInfoProperty = rawType.GetProperty("CurrentSessionInfo", BindingFlags.Instance | BindingFlags.Public);
        }

        private static object GetValue(PropertyInfo property, object target)
        {
            if (property == null || target == null)
            {
                return null;
            }

            try
            {
                return property.GetValue(target, null);
            }
            catch
            {
                return null;
            }
        }

        private static bool TryGetTelemetryValue(object telemetry, string key, out object value)
        {
            value = null;

            IDictionary<string, object> generic = telemetry as IDictionary<string, object>;
            if (generic != null)
            {
                return generic.TryGetValue(key, out value);
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

                Type type = item.GetType();
                PropertyInfo keyProperty = type.GetProperty("Key") ?? type.GetProperty("Name");
                PropertyInfo valueProperty = type.GetProperty("Value");
                if (keyProperty == null || valueProperty == null)
                {
                    continue;
                }

                string itemKey = Convert.ToString(GetValue(keyProperty, item), CultureInfo.InvariantCulture);
                if (!string.Equals(itemKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                value = GetValue(valueProperty, item);
                return true;
            }

            return false;
        }

        private static double FindNumericProperty(object value, string propertyName, int depth)
        {
            if (value == null || depth < 0)
            {
                return 0.0;
            }

            Type type = value.GetType();
            PropertyInfo direct = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (direct != null)
            {
                return ToDouble(GetValue(direct, value));
            }

            if (value is string || type.IsPrimitive || type.IsEnum)
            {
                return 0.0;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                int count = 0;
                foreach (object item in enumerable)
                {
                    double found = FindNumericProperty(item, propertyName, depth - 1);
                    if (found > 0.0)
                    {
                        return found;
                    }

                    count++;
                    if (count >= 80)
                    {
                        break;
                    }
                }
                return 0.0;
            }

            PropertyInfo[] properties;
            try
            {
                properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            }
            catch
            {
                return 0.0;
            }

            for (int index = 0; index < properties.Length; index++)
            {
                PropertyInfo property = properties[index];
                if (!property.CanRead || property.GetIndexParameters().Length != 0)
                {
                    continue;
                }

                object child = GetValue(property, value);
                double found = FindNumericProperty(child, propertyName, depth - 1);
                if (found > 0.0)
                {
                    return found;
                }
            }

            return 0.0;
        }


        private static double FirstPositive(params double[] values)
        {
            if (values == null) return 0.0;
            for (int i = 0; i < values.Length; i++)
            {
                if (!double.IsNaN(values[i]) && !double.IsInfinity(values[i]) && values[i] > 0.0) return values[i];
            }
            return 0.0;
        }

        private static double ToDouble(object value)
        {
            if (value == null)
            {
                return 0.0;
            }

            try
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0.0;
            }
        }

        private static double NormalizePercent(double value)
        {
            if (value > 1.5)
            {
                value /= 100.0;
            }
            return Clamp01(value);
        }

        private static double Clamp01(double value)
        {
            if (value < 0.0) return 0.0;
            if (value > 1.0) return 1.0;
            return value;
        }
    }
}
