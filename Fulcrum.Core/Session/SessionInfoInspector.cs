using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Fulcrum.Core.Session
{
    /// <summary>
    /// Inspects the CurrentSessionInfo object exposed by SimHub.
    ///
    /// This is a temporary diagnostic component used to discover
    /// the exact object structure available at runtime.
    /// </summary>
    public sealed class SessionInfoInspector
    {
        private const int MaximumDepth = 4;
        private const int MaximumCollectionItems = 1;
        private const int MaximumOutputLength = 12000;

        private Type cachedRawDataType;
        private PropertyInfo currentSessionInfoProperty;

        public SessionInfoInspector()
        {
            Reset();
        }

        public bool HasSessionInfo
        {
            get;
            private set;
        }

        public string RawDataType
        {
            get;
            private set;
        }

        public string SessionInfoType
        {
            get;
            private set;
        }

        public string Structure
        {
            get;
            private set;
        }

        public string Error
        {
            get;
            private set;
        }

        public DateTime CapturedAtUtc
        {
            get;
            private set;
        }

        public void Inspect(
            object rawData)
        {
            ResetResult();

            if (rawData == null)
            {
                Error = "RawData is null";

                return;
            }

            RawDataType =
                rawData.GetType().FullName
                ?? rawData.GetType().Name;

            try
            {
                EnsureRawDataAccessor(
                    rawData.GetType());

                if (currentSessionInfoProperty == null)
                {
                    Error =
                        "CurrentSessionInfo property was not found";

                    return;
                }

                object sessionInfo =
                    currentSessionInfoProperty.GetValue(
                        rawData,
                        null);

                if (sessionInfo == null)
                {
                    Error =
                        "CurrentSessionInfo is null";

                    return;
                }

                HasSessionInfo = true;

                SessionInfoType =
                    sessionInfo.GetType().FullName
                    ?? sessionInfo.GetType().Name;

                StringBuilder builder =
                    new StringBuilder();

                HashSet<object> visited =
                    new HashSet<object>(
                        ReferenceEqualityComparer.Instance);

                AppendObject(
                    builder,
                    "CurrentSessionInfo",
                    sessionInfo,
                    0,
                    visited);

                if (builder.Length >
                    MaximumOutputLength)
                {
                    builder.Length =
                        MaximumOutputLength;

                    builder.AppendLine();
                    builder.Append(
                        "[OUTPUT TRUNCATED]");
                }

                Structure =
                    builder.ToString();

                CapturedAtUtc =
                    DateTime.UtcNow;
            }
            catch (Exception exception)
            {
                Error =
                    exception.GetType().Name +
                    ": " +
                    exception.Message;
            }
        }

        public void Reset()
        {
            cachedRawDataType = null;
            currentSessionInfoProperty = null;

            ResetResult();
        }

        private void ResetResult()
        {
            HasSessionInfo = false;

            RawDataType = string.Empty;
            SessionInfoType = string.Empty;
            Structure = string.Empty;
            Error = string.Empty;

            CapturedAtUtc =
                DateTime.MinValue;
        }

        private void EnsureRawDataAccessor(
            Type rawDataType)
        {
            if (rawDataType ==
                cachedRawDataType)
            {
                return;
            }

            cachedRawDataType =
                rawDataType;

            currentSessionInfoProperty =
                rawDataType.GetProperty(
                    "CurrentSessionInfo",
                    BindingFlags.Public |
                    BindingFlags.Instance);
        }

        private static void AppendObject(
            StringBuilder builder,
            string name,
            object value,
            int depth,
            HashSet<object> visited)
        {
            if (builder.Length >=
                MaximumOutputLength)
            {
                return;
            }

            AppendIndent(
                builder,
                depth);

            if (value == null)
            {
                builder.Append(name);
                builder.AppendLine(": null");

                return;
            }

            Type valueType =
                value.GetType();

            builder.Append(name);
            builder.Append(": ");
            builder.Append(valueType.FullName
                ?? valueType.Name);

            if (IsSimpleType(valueType))
            {
                builder.Append(" = ");
                builder.AppendLine(
                    ConvertToText(value));

                return;
            }

            builder.AppendLine();

            if (depth >=
                MaximumDepth)
            {
                AppendIndent(
                    builder,
                    depth + 1);

                builder.AppendLine(
                    "[MAX DEPTH]");

                return;
            }

            if (!valueType.IsValueType)
            {
                if (visited.Contains(value))
                {
                    AppendIndent(
                        builder,
                        depth + 1);

                    builder.AppendLine(
                        "[ALREADY VISITED]");

                    return;
                }

                visited.Add(value);
            }

            IDictionary dictionary =
                value as IDictionary;

            if (dictionary != null)
            {
                AppendDictionary(
                    builder,
                    dictionary,
                    depth + 1,
                    visited);

                return;
            }

            IEnumerable enumerable =
                value as IEnumerable;

            if (enumerable != null &&
                !(value is string))
            {
                AppendEnumerable(
                    builder,
                    enumerable,
                    depth + 1,
                    visited);

                return;
            }

            PropertyInfo[] properties =
                valueType.GetProperties(
                    BindingFlags.Public |
                    BindingFlags.Instance);

            if (properties.Length == 0)
            {
                AppendIndent(
                    builder,
                    depth + 1);

                builder.AppendLine(
                    "[NO PUBLIC PROPERTIES]");

                return;
            }

            for (int index = 0;
                 index < properties.Length;
                 index++)
            {
                PropertyInfo property =
                    properties[index];

                if (!property.CanRead ||
                    property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                object propertyValue;

                try
                {
                    propertyValue =
                        property.GetValue(
                            value,
                            null);
                }
                catch (Exception exception)
                {
                    AppendIndent(
                        builder,
                        depth + 1);

                    builder.Append(
                        property.Name);

                    builder.Append(
                        ": [READ ERROR: ");

                    builder.Append(
                        exception.GetType().Name);

                    builder.AppendLine("]");

                    continue;
                }

                AppendObject(
                    builder,
                    property.Name,
                    propertyValue,
                    depth + 1,
                    visited);

                if (builder.Length >=
                    MaximumOutputLength)
                {
                    return;
                }
            }
        }

        private static void AppendDictionary(
            StringBuilder builder,
            IDictionary dictionary,
            int depth,
            HashSet<object> visited)
        {
            AppendIndent(
                builder,
                depth);

            builder.Append("Count = ");
            builder.AppendLine(
                dictionary.Count.ToString(
                    CultureInfo.InvariantCulture));

            int inspected = 0;

            foreach (DictionaryEntry entry
                     in dictionary)
            {
                string key =
                    entry.Key == null
                        ? "null"
                        : ConvertToText(
                            entry.Key);

                AppendObject(
                    builder,
                    "[" + key + "]",
                    entry.Value,
                    depth,
                    visited);

                inspected++;

                if (inspected >=
                    MaximumCollectionItems)
                {
                    break;
                }
            }
        }

        private static void AppendEnumerable(
            StringBuilder builder,
            IEnumerable enumerable,
            int depth,
            HashSet<object> visited)
        {
            int inspected = 0;

            foreach (object item
                     in enumerable)
            {
                AppendObject(
                    builder,
                    "[" +
                    inspected.ToString(
                        CultureInfo.InvariantCulture) +
                    "]",
                    item,
                    depth,
                    visited);

                inspected++;

                if (inspected >=
                    MaximumCollectionItems)
                {
                    break;
                }
            }

            if (inspected == 0)
            {
                AppendIndent(
                    builder,
                    depth);

                builder.AppendLine(
                    "[EMPTY COLLECTION]");
            }
        }

        private static bool IsSimpleType(
            Type type)
        {
            Type nullableType =
                Nullable.GetUnderlyingType(
                    type);

            if (nullableType != null)
            {
                type = nullableType;
            }

            return
                type.IsPrimitive ||
                type.IsEnum ||
                type == typeof(string) ||
                type == typeof(decimal) ||
                type == typeof(DateTime) ||
                type == typeof(TimeSpan) ||
                type == typeof(Guid);
        }

        private static string ConvertToText(
            object value)
        {
            if (value == null)
            {
                return "null";
            }

            try
            {
                return Convert.ToString(
                    value,
                    CultureInfo.InvariantCulture)
                    ?? string.Empty;
            }
            catch
            {
                return value.ToString()
                    ?? string.Empty;
            }
        }

        private static void AppendIndent(
            StringBuilder builder,
            int depth)
        {
            for (int index = 0;
                 index < depth;
                 index++)
            {
                builder.Append("  ");
            }
        }

        private sealed class
            ReferenceEqualityComparer :
            IEqualityComparer<object>
        {
            public static readonly
                ReferenceEqualityComparer Instance =
                    new ReferenceEqualityComparer();

            public new bool Equals(
                object first,
                object second)
            {
                return ReferenceEquals(
                    first,
                    second);
            }

            public int GetHashCode(
                object value)
            {
                return
                    System.Runtime.CompilerServices
                        .RuntimeHelpers
                        .GetHashCode(value);
            }
        }
    }
}