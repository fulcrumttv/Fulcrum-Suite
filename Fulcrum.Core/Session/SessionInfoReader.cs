using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Fulcrum.Core.Resources;

namespace Fulcrum.Core.Session
{
    /// <summary>
    /// Reads static participant identity data from iRacing SessionData.
    /// Reflection keeps Fulcrum.Core independent from SimHub/iRacing assemblies.
    /// </summary>
    public sealed class SessionInfoReader
    {
        private Type cachedRawType;
        private MemberInfo sessionDataMember;
        private readonly RelativeResourceResolver resourceResolver = new RelativeResourceResolver();

        public bool HasSessionData { get; private set; }
        public bool HasDriverInfo { get; private set; }
        public int LastDriverCount { get; private set; }
        public string LastError { get; private set; }
        public DateTime LastUpdatedUtc { get; private set; }

        public SessionInfoReader()
        {
            ResetState();
        }

        public void Update(object rawData, SessionDatabase database)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            ResetState();

            if (rawData == null)
            {
                LastError = "RawData is null";
                database.Reset();
                return;
            }

            try
            {
                object sessionData = ReadSessionData(rawData);
                HasSessionData = sessionData != null;

                if (sessionData == null)
                {
                    LastError = "SessionData was not found";
                    database.Reset();
                    return;
                }

                object driverInfo = GetMemberValue(sessionData, "DriverInfo");
                HasDriverInfo = driverInfo != null;

                if (driverInfo == null)
                {
                    LastError = "DriverInfo was not found";
                    database.Reset();
                    return;
                }

                object driversObject = GetMemberValue(driverInfo, "Drivers");
                IEnumerable drivers = driversObject as IEnumerable;

                if (drivers == null)
                {
                    LastError = "DriverInfo.Drivers was not enumerable";
                    database.Reset();
                    return;
                }

                database.Reset();

                // iRacing added FlairID after the strongly-typed SessionData model
                // used by some SimHub builds was generated.  Those older typed
                // DriverInfo.Drivers objects may therefore omit FlairID even though
                // the raw SessionData dictionary still contains it.  Build a
                // CarIdx -> FlairID lookup from every raw representation we can
                // safely inspect before processing the typed driver list.
                Dictionary<int, int> flairByCarIndex = BuildFlairIdMap(rawData, sessionData);

                foreach (object driver in drivers)
                {
                    if (driver == null)
                    {
                        continue;
                    }

                    int carIndex = ReadInt(driver, "CarIdx", "CarIndex");
                    if (carIndex < 0 || carIndex >= SessionDatabase.Capacity)
                    {
                        continue;
                    }

                    string driverName = ReadString(driver, "UserName", "DriverName", "AbbrevName");
                    int userId = ReadInt(driver, "UserID", "UserId", "CustID", "CustomerID");
                    string carNumber = ReadString(driver, "CarNumber", "CarNumberRaw");
                    string teamName = ReadString(driver, "TeamName");
                    string className = ReadString(driver, "CarClassShortName", "CarClassName");
                    string driverInfoRaw = BuildDriverInfoRaw(driver);

                    int flairId = ReadInt(driver, "FlairID", "FlairId");
                    string flairSource = flairId > 0 ? "TypedDriver" : string.Empty;
                    if (flairId <= 0)
                    {
                        int rawFlairId;
                        if (flairByCarIndex.TryGetValue(carIndex, out rawFlairId))
                        {
                            flairId = rawFlairId;
                            flairSource = "RawSessionData";
                        }
                    }

                    // Keep the public DriverIdentity ABI unchanged for this first
                    // diagnostic build.  Appending the two synthetic fields lets
                    // the existing DriverInfoRaw property prove that FlairID is
                    // being captured before we wire the permanent country lookup.
                    if (flairId > 0)
                    {
                        driverInfoRaw = AppendSyntheticRawField(driverInfoRaw, "FlairID", flairId);
                        driverInfoRaw = AppendSyntheticRawField(driverInfoRaw, "FlairSource", flairSource);
                    }
                    else
                    {
                        driverInfoRaw = AppendSyntheticRawField(driverInfoRaw, "FlairID", "NOT_FOUND");
                    }

                    int carId = ReadInt(driver, "CarID", "CarId", "CarModelID", "CarModelId");
                    float carClassEstimatedLapTime = (float)ReadDouble(
                        driver,
                        "CarClassEstLapTime",
                        "CarClassEstimatedLapTime");
                    string carPath = ReadString(driver, "CarPath");
                    string carScreenName = ReadString(driver, "CarScreenName", "CarScreenNameShort");
                    string carName = ReadString(driver, "CarName");
                    string manufacturer = ReadManufacturer(driver);
                    if (string.IsNullOrWhiteSpace(manufacturer))
                    {
                        // Manufacturer fallback is intentionally limited to
                        // structured car identity. Raw diagnostic fields must not
                        // participate in brand detection.
                        string normalizedDriverInfo = string.Join(" ", new[]
                        {
                            carPath,
                            carScreenName,
                            carName,
                            className
                        }).ToLowerInvariant();
                        if (normalizedDriverInfo.Contains("bmw") ||
                            normalizedDriverInfo.Contains("bmwm4gt4") ||
                            normalizedDriverInfo.Contains("m4 gt4") ||
                            normalizedDriverInfo.Contains("bmwg82") ||
                            normalizedDriverInfo.Contains("g82 gt4") ||
                            normalizedDriverInfo.Contains("m4gt4") ||
                            normalizedDriverInfo.Contains("g82m4") ||
                            normalizedDriverInfo.Contains("bmw m4"))
                        {
                            manufacturer = "BMW";
                        }
                    }
                    int iRating = ReadInt(driver, "IRating", "iRating");
                    string license = ReadString(driver, "LicString", "License", "LicenseString");
                    string clubName = ReadString(driver, "ClubName", "Club", "CountryName");
                    string countryCode = ReadString(driver, "CountryCode", "CountryCode2");

                    // iRacing's current driver flair is exposed as FlairID rather than
                    // CountryCode in SimHub's raw SessionData. Resolve the IDs for the
                    // flag resources shipped with Fulcrum Relative PRO.
                    if (string.IsNullOrWhiteSpace(countryCode) && flairId > 0)
                    {
                        countryCode = ResolveFlairCountryCode(flairId);
                    }

                    string flagText = BuildFlagText(countryCode, clubName);

                    database.SetDriver(
                        carIndex,
                        driverName,
                        carNumber,
                        teamName,
                        className);

                    DriverIdentity identity = database.GetWritable(carIndex);
                    identity.SetExtendedData(
                        manufacturer,
                        iRating,
                        license,
                        clubName,
                        flagText);

                    identity.SetDiagnosticData(
                        userId,
                        carId,
                        carPath,
                        carScreenName,
                        carName,
                        driverInfoRaw,
                        carClassEstimatedLapTime);

                    identity.SetClassIdentity(
                        ReadInt(driver, "CarClassID", "CarClassId"),
                        ReadInt(driver, "CarIsPaceCar", "IsPaceCar") == 1 ||
                        ReadInt(driver, "IsSpectator") == 1);

                    // Canonical brand resolution: structured car identity only.
                    // CarPath is authoritative; raw diagnostic fields are never used
                    // to decide a manufacturer/logo.
                    string manufacturerAlias =
                        resourceResolver.ResolveManufacturerAliasForCar(
                            manufacturer,
                            className,
                            carPath,
                            carScreenName,
                            carName);
                    string logoResourceKey =
                        resourceResolver.ResolveLogoResourceKeyForCar(
                            manufacturer,
                            className,
                            carPath,
                            carScreenName,
                            carName);
                    string countryAlias =
                        resourceResolver.ResolveCountryAlias(
                            countryCode,
                            clubName);
                    string flagResourceKey =
                        resourceResolver.ResolveFlagResourceKey(
                            countryCode,
                            clubName);

                    identity.SetResourceData(
                        manufacturerAlias,
                        logoResourceKey,
                        countryAlias,
                        flagResourceKey);
                }

                database.RefreshValidDriverCount();
                LastDriverCount = database.ValidDriverCount;
                LastUpdatedUtc = DateTime.UtcNow;
            }
            catch (Exception exception)
            {
                LastError = exception.GetType().Name + ": " + exception.Message;
                database.Reset();
            }
        }

        public void Reset(SessionDatabase database)
        {
            ResetState();

            if (database != null)
            {
                database.Reset();
            }
        }

        private object ReadSessionData(object rawData)
        {
            Type rawType = rawData.GetType();

            if (rawType != cachedRawType)
            {
                cachedRawType = rawType;
                sessionDataMember = FindMember(rawType, "SessionData");
            }

            object sessionData = GetMemberValue(rawData, sessionDataMember);

            if (sessionData != null)
            {
                return sessionData;
            }

            return GetMemberValue(rawData, "AllSessionData");
        }

        private void ResetState()
        {
            HasSessionData = false;
            HasDriverInfo = false;
            LastDriverCount = 0;
            LastError = string.Empty;
            LastUpdatedUtc = DateTime.MinValue;
        }



        private static Dictionary<int, int> BuildFlairIdMap(object rawData, object sessionData)
        {
            Dictionary<int, int> result = new Dictionary<int, int>();

            // 1) The typed SessionData tree, including its legacy "Dictionnary"
            //    member when present.
            CollectFlairPairs(sessionData, result, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));

            // 2) SimHub DataSampleEx also exposes a flattened SessionDataDict.
            //    This representation is especially useful for SDK fields added
            //    after the strongly-typed iRacingSDK classes were generated.
            object sessionDataDict = GetMemberValue(rawData, "SessionDataDict");
            CollectFlairPairs(sessionDataDict, result, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));

            // 3) Some readers expose the raw collection as AllSessionData.
            object allSessionData = GetMemberValue(rawData, "AllSessionData");
            CollectFlairPairs(allSessionData, result, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));

            return result;
        }

        private static void CollectFlairPairs(
            object value,
            Dictionary<int, int> result,
            int depth,
            HashSet<object> visited)
        {
            if (value == null || result == null || depth > 8) return;

            Type valueType = value.GetType();
            if (value is string || valueType.IsPrimitive || valueType.IsEnum ||
                value is decimal || value is DateTime || value is TimeSpan)
            {
                return;
            }

            if (!valueType.IsValueType)
            {
                if (visited.Contains(value)) return;
                visited.Add(value);
            }

            IDictionary dictionary = value as IDictionary;
            if (dictionary != null)
            {
                CollectFlairPairFromDictionary(dictionary, result);

                foreach (DictionaryEntry entry in dictionary)
                {
                    CollectFlairPairs(entry.Value, result, depth + 1, visited);
                }
                return;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                int count = 0;
                foreach (object item in enumerable)
                {
                    CollectFlairPairs(item, result, depth + 1, visited);
                    count++;
                    if (count > 5000) break;
                }
                return;
            }

            // If this is a driver-like object with both values available, capture
            // it immediately. This also handles future SimHub SDK updates where
            // FlairID becomes a public property/field on the typed driver model.
            int carIdx = ReadInt(value, "CarIdx", "CarIndex");
            int flairId = ReadInt(value, "FlairID", "FlairId");
            if (carIdx >= 0 && flairId > 0)
            {
                result[carIdx] = flairId;
            }

            PropertyInfo[] properties;
            try
            {
                properties = valueType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            }
            catch
            {
                properties = new PropertyInfo[0];
            }

            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (!property.CanRead || property.GetIndexParameters().Length > 0) continue;

                // Stay focused on session/driver containers. Traversing every
                // telemetry property would be wasteful and can introduce cycles.
                string name = property.Name ?? string.Empty;
                string lower = name.ToLowerInvariant();
                if (!(lower.Contains("driver") || lower.Contains("session") ||
                      lower.Contains("diction") || lower.Contains("data") ||
                      lower.Contains("flair")))
                {
                    continue;
                }

                object child = null;
                try { child = property.GetValue(value, null); } catch { }
                CollectFlairPairs(child, result, depth + 1, visited);
            }

            FieldInfo[] fields;
            try
            {
                fields = valueType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            }
            catch
            {
                fields = new FieldInfo[0];
            }

            for (int i = 0; i < fields.Length; i++)
            {
                string name = fields[i].Name ?? string.Empty;
                string lower = name.ToLowerInvariant();
                if (!(lower.Contains("driver") || lower.Contains("session") ||
                      lower.Contains("diction") || lower.Contains("data") ||
                      lower.Contains("flair")))
                {
                    continue;
                }

                object child = null;
                try { child = fields[i].GetValue(value); } catch { }
                CollectFlairPairs(child, result, depth + 1, visited);
            }
        }

        private static void CollectFlairPairFromDictionary(
            IDictionary dictionary,
            Dictionary<int, int> result)
        {
            if (dictionary == null || result == null) return;

            // Nested driver dictionary: { CarIdx: 16, FlairID: 134, ... }
            object carValue = GetDictionaryValueIgnoreCase(dictionary, "CarIdx", "CarIndex");
            object flairValue = GetDictionaryValueIgnoreCase(dictionary, "FlairID", "FlairId");
            int carIdx = ToIntSafe(carValue, -1);
            int flairId = ToIntSafe(flairValue, -1);
            if (carIdx >= 0 && flairId > 0)
            {
                result[carIdx] = flairId;
            }

            // Flattened SessionDataDict: keys typically preserve a shared path,
            // e.g. DriverInfo:Drivers:5:CarIdx and ...:FlairID. Pair siblings by
            // their common prefix without depending on one exact separator style.
            Dictionary<string, int> carByPrefix = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> flairByPrefix = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (DictionaryEntry entry in dictionary)
            {
                string key = entry.Key != null ? entry.Key.ToString() : string.Empty;
                if (key.Length == 0) continue;

                string member;
                string prefix = SplitFlattenedKey(key, out member);
                if (prefix.Length == 0 || member.Length == 0) continue;

                if (string.Equals(member, "CarIdx", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(member, "CarIndex", StringComparison.OrdinalIgnoreCase))
                {
                    int parsed = ToIntSafe(entry.Value, -1);
                    if (parsed >= 0) carByPrefix[prefix] = parsed;
                }
                else if (string.Equals(member, "FlairID", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(member, "FlairId", StringComparison.OrdinalIgnoreCase))
                {
                    int parsed = ToIntSafe(entry.Value, -1);
                    if (parsed > 0) flairByPrefix[prefix] = parsed;
                }
            }

            foreach (KeyValuePair<string, int> pair in flairByPrefix)
            {
                int pairedCarIdx;
                if (carByPrefix.TryGetValue(pair.Key, out pairedCarIdx) && pairedCarIdx >= 0)
                {
                    result[pairedCarIdx] = pair.Value;
                }
            }
        }

        private static string SplitFlattenedKey(string key, out string member)
        {
            member = string.Empty;
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;

            int last = -1;
            char[] separators = { ':', '.', '/', '\\', ']' };
            for (int i = 0; i < separators.Length; i++)
            {
                int found = key.LastIndexOf(separators[i]);
                if (found > last) last = found;
            }

            if (last < 0 || last >= key.Length - 1) return string.Empty;
            member = key.Substring(last + 1).TrimStart('[', ' ');
            return key.Substring(0, last + 1);
        }

        private static object GetDictionaryValueIgnoreCase(IDictionary dictionary, params string[] names)
        {
            if (dictionary == null || names == null) return null;

            foreach (DictionaryEntry entry in dictionary)
            {
                string key = entry.Key != null ? entry.Key.ToString() : string.Empty;
                for (int i = 0; i < names.Length; i++)
                {
                    if (string.Equals(key, names[i], StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.Value;
                    }
                }
            }
            return null;
        }

        private static int ToIntSafe(object value, int fallback)
        {
            if (value == null) return fallback;
            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                int parsed;
                return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
                    NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                    ? parsed
                    : fallback;
            }
        }

        private static string AppendSyntheticRawField(string raw, string name, object value)
        {
            string prefix = raw ?? string.Empty;
            string text = name + "=" + (value != null ? value.ToString() : string.Empty);
            return prefix.Length == 0 ? text : prefix + " | " + text;
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            public new bool Equals(object x, object y) { return object.ReferenceEquals(x, y); }
            public int GetHashCode(object obj)
            {
                return obj == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }

        private static string BuildDriverInfoRaw(object driver)
        {
            if (driver == null) return string.Empty;

            try
            {
                System.Text.StringBuilder builder = new System.Text.StringBuilder();
                IDictionary dictionary = driver as IDictionary;

                if (dictionary != null)
                {
                    foreach (DictionaryEntry item in dictionary)
                    {
                        AppendRawField(builder, item.Key != null ? item.Key.ToString() : string.Empty, item.Value);
                    }
                    return builder.ToString();
                }

                Type type = driver.GetType();
                PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                for (int i = 0; i < properties.Length; i++)
                {
                    PropertyInfo property = properties[i];
                    if (!property.CanRead || property.GetIndexParameters().Length > 0) continue;

                    try
                    {
                        AppendRawField(builder, property.Name, property.GetValue(driver, null));
                    }
                    catch { }
                }

                FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                for (int i = 0; i < fields.Length; i++)
                {
                    try
                    {
                        AppendRawField(builder, fields[i].Name, fields[i].GetValue(driver));
                    }
                    catch { }
                }

                return builder.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void AppendRawField(System.Text.StringBuilder builder, string name, object value)
        {
            if (builder == null || string.IsNullOrWhiteSpace(name)) return;

            string valueText = value != null ? value.ToString() : string.Empty;
            if (valueText.Length > 160) valueText = valueText.Substring(0, 160);

            if (builder.Length > 0) builder.Append(" | ");
            builder.Append(name);
            builder.Append('=');
            builder.Append(valueText);
        }

        private static string ResolveFlairCountryCode(int flairId)
        {
            // Current iRacing flair table coverage. FlairID 1 (Unaffiliated)
            // intentionally returns no flag.
            switch (flairId)
            {
                case 2: return "GO";
                case 3: return "AF";
                case 4: return "AX";
                case 5: return "AL";
                case 6: return "DZ";
                case 7: return "AS";
                case 8: return "AD";
                case 9: return "AO";
                case 10: return "AI";
                case 11: return "AQ";
                case 12: return "AG";
                case 13: return "AR";
                case 14: return "AM";
                case 15: return "AW";
                case 16: return "AU";
                case 17: return "AT";
                case 18: return "AZ";
                case 19: return "BS";
                case 20: return "BH";
                case 21: return "BD";
                case 22: return "BB";
                case 23: return "BE";
                case 24: return "BZ";
                case 25: return "BJ";
                case 26: return "BM";
                case 27: return "BT";
                case 28: return "BO";
                case 29: return "BA";
                case 30: return "BW";
                case 31: return "BR";
                case 32: return "VG";
                case 33: return "BN";
                case 34: return "BG";
                case 35: return "BF";
                case 36: return "BI";
                case 37: return "KH";
                case 38: return "CM";
                case 39: return "CA";
                case 40: return "CV";
                case 41: return "KY";
                case 42: return "CF";
                case 43: return "TD";
                case 44: return "CL";
                case 45: return "CN";
                case 46: return "CX";
                case 47: return "CC";
                case 48: return "CO";
                case 49: return "KM";
                case 50: return "CK";
                case 51: return "CR";
                case 52: return "HR";
                case 53: return "CY";
                case 54: return "CZ";
                case 55: return "CD";
                case 56: return "DK";
                case 57: return "DJ";
                case 58: return "DM";
                case 59: return "DO";
                case 60: return "EC";
                case 61: return "EG";
                case 62: return "SV";
                case 63: return "GQ";
                case 64: return "ER";
                case 65: return "EE";
                case 66: return "ET";
                case 67: return "FK";
                case 68: return "FO";
                case 69: return "FJ";
                case 70: return "FI";
                case 71: return "FR";
                case 72: return "GF";
                case 73: return "PF";
                case 74: return "GA";
                case 75: return "GM";
                case 76: return "GE";
                case 77: return "DE";
                case 78: return "GH";
                case 79: return "GI";
                case 80: return "GR";
                case 81: return "GL";
                case 82: return "GD";
                case 83: return "GP";
                case 84: return "GU";
                case 85: return "GT";
                case 86: return "GG";
                case 87: return "GN";
                case 88: return "GW";
                case 89: return "GY";
                case 90: return "HT";
                case 91: return "HN";
                case 92: return "HK";
                case 93: return "HU";
                case 94: return "IS";
                case 95: return "IN";
                case 96: return "ID";
                case 97: return "IQ";
                case 98: return "IE";
                case 99: return "IM";
                case 100: return "IL";
                case 101: return "IT";
                case 102: return "CI";
                case 103: return "JM";
                case 104: return "JP";
                case 105: return "JE";
                case 106: return "JO";
                case 107: return "KZ";
                case 108: return "KE";
                case 109: return "KI";
                case 110: return "KW";
                case 111: return "KG";
                case 112: return "LA";
                case 113: return "LV";
                case 114: return "LB";
                case 115: return "LS";
                case 116: return "LR";
                case 117: return "LY";
                case 118: return "LI";
                case 119: return "LT";
                case 120: return "LU";
                case 121: return "MO";
                case 122: return "MK";
                case 123: return "MG";
                case 124: return "MW";
                case 125: return "MY";
                case 126: return "MV";
                case 127: return "ML";
                case 128: return "MT";
                case 129: return "MH";
                case 130: return "MQ";
                case 131: return "MR";
                case 132: return "MU";
                case 133: return "YT";
                case 134: return "MX";
                case 135: return "FM";
                case 136: return "MD";
                case 137: return "MC";
                case 138: return "MN";
                case 139: return "ME";
                case 140: return "MS";
                case 141: return "MA";
                case 142: return "MZ";
                case 143: return "NA";
                case 144: return "NR";
                case 145: return "NP";
                case 146: return "NL";
                case 148: return "NC";
                case 149: return "NZ";
                case 150: return "NI";
                case 151: return "NE";
                case 152: return "NG";
                case 153: return "NU";
                case 154: return "NF";
                case 155: return "MP";
                case 156: return "NO";
                case 157: return "OM";
                case 158: return "PK";
                case 159: return "PW";
                case 160: return "PS";
                case 161: return "PA";
                case 162: return "PG";
                case 163: return "PY";
                case 164: return "PE";
                case 165: return "PH";
                case 166: return "PN";
                case 167: return "PL";
                case 168: return "PT";
                case 169: return "PR";
                case 170: return "QA";
                case 171: return "CG";
                case 172: return "RE";
                case 173: return "RO";
                case 174: return "RW";
                case 175: return "SH";
                case 176: return "KN";
                case 177: return "LC";
                case 178: return "PM";
                case 179: return "VC";
                case 180: return "BL";
                case 181: return "MF";
                case 182: return "WS";
                case 183: return "SM";
                case 184: return "ST";
                case 185: return "SA";
                case 186: return "SN";
                case 187: return "RS";
                case 188: return "SC";
                case 189: return "SL";
                case 190: return "SG";
                case 191: return "SK";
                case 192: return "SI";
                case 193: return "SB";
                case 194: return "SO";
                case 195: return "ZA";
                case 196: return "GS";
                case 197: return "KR";
                case 198: return "ES";
                case 199: return "LK";
                case 200: return "SR";
                case 201: return "SJ";
                case 202: return "SZ";
                case 203: return "SE";
                case 204: return "CH";
                case 205: return "TW";
                case 206: return "TJ";
                case 207: return "TZ";
                case 208: return "TH";
                case 209: return "TL";
                case 210: return "TG";
                case 211: return "TK";
                case 212: return "TO";
                case 213: return "TT";
                case 214: return "TN";
                case 215: return "TR";
                case 216: return "TM";
                case 217: return "TC";
                case 218: return "TV";
                case 219: return "UG";
                case 220: return "UA";
                case 221: return "AE";
                case 222: return "GB";
                case 223: return "US";
                case 224: return "UY";
                case 225: return "UZ";
                case 226: return "VU";
                case 227: return "VA";
                case 228: return "VE";
                case 229: return "VN";
                case 230: return "VI";
                case 231: return "WF";
                case 232: return "EH";
                case 233: return "YE";
                case 234: return "ZM";
                case 235: return "ZW";
                case 236: return "ENG";
                case 237: return "SCT";
                case 238: return "WLS";
                case 239: return "NIR";
                case 240: return "BQ";
                case 241: return "CW";
                case 242: return "SX";
                default:
                    return string.Empty;
            }
        }

        private static string BuildFlagText(string countryCode, string clubName)
        {
            string code = NormalizeCountryCode(countryCode, clubName);

            if (code.Length != 2)
            {
                return string.Empty;
            }

            // Publish a stable ISO code. The dashboard maps this code to an
            // embedded flag image; emoji flags are not rendered reliably by
            // SimHub's HTML/WPF renderers.
            return code;
        }

        private static string NormalizeCountryCode(string countryCode, string clubName)
        {
            string code = (countryCode ?? string.Empty).Trim().ToUpperInvariant();
            if (code.Length == 2) return code;

            string club = (clubName ?? string.Empty).Trim().ToLowerInvariant();
            if (club.Contains("mexico")) return "MX";
            if (club.Contains("puerto rico")) return "PR";
            if (club.Contains("canada")) return "CA";
            if (club.Contains("brazil")) return "BR";
            if (club.Contains("argentina")) return "AR";
            if (club.Contains("chile")) return "CL";
            if (club.Contains("colombia")) return "CO";
            if (club.Contains("peru")) return "PE";
            if (club.Contains("venezuela")) return "VE";
            if (club.Contains("united kingdom") || club.Contains("great britain")) return "GB";
            if (club.Contains("germany")) return "DE";
            if (club.Contains("france")) return "FR";
            if (club.Contains("spain")) return "ES";
            if (club.Contains("italy")) return "IT";
            if (club.Contains("poland")) return "PL";
            if (club.Contains("netherlands")) return "NL";
            if (club.Contains("belgium")) return "BE";
            if (club.Contains("australia")) return "AU";
            if (club.Contains("new zealand")) return "NZ";
            if (club.Contains("central-eastern europe") || club.Contains("central eastern europe")) return "PL";
            if (club.Contains("iberia")) return "ES";
            if (club.Contains("scandinavia")) return "SE";
            if (club.Contains("finland")) return "FI";
            if (club.Contains("norway")) return "NO";
            if (club.Contains("sweden")) return "SE";
            if (club.Contains("denmark")) return "DK";
            if (club.Contains("switzerland")) return "CH";
            if (club.Contains("austria")) return "AT";
            if (club.Contains("portugal")) return "PT";
            if (club.Contains("ireland")) return "IE";
            if (club.Contains("czech")) return "CZ";
            if (club.Contains("slovakia")) return "SK";
            if (club.Contains("hungary")) return "HU";
            if (club.Contains("romania")) return "RO";
            if (club.Contains("croatia")) return "HR";
            if (club.Contains("slovenia")) return "SI";
            if (club.Contains("greece")) return "GR";
            if (club.Contains("turkey")) return "TR";
            if (club.Contains("south africa")) return "ZA";
            if (club.Contains("india")) return "IN";
            if (club.Contains("china")) return "CN";
            if (club.Contains("taiwan")) return "TW";
            if (club.Contains("japan")) return "JP";
            if (club.Contains("south korea") || club.Contains("korea")) return "KR";
            if (club.Contains("united states") || club.Contains("usa") || club.Contains("atlantic") || club.Contains("california") || club.Contains("carolina") || club.Contains("central") || club.Contains("florida") || club.Contains("georgia") || club.Contains("great plains") || club.Contains("illinois") || club.Contains("indiana") || club.Contains("massachusetts") || club.Contains("mid-south") || club.Contains("mid south") || club.Contains("midwest") || club.Contains("new england") || club.Contains("new jersey") || club.Contains("new york") || club.Contains("northwest") || club.Contains("ohio") || club.Contains("pennsylvania") || club.Contains("plains") || club.Contains("rocky mountain") || club.Contains("south east") || club.Contains("southeast") || club.Contains("southwest") || club.Contains("texas") || club.Contains("virginia") || club.Contains("washington") || club.Contains("wisconsin")) return "US";
            return string.Empty;
        }

        private static string ReadManufacturer(object driver)
        {
            string explicitValue = ReadString(
                driver,
                "CarMake",
                "Manufacturer",
                "CarManufacturer",
                "CarMakeName");

            string descriptor = string.Join(
                " ",
                new[]
                {
                    explicitValue,
                    ReadString(driver, "CarScreenName"),
                    ReadString(driver, "CarScreenNameShort"),
                    ReadString(driver, "CarPath"),
                    ReadString(driver, "CarName")
                });

            string normalized = descriptor.ToLowerInvariant();

            if (normalized.Contains("aston")) return "Aston Martin";
            if (normalized.Contains("mercedes") || normalized.Contains("amg")) return "Mercedes";
            if (normalized.Contains("lamborghini")) return "Lamborghini";
            if (normalized.Contains("mclaren")) return "McLaren";
            if (normalized.Contains("porsche")) return "Porsche";
            if (normalized.Contains("ferrari")) return "Ferrari";
            if (normalized.Contains("bmw")) return "BMW";
            if (normalized.Contains("audi")) return "Audi";
            if (normalized.Contains("acura")) return "Acura";
            if (normalized.Contains("cadillac")) return "Cadillac";
            if (normalized.Contains("chevrolet") || normalized.Contains("corvette")) return "Chevrolet";
            if (normalized.Contains("ford") || normalized.Contains("mustang")) return "Ford";
            if (normalized.Contains("nissan")) return "Nissan";
            if (normalized.Contains("toyota")) return "Toyota";
            if (normalized.Contains("lexus")) return "Lexus";
            if (normalized.Contains("mazda")) return "Mazda";
            if (normalized.Contains("honda")) return "Honda";

            return explicitValue ?? string.Empty;
        }

        private static string ReadString(object source, params string[] names)
        {
            for (int index = 0; index < names.Length; index++)
            {
                object value = GetMemberValue(source, names[index]);

                if (value == null)
                {
                    continue;
                }

                string text = Convert.ToString(value, CultureInfo.InvariantCulture);

                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text.Trim();
                }
            }

            return string.Empty;
        }

        private static double ReadDouble(object source, params string[] names)
        {
            if (source == null || names == null) return 0.0;
            for (int index = 0; index < names.Length; index++)
            {
                object value = GetMemberValue(source, names[index]);
                if (value == null) continue;
                try { return Convert.ToDouble(value, CultureInfo.InvariantCulture); }
                catch { }
            }
            return 0.0;
        }

        private static int ReadInt(object source, params string[] names)
        {
            for (int index = 0; index < names.Length; index++)
            {
                object value = GetMemberValue(source, names[index]);

                if (value == null)
                {
                    continue;
                }

                try
                {
                    return Convert.ToInt32(value, CultureInfo.InvariantCulture);
                }
                catch
                {
                    int parsed;
                    if (int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out parsed))
                    {
                        return parsed;
                    }
                }
            }

            return -1;
        }

        private static object GetMemberValue(object source, string name)
        {
            if (source == null)
            {
                return null;
            }

            IDictionary dictionary = source as IDictionary;
            if (dictionary != null && dictionary.Contains(name))
            {
                return dictionary[name];
            }

            return GetMemberValue(source, FindMember(source.GetType(), name));
        }

        private static object GetMemberValue(object source, MemberInfo member)
        {
            if (source == null || member == null)
            {
                return null;
            }

            PropertyInfo property = member as PropertyInfo;
            if (property != null)
            {
                return property.GetValue(source, null);
            }

            FieldInfo field = member as FieldInfo;
            return field != null ? field.GetValue(source) : null;
        }

        private static MemberInfo FindMember(Type type, string name)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null)
            {
                return property;
            }

            return type.GetField(name, flags);
        }
    }
}
