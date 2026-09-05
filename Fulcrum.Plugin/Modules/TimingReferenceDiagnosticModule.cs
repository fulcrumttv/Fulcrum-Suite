using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using Fulcrum.Core.Performance;
using Fulcrum.Core.Relative;
using Fulcrum.Core.Session;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Modules
{
    public sealed class TimingReferenceDiagnosticModule
    {
        private const double UpdateHz = 20.0;
        private const float LapTolerance = 0.030f;

        private readonly PluginManager manager;
        private readonly Type pluginType;
        private readonly ParticipantBuffer participants;
        private readonly SessionDatabase database;
        private readonly int[] previousLapCompleted;

        private object latestRawData;
        private bool latestGameRunning;
        private int latestPlayerCarIndex;
        private int lastSessionNumber;
        private bool hasSessionNumber;

        private long observedClassLapCount;
        private string lastClassLapDriver;
        private float lastClassLapTime;
        private bool classBestLapObserved;
        private string classBestLapObservedDriver;
        private float classBestLapObservedTime;
        private DateTime nextSplitInspectUtc;
        private bool splitInfoFound;
        private int splitCount;
        private string splitSummary;

        // FIX2 diagnostic: raw iRacing class metadata probe.  The shared
        // SessionDatabase can be empty for ClassName on some SimHub builds even
        // while DriverInfo/timing data is otherwise valid.  Keep this probe in
        // the plugin so the installed Fulcrum.Core.dll remains untouched.
        private readonly string[] rawClassNameByCarIndex;
        private readonly int[] rawClassIdByCarIndex;
        private DateTime nextClassInspectUtc;
        private string rawClassSource;
        private int rawClassDriverCount;

        public TimingReferenceDiagnosticModule(
            PluginManager manager,
            Type pluginType,
            UpdateScheduler scheduler,
            ParticipantBuffer participants,
            SessionDatabase database)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.pluginType = pluginType ?? throw new ArgumentNullException(nameof(pluginType));
            this.participants = participants ?? throw new ArgumentNullException(nameof(participants));
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));

            previousLapCompleted = new int[SessionDatabase.Capacity];
            rawClassNameByCarIndex = new string[SessionDatabase.Capacity];
            rawClassIdByCarIndex = new int[SessionDatabase.Capacity];
            RegisterProperties();
            scheduler.RegisterTask("Timing Reference Diagnostic", UpdateHz, UpdateScheduled, false);
            Reset();
        }

        public void SetFrameContext(object rawData, bool gameRunning, int playerCarIndex)
        {
            latestRawData = rawData;
            latestGameRunning = gameRunning;
            latestPlayerCarIndex = playerCarIndex;
        }

        public void Reset()
        {
            latestRawData = null;
            latestGameRunning = false;
            latestPlayerCarIndex = -1;
            hasSessionNumber = false;
            lastSessionNumber = -1;
            ResetObservations();
            Set("Status", "WAITING FOR IRACING");
        }

        private void ResetObservations()
        {
            for (int i = 0; i < previousLapCompleted.Length; i++) previousLapCompleted[i] = int.MinValue;
            observedClassLapCount = 0;
            lastClassLapDriver = string.Empty;
            lastClassLapTime = 0.0f;
            classBestLapObserved = false;
            classBestLapObservedDriver = string.Empty;
            classBestLapObservedTime = 0.0f;
            splitInfoFound = false;
            splitCount = 0;
            splitSummary = string.Empty;
            nextSplitInspectUtc = DateTime.MinValue;
            nextClassInspectUtc = DateTime.MinValue;
            rawClassSource = string.Empty;
            rawClassDriverCount = 0;
            for (int i = 0; i < rawClassNameByCarIndex.Length; i++)
            {
                rawClassNameByCarIndex[i] = string.Empty;
                rawClassIdByCarIndex[i] = -1;
            }
        }

        private void RegisterProperties()
        {
            Add("Status", string.Empty, "Timing diagnostic status");
            Add("SessionNumber", -1, "Session number");
            Add("SessionTime", 0.0, "Session time");
            Add("PlayerCarIndex", -1, "Player CarIdx");
            Add("PlayerClass", string.Empty, "Effective player class name/key");
            Add("DatabasePlayerClass", string.Empty, "Player class from shared SessionDatabase");
            Add("RawPlayerClass", string.Empty, "Player class discovered directly from raw iRacing SessionData");
            Add("RawPlayerClassId", -1, "Player CarClassID discovered directly from raw iRacing SessionData");
            Add("RawClassSource", string.Empty, "Raw SessionData representation which supplied class metadata");
            Add("RawClassDriverCount", 0, "Drivers with raw class metadata");
            Add("ClassCarCount", 0, "Cars in player class");
            Add("ClassValidBestCount", 0, "Class cars with valid best lap");
            Add("MyBestLapText", "--:--.---", "Player best lap");
            Add("ClassBestCarIndex", -1, "Class-best CarIdx");
            Add("ClassBestDriver", string.Empty, "Class-best driver");
            Add("ClassBestLapText", "--:--.---", "Class-best lap");
            Add("SessionBestCarIndex", -1, "Overall-best CarIdx");
            Add("SessionBestDriver", string.Empty, "Overall-best driver");
            Add("SessionBestLapText", "--:--.---", "Overall-best lap");
            Add("ClassBestIsSessionBest", false, "Class and overall reference match");
            Add("RawPersonalDelta", 0.0, "LapDeltaToBestLap");
            Add("RawPersonalDeltaOk", false, "LapDeltaToBestLap_OK");
            Add("RawSessionDelta", 0.0, "LapDeltaToSessionBestLap");
            Add("RawSessionDeltaOk", false, "LapDeltaToSessionBestLap_OK");
            Add("PlayerLapDistPct", -1.0, "Player LapDistPct");
            Add("ClassBestLapDistPct", -1.0, "Class-best car LapDistPct");
            Add("SplitInfoFound", false, "SplitTimeInfo found");
            Add("SplitCount", 0, "Split count");
            Add("SplitSummary", string.Empty, "Sector start percentages");
            Add("ObservedClassLapCount", 0L, "Observed class lap completions");
            Add("LastClassLapDriver", string.Empty, "Last class lap driver");
            Add("LastClassLapTimeText", "--:--.---", "Last class lap time");
            Add("ClassBestLapObserved", false, "Observed a completed lap matching class best");
            Add("ClassBestLapObservedDriver", string.Empty, "Observed class-best driver");
            Add("ClassBestLapObservedTimeText", "--:--.---", "Observed class-best time");
        }

        private void UpdateScheduled()
        {
            if (!latestGameRunning || latestRawData == null || latestPlayerCarIndex < 0 || latestPlayerCarIndex >= SessionDatabase.Capacity)
            {
                Set("Status", "WAITING FOR IRACING");
                return;
            }

            object telemetry = Get(latestRawData, "CurrentTelemetry") ?? Get(latestRawData, "Telemetry");
            int sessionNumber = ToInt(Get(telemetry, "SessionNum"), -1);
            double sessionTime = ToDouble(Get(telemetry, "SessionTime"), 0.0);

            if (!hasSessionNumber || sessionNumber != lastSessionNumber)
            {
                hasSessionNumber = true;
                lastSessionNumber = sessionNumber;
                ResetObservations();
            }

            if (DateTime.UtcNow >= nextClassInspectUtc)
            {
                InspectRawClasses(latestRawData);
                nextClassInspectUtc = DateTime.UtcNow.AddSeconds(1);
            }

            DriverIdentity playerIdentity;
            string databasePlayerClass = database.TryGet(latestPlayerCarIndex, out playerIdentity) && playerIdentity != null
                ? (playerIdentity.ClassName ?? string.Empty)
                : string.Empty;
            string rawPlayerClass = rawClassNameByCarIndex[latestPlayerCarIndex] ?? string.Empty;
            int rawPlayerClassId = rawClassIdByCarIndex[latestPlayerCarIndex];
            string playerClassKey = BuildClassKey(databasePlayerClass, rawPlayerClass, rawPlayerClassId);
            string playerClass = !string.IsNullOrWhiteSpace(databasePlayerClass)
                ? databasePlayerClass
                : (!string.IsNullOrWhiteSpace(rawPlayerClass)
                    ? rawPlayerClass
                    : (rawPlayerClassId > 0 ? "CLASS_ID_" + rawPlayerClassId.ToString(CultureInfo.InvariantCulture) : string.Empty));

            ParticipantSnapshot player = participants[latestPlayerCarIndex];
            int classCarCount = 0;
            int classValidBestCount = 0;
            int classBestIdx = -1;
            string classBestDriver = string.Empty;
            float classBestLap = 0.0f;
            int sessionBestIdx = -1;
            string sessionBestDriver = string.Empty;
            float sessionBestLap = 0.0f;

            for (int carIndex = 0; carIndex < SessionDatabase.Capacity; carIndex++)
            {
                DriverIdentity identity;
                bool hasIdentity = database.TryGet(carIndex, out identity) && identity != null;
                string dbClass = hasIdentity ? (identity.ClassName ?? string.Empty) : string.Empty;
                string otherClassKey = BuildClassKey(dbClass, rawClassNameByCarIndex[carIndex], rawClassIdByCarIndex[carIndex]);
                bool sameClass = !string.IsNullOrWhiteSpace(playerClassKey) &&
                    string.Equals(otherClassKey, playerClassKey, StringComparison.OrdinalIgnoreCase);
                if (sameClass) classCarCount++;

                ParticipantSnapshot p = participants[carIndex];
                if (p == null || !p.IsValid) continue;
                float best = ValidLap(p.BestLapTime);
                if (best <= 0.0f) continue;

                if (sessionBestLap <= 0.0f || best < sessionBestLap)
                {
                    sessionBestLap = best;
                    sessionBestIdx = carIndex;
                    sessionBestDriver = hasIdentity ? (identity.DriverName ?? string.Empty) : string.Empty;
                }

                if (sameClass)
                {
                    classValidBestCount++;
                    if (classBestLap <= 0.0f || best < classBestLap)
                    {
                        classBestLap = best;
                        classBestIdx = carIndex;
                        classBestDriver = hasIdentity ? (identity.DriverName ?? string.Empty) : ("CarIdx " + carIndex.ToString(CultureInfo.InvariantCulture));
                    }
                }
            }

            ObserveClassLaps(playerClassKey, classBestLap);
            if (DateTime.UtcNow >= nextSplitInspectUtc)
            {
                InspectSplits(latestRawData);
                nextSplitInspectUtc = DateTime.UtcNow.AddSeconds(1);
            }

            float myBest = player != null && player.IsValid ? ValidLap(player.BestLapTime) : 0.0f;
            float playerDist = player != null && player.IsValid ? player.LapDistancePercent : -1.0f;
            float classBestDist = -1.0f;
            if (classBestIdx >= 0)
            {
                ParticipantSnapshot cb = participants[classBestIdx];
                if (cb != null && cb.IsValid) classBestDist = cb.LapDistancePercent;
            }

            float personalDelta = ToFloat(Get(telemetry, "LapDeltaToBestLap"), 0.0f);
            bool personalOk = ToBool(Get(telemetry, "LapDeltaToBestLap_OK"), false);
            float sessionDelta = ToFloat(Get(telemetry, "LapDeltaToSessionBestLap"), 0.0f);
            bool sessionOk = ToBool(Get(telemetry, "LapDeltaToSessionBestLap_OK"), false);
            bool sameReference = classBestIdx >= 0 && classBestIdx == sessionBestIdx && classBestLap > 0.0f &&
                Math.Abs(classBestLap - sessionBestLap) <= LapTolerance;

            string status;
            if (string.IsNullOrWhiteSpace(playerClassKey)) status = "WAITING FOR CLASS INFO";
            else if (string.IsNullOrWhiteSpace(databasePlayerClass)) status = "ACTIVE - RAW CLASS FALLBACK";
            else status = "ACTIVE";
            Set("Status", status);
            Set("SessionNumber", sessionNumber);
            Set("SessionTime", sessionTime);
            Set("PlayerCarIndex", latestPlayerCarIndex);
            Set("PlayerClass", playerClass);
            Set("DatabasePlayerClass", databasePlayerClass);
            Set("RawPlayerClass", rawPlayerClass);
            Set("RawPlayerClassId", rawPlayerClassId);
            Set("RawClassSource", rawClassSource ?? string.Empty);
            Set("RawClassDriverCount", rawClassDriverCount);
            Set("ClassCarCount", classCarCount);
            Set("ClassValidBestCount", classValidBestCount);
            Set("MyBestLapText", FormatLap(myBest));
            Set("ClassBestCarIndex", classBestIdx);
            Set("ClassBestDriver", classBestDriver);
            Set("ClassBestLapText", FormatLap(classBestLap));
            Set("SessionBestCarIndex", sessionBestIdx);
            Set("SessionBestDriver", sessionBestDriver);
            Set("SessionBestLapText", FormatLap(sessionBestLap));
            Set("ClassBestIsSessionBest", sameReference);
            Set("RawPersonalDelta", (double)personalDelta);
            Set("RawPersonalDeltaOk", personalOk);
            Set("RawSessionDelta", (double)sessionDelta);
            Set("RawSessionDeltaOk", sessionOk);
            Set("PlayerLapDistPct", (double)playerDist);
            Set("ClassBestLapDistPct", (double)classBestDist);
            Set("SplitInfoFound", splitInfoFound);
            Set("SplitCount", splitCount);
            Set("SplitSummary", splitSummary ?? string.Empty);
            Set("ObservedClassLapCount", observedClassLapCount);
            Set("LastClassLapDriver", lastClassLapDriver ?? string.Empty);
            Set("LastClassLapTimeText", FormatLap(lastClassLapTime));
            Set("ClassBestLapObserved", classBestLapObserved);
            Set("ClassBestLapObservedDriver", classBestLapObservedDriver ?? string.Empty);
            Set("ClassBestLapObservedTimeText", FormatLap(classBestLapObservedTime));
        }

        private void ObserveClassLaps(string playerClassKey, float classBestLap)
        {
            for (int carIndex = 0; carIndex < SessionDatabase.Capacity; carIndex++)
            {
                ParticipantSnapshot p = participants[carIndex];
                if (p == null || !p.IsValid)
                {
                    previousLapCompleted[carIndex] = int.MinValue;
                    continue;
                }

                int completed = p.LapCompleted;
                if (previousLapCompleted[carIndex] == int.MinValue)
                {
                    previousLapCompleted[carIndex] = completed;
                    continue;
                }
                if (completed <= previousLapCompleted[carIndex])
                {
                    previousLapCompleted[carIndex] = completed;
                    continue;
                }
                previousLapCompleted[carIndex] = completed;

                DriverIdentity identity;
                bool hasIdentity = database.TryGet(carIndex, out identity) && identity != null;
                string dbClass = hasIdentity ? (identity.ClassName ?? string.Empty) : string.Empty;
                string otherClassKey = BuildClassKey(dbClass, rawClassNameByCarIndex[carIndex], rawClassIdByCarIndex[carIndex]);
                if (string.IsNullOrWhiteSpace(playerClassKey) ||
                    !string.Equals(otherClassKey, playerClassKey, StringComparison.OrdinalIgnoreCase)) continue;

                float last = ValidLap(p.LastLapTime);
                if (last <= 0.0f) continue;
                observedClassLapCount++;
                lastClassLapDriver = hasIdentity ? (identity.DriverName ?? string.Empty) : ("CarIdx " + carIndex.ToString(CultureInfo.InvariantCulture));
                lastClassLapTime = last;

                if (classBestLap > 0.0f && Math.Abs(last - classBestLap) <= LapTolerance)
                {
                    classBestLapObserved = true;
                    classBestLapObservedDriver = hasIdentity ? (identity.DriverName ?? string.Empty) : ("CarIdx " + carIndex.ToString(CultureInfo.InvariantCulture));
                    classBestLapObservedTime = last;
                }
            }
        }


        private static string BuildClassKey(string databaseClass, string rawClassName, int rawClassId)
        {
            if (rawClassId > 0) return "ID:" + rawClassId.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(databaseClass)) return "NAME:" + databaseClass.Trim();
            if (!string.IsNullOrWhiteSpace(rawClassName)) return "NAME:" + rawClassName.Trim();
            return string.Empty;
        }

        private void InspectRawClasses(object rawData)
        {
            for (int i = 0; i < rawClassNameByCarIndex.Length; i++)
            {
                rawClassNameByCarIndex[i] = string.Empty;
                rawClassIdByCarIndex[i] = -1;
            }
            rawClassSource = string.Empty;
            rawClassDriverCount = 0;

            if (rawData == null) return;

            object sessionData = Get(rawData, "SessionData");
            object sessionDataDict = Get(rawData, "SessionDataDict");
            object allSessionData = Get(rawData, "AllSessionData");

            CollectRawClassInfo(sessionData, "SessionData", 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
            CollectRawClassInfo(sessionDataDict, "SessionDataDict", 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
            CollectRawClassInfo(allSessionData, "AllSessionData", 0, new HashSet<object>(ReferenceEqualityComparer.Instance));

            int count = 0;
            for (int i = 0; i < rawClassNameByCarIndex.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(rawClassNameByCarIndex[i]) || rawClassIdByCarIndex[i] > 0) count++;
            }
            rawClassDriverCount = count;
        }

        private void CollectRawClassInfo(object value, string source, int depth, HashSet<object> visited)
        {
            if (value == null || depth > 9) return;
            Type t = value.GetType();
            if (value is string || t.IsPrimitive || t.IsEnum || value is decimal || value is DateTime || value is TimeSpan) return;

            if (!t.IsValueType)
            {
                if (visited.Contains(value)) return;
                visited.Add(value);
            }

            IDictionary dict = value as IDictionary;
            if (dict != null)
            {
                CollectClassFromDictionary(dict, source);
                int guard = 0;
                foreach (DictionaryEntry entry in dict)
                {
                    CollectRawClassInfo(entry.Value, source, depth + 1, visited);
                    if (++guard > 6000) break;
                }
                return;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                int guard = 0;
                foreach (object item in enumerable)
                {
                    CollectRawClassInfo(item, source, depth + 1, visited);
                    if (++guard > 6000) break;
                }
                return;
            }

            int carIdx = ToInt(Get(value, "CarIdx") ?? Get(value, "CarIndex"), -1);
            if (carIdx >= 0 && carIdx < SessionDatabase.Capacity)
            {
                string className = FirstText(Get(value, "CarClassShortName"), Get(value, "CarClassName"), Get(value, "ClassName"));
                int classId = ToInt(Get(value, "CarClassID") ?? Get(value, "CarClassId") ?? Get(value, "ClassID") ?? Get(value, "ClassId"), -1);
                StoreRawClass(carIdx, className, classId, source);
            }

            PropertyInfo[] props;
            try { props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public); } catch { props = new PropertyInfo[0]; }
            for (int i = 0; i < props.Length; i++)
            {
                PropertyInfo prop = props[i];
                if (!prop.CanRead || prop.GetIndexParameters().Length > 0) continue;
                string lower = (prop.Name ?? string.Empty).ToLowerInvariant();
                if (!(lower.Contains("driver") || lower.Contains("session") || lower.Contains("diction") || lower.Contains("data") || lower.Contains("class"))) continue;
                object child = null; try { child = prop.GetValue(value, null); } catch { }
                CollectRawClassInfo(child, source, depth + 1, visited);
            }

            FieldInfo[] fields;
            try { fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public); } catch { fields = new FieldInfo[0]; }
            for (int i = 0; i < fields.Length; i++)
            {
                string lower = (fields[i].Name ?? string.Empty).ToLowerInvariant();
                if (!(lower.Contains("driver") || lower.Contains("session") || lower.Contains("diction") || lower.Contains("data") || lower.Contains("class"))) continue;
                object child = null; try { child = fields[i].GetValue(value); } catch { }
                CollectRawClassInfo(child, source, depth + 1, visited);
            }
        }

        private void CollectClassFromDictionary(IDictionary dict, string source)
        {
            object carValue = GetDictionaryIgnoreCase(dict, "CarIdx", "CarIndex");
            int carIdx = ToInt(carValue, -1);
            if (carIdx >= 0 && carIdx < SessionDatabase.Capacity)
            {
                string className = FirstText(
                    GetDictionaryIgnoreCase(dict, "CarClassShortName"),
                    GetDictionaryIgnoreCase(dict, "CarClassName"),
                    GetDictionaryIgnoreCase(dict, "ClassName"));
                int classId = ToInt(GetDictionaryIgnoreCase(dict, "CarClassID", "CarClassId", "ClassID", "ClassId"), -1);
                StoreRawClass(carIdx, className, classId, source);
            }

            Dictionary<string, int> carByPrefix = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> nameByPrefix = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> idByPrefix = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (DictionaryEntry entry in dict)
            {
                string key = entry.Key != null ? entry.Key.ToString() : string.Empty;
                if (string.IsNullOrWhiteSpace(key)) continue;
                string member;
                string prefix = SplitFlattenedKey(key, out member);
                if (prefix.Length == 0 || member.Length == 0) continue;

                if (string.Equals(member, "CarIdx", StringComparison.OrdinalIgnoreCase) || string.Equals(member, "CarIndex", StringComparison.OrdinalIgnoreCase))
                {
                    int parsed = ToInt(entry.Value, -1);
                    if (parsed >= 0) carByPrefix[prefix] = parsed;
                }
                else if (string.Equals(member, "CarClassShortName", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(member, "CarClassName", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(member, "ClassName", StringComparison.OrdinalIgnoreCase))
                {
                    string parsed = entry.Value != null ? Convert.ToString(entry.Value, CultureInfo.InvariantCulture) : string.Empty;
                    if (!string.IsNullOrWhiteSpace(parsed)) nameByPrefix[prefix] = parsed;
                }
                else if (string.Equals(member, "CarClassID", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(member, "CarClassId", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(member, "ClassID", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(member, "ClassId", StringComparison.OrdinalIgnoreCase))
                {
                    int parsed = ToInt(entry.Value, -1);
                    if (parsed > 0) idByPrefix[prefix] = parsed;
                }
            }

            foreach (KeyValuePair<string, int> pair in carByPrefix)
            {
                int idx = pair.Value;
                if (idx < 0 || idx >= SessionDatabase.Capacity) continue;
                string name; nameByPrefix.TryGetValue(pair.Key, out name);
                int id; if (!idByPrefix.TryGetValue(pair.Key, out id)) id = -1;
                StoreRawClass(idx, name ?? string.Empty, id, source + "/flat");
            }
        }

        private void StoreRawClass(int carIdx, string className, int classId, string source)
        {
            if (carIdx < 0 || carIdx >= SessionDatabase.Capacity) return;
            bool changed = false;
            if (!string.IsNullOrWhiteSpace(className) && string.IsNullOrWhiteSpace(rawClassNameByCarIndex[carIdx]))
            {
                rawClassNameByCarIndex[carIdx] = className.Trim();
                changed = true;
            }
            if (classId > 0 && rawClassIdByCarIndex[carIdx] <= 0)
            {
                rawClassIdByCarIndex[carIdx] = classId;
                changed = true;
            }
            if (changed && string.IsNullOrWhiteSpace(rawClassSource)) rawClassSource = source ?? string.Empty;
        }

        private static string FirstText(params object[] values)
        {
            if (values == null) return string.Empty;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == null) continue;
                string text = Convert.ToString(values[i], CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }
            return string.Empty;
        }

        private static object GetDictionaryIgnoreCase(IDictionary dictionary, params string[] names)
        {
            if (dictionary == null || names == null) return null;
            foreach (DictionaryEntry entry in dictionary)
            {
                string key = entry.Key != null ? entry.Key.ToString() : string.Empty;
                for (int i = 0; i < names.Length; i++)
                {
                    if (string.Equals(key, names[i], StringComparison.OrdinalIgnoreCase)) return entry.Value;
                }
            }
            return null;
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

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            public new bool Equals(object x, object y) { return object.ReferenceEquals(x, y); }
            public int GetHashCode(object obj)
            {
                return obj == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }

        private void InspectSplits(object rawData)
        {
            splitInfoFound = false;
            splitCount = 0;
            splitSummary = string.Empty;

            object sessionData = Get(rawData, "SessionData") ?? Get(rawData, "AllSessionData");
            object splitInfo = Get(sessionData, "SplitTimeInfo");
            object sectorsObject = Get(splitInfo, "Sectors");
            IEnumerable sectors = sectorsObject as IEnumerable;
            if (sectors == null) return;

            StringBuilder b = new StringBuilder();
            int count = 0;
            foreach (object sector in sectors)
            {
                if (sector == null) continue;
                int num = ToInt(Get(sector, "SectorNum"), count);
                double pct = ToDouble(Get(sector, "SectorStartPct"), double.NaN);
                if (double.IsNaN(pct) || double.IsInfinity(pct)) continue;
                if (b.Length > 0) b.Append(" | ");
                b.Append("S").Append(num.ToString(CultureInfo.InvariantCulture)).Append("@").Append((pct * 100.0).ToString("0.0", CultureInfo.InvariantCulture)).Append("%");
                count++;
                if (count >= 12) break;
            }
            splitInfoFound = count > 0;
            splitCount = count;
            splitSummary = b.ToString();
        }

        private void Add(string suffix, object value, string description)
        {
            manager.AddProperty("Fulcrum.Diagnostics.Timing." + suffix, pluginType, value, description);
        }
        private void Set(string suffix, object value)
        {
            manager.SetPropertyValue("Fulcrum.Diagnostics.Timing." + suffix, pluginType, value);
        }

        private static object Get(object source, string name)
        {
            if (source == null) return null;
            IDictionary dict = source as IDictionary;
            if (dict != null && dict.Contains(name)) return dict[name];
            PropertyInfo p = source.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (p != null && p.GetIndexParameters().Length == 0)
            {
                try { return p.GetValue(source, null); } catch { }
            }
            FieldInfo f = source.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (f != null) { try { return f.GetValue(source); } catch { } }
            return null;
        }

        private static int ToInt(object v, int fallback) { try { return v == null ? fallback : Convert.ToInt32(v, CultureInfo.InvariantCulture); } catch { return fallback; } }
        private static double ToDouble(object v, double fallback) { try { return v == null ? fallback : Convert.ToDouble(v, CultureInfo.InvariantCulture); } catch { return fallback; } }
        private static float ToFloat(object v, float fallback) { try { return v == null ? fallback : Convert.ToSingle(v, CultureInfo.InvariantCulture); } catch { return fallback; } }
        private static bool ToBool(object v, bool fallback) { try { return v == null ? fallback : Convert.ToBoolean(v, CultureInfo.InvariantCulture); } catch { return fallback; } }
        private static float ValidLap(float v) { return !float.IsNaN(v) && !float.IsInfinity(v) && v > 0.0f && v < 86400.0f ? v : 0.0f; }
        private static string FormatLap(float seconds)
        {
            if (seconds <= 0.0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return "--:--.---";
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            return ((int)t.TotalMinutes).ToString("00", CultureInfo.InvariantCulture) + ":" + t.Seconds.ToString("00", CultureInfo.InvariantCulture) + "." + t.Milliseconds.ToString("000", CultureInfo.InvariantCulture);
        }
    }
}
