using System;
using System.Collections;
using System.Reflection;
using System.Text;

namespace Fulcrum.Core.Diagnostics
{
    public static class ObjectInspector
    {
        public static string Inspect(object target)
        {
            if (target == null)
            {
                return "The inspected object is null.";
            }

            StringBuilder report = new StringBuilder();

            Type targetType = target.GetType();

            report.AppendLine("=====================================");
            report.AppendLine("FULCRUM DIAGNOSTICS");
            report.AppendLine("=====================================");
            report.AppendLine();
            report.AppendLine("Type: " + targetType.FullName);
            report.AppendLine();

            InspectObject(target, report, 0, 1);

            return report.ToString();
        }

        private static void InspectObject(
            object target,
            StringBuilder report,
            int depth,
            int maxDepth)
        {
            if (target == null || depth > maxDepth)
            {
                return;
            }

            Type targetType = target.GetType();

            PropertyInfo[] properties = targetType.GetProperties(
                BindingFlags.Public |
                BindingFlags.Instance);

            foreach (PropertyInfo property in properties)
            {
                string indentation = new string(' ', depth * 4);

                try
                {
                    object value = property.GetValue(target);

                    report.Append(indentation);
                    report.Append(property.Name);
                    report.Append(" : ");
                    report.Append(property.PropertyType.FullName);

                    if (value == null)
                    {
                        report.AppendLine(" = null");
                        continue;
                    }

                    if (IsSimpleType(property.PropertyType))
                    {
                        report.AppendLine(" = " + value);
                        continue;
                    }

                    IEnumerable collection = value as IEnumerable;

                    if (collection != null && !(value is string))
                    {
                        int count = GetCollectionCount(collection);

                        report.AppendLine(" = Collection (" + count + " items)");
                        continue;
                    }

                    report.AppendLine();

                    InspectObject(
                        value,
                        report,
                        depth + 1,
                        maxDepth);
                }
                catch (Exception exception)
                {
                    report.AppendLine(
                        indentation +
                        property.Name +
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

        private static int GetCollectionCount(IEnumerable collection)
        {
            int count = 0;

            foreach (object item in collection)
            {
                count++;
            }

            return count;
        }
    }
}