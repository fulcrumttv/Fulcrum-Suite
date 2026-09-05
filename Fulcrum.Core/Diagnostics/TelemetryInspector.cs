using System;
using System.Collections;
using System.Reflection;
using System.Text;

namespace Fulcrum.Core.Diagnostics
{
    public static class TelemetryInspector
    {
        private const int MaximumItems = 500;

        public static string Inspect(object rawData)
        {
            StringBuilder report = new StringBuilder();

            report.AppendLine();
            report.AppendLine("=====================================");
            report.AppendLine("FULCRUM TELEMETRY EXPLORER");
            report.AppendLine("=====================================");
            report.AppendLine();

            if (rawData == null)
            {
                report.AppendLine("Raw telemetry object is null.");
                return report.ToString();
            }

            Type rawType = rawData.GetType();

            PropertyInfo telemetryProperty = rawType.GetProperty(
                "Telemetry",
                BindingFlags.Public | BindingFlags.Instance);

            if (telemetryProperty == null)
            {
                report.AppendLine(
                    "The Telemetry property was not found on " +
                    rawType.FullName +
                    ".");

                return report.ToString();
            }

            object telemetry;

            try
            {
                telemetry = telemetryProperty.GetValue(rawData);
            }
            catch (Exception exception)
            {
                report.AppendLine(
                    "Could not read Telemetry: " +
                    exception.Message);

                return report.ToString();
            }

            if (telemetry == null)
            {
                report.AppendLine("Telemetry is null.");
                return report.ToString();
            }

            Type telemetryType = telemetry.GetType();

            report.AppendLine(
                "Telemetry type: " +
                telemetryType.FullName);

            report.AppendLine();

            IEnumerable collection = telemetry as IEnumerable;

            if (collection == null)
            {
                report.AppendLine(
                    "Telemetry does not implement IEnumerable.");

                InspectPublicProperties(
                    telemetry,
                    report,
                    "    ");

                return report.ToString();
            }

            int index = 0;

            foreach (object item in collection)
            {
                if (index >= MaximumItems)
                {
                    report.AppendLine();
                    report.AppendLine(
                        "Maximum item limit reached: " +
                        MaximumItems);

                    break;
                }

                report.AppendLine(
                    "[" +
                    index +
                    "]");

                InspectCollectionItem(
                    item,
                    report,
                    "    ");

                report.AppendLine();

                index++;
            }

            report.AppendLine(
                "Items inspected: " +
                index);

            return report.ToString();
        }

        private static void InspectCollectionItem(
            object item,
            StringBuilder report,
            string indentation)
        {
            if (item == null)
            {
                report.AppendLine(
                    indentation +
                    "null");

                return;
            }

            Type itemType = item.GetType();

            if (IsSimpleType(itemType))
            {
                report.AppendLine(
                    indentation +
                    itemType.FullName +
                    " = " +
                    item);

                return;
            }

            report.AppendLine(
                indentation +
                "Type: " +
                itemType.FullName);

            InspectPublicProperties(
                item,
                report,
                indentation);
        }

        private static void InspectPublicProperties(
            object target,
            StringBuilder report,
            string indentation)
        {
            Type targetType = target.GetType();

            PropertyInfo[] properties = targetType.GetProperties(
                BindingFlags.Public |
                BindingFlags.Instance);

            if (properties.Length == 0)
            {
                report.AppendLine(
                    indentation +
                    "Value: " +
                    target);

                return;
            }

            foreach (PropertyInfo property in properties)
            {
                report.Append(
                    indentation +
                    property.Name +
                    " : " +
                    property.PropertyType.FullName);

                try
                {
                    object value = property.GetValue(target);

                    if (value == null)
                    {
                        report.AppendLine(" = null");
                    }
                    else if (IsSimpleType(value.GetType()))
                    {
                        report.AppendLine(
                            " = " +
                            value);
                    }
                    else
                    {
                        report.AppendLine(
                            " = " +
                            value);
                    }
                }
                catch (Exception exception)
                {
                    report.AppendLine(
                        " = ERROR: " +
                        exception.Message);
                }
            }
        }

        private static bool IsSimpleType(Type type)
        {
            return type.IsPrimitive
                || type.IsEnum
                || type == typeof(string)
                || type == typeof(decimal)
                || type == typeof(DateTime)
                || type == typeof(TimeSpan)
                || type == typeof(Guid);
        }
    }
}