using System;
using System.Globalization;
using Fulcrum.Core.Relative.Display;
using Fulcrum.Plugin.Modules;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Publishing
{
    /// <summary>
    /// Publishes display-ready Relative fields. v0.6.6 adds formatted gaps,
    /// positions, lap relationships, class matching and compact row states.
    /// </summary>
    internal sealed class RelativeDisplayPublisher
    {
        private const int PublishedSlotCount = 4; // Keep public property surface backward-compatible.

        private readonly PluginManager pluginManager;
        private readonly Type pluginType;
        private readonly string[][] aheadProperties;
        private readonly string[][] behindProperties;

        private static readonly string[] RowFieldNames =
        {
            "HasData", "IsPlayer", "CarIndex", "OverallPosition", "OverallPositionText",
            "ClassPosition", "ClassPositionText", "Lap", "LapDistancePercent",
            "LapDifference", "RelativeDistanceLaps", "GapSeconds", "GapText", "HasGap",
            "GapLiveSeconds", "GapLiveText", "HasLiveGap", "LastLapTimeSeconds",
            "LastLapTimeText", "StintLap", "StintText", "IsOutLap", "IsTowing",
            "DriverName", "CarNumber", "TeamName", "ClassName", "Manufacturer",
            "IRating", "License", "IsSameClass", "IsLappedByPlayer", "IsAheadByLap",
            "TrackSurface", "TrackStatus", "StatusText", "RowState", "IsOnTrack",
            "IsOffTrack", "IsInPitStall", "IsApproachingPits", "IsInPits"
        };

        private static readonly string[] PlayerFieldNames =
        {
            "HasData", "IsPlayer", "CarIndex", "OverallPosition", "OverallPositionText",
            "ClassPosition", "ClassPositionText", "Lap", "LapDistancePercent",
            "DriverName", "CarNumber", "TeamName", "ClassName", "Manufacturer",
            "IRating", "License", "TrackSurface", "TrackStatus", "StatusText", "RowState",
            "IsOnTrack", "IsOffTrack", "IsInPitStall", "IsApproachingPits", "IsInPits",
            "IsOnPitRoad", "LastLapTimeSeconds", "LastLapTimeText", "StintLap", "StintText",
            "IsOutLap", "IsTowing"
        };

        public RelativeDisplayPublisher(PluginManager pluginManager, Type pluginType)
        {
            if (pluginManager == null) throw new ArgumentNullException(nameof(pluginManager));
            if (pluginType == null) throw new ArgumentNullException(nameof(pluginType));

            this.pluginManager = pluginManager;
            this.pluginType = pluginType;
            aheadProperties = CreatePropertyMatrix();
            behindProperties = CreatePropertyMatrix();
            BuildPropertyNames();
            RegisterProperties();
        }

        public void Publish(RelativeDisplaySnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            PublishPlayer(snapshot.Player);
            pluginManager.SetPropertyValue(RelativePropertyNames.DisplayAheadCount, pluginType, snapshot.AheadCount);
            pluginManager.SetPropertyValue(RelativePropertyNames.DisplayBehindCount, pluginType, snapshot.BehindCount);

            string playerClass = snapshot.Player == null ? string.Empty : snapshot.Player.ClassName;
            for (int index = 0; index < PublishedSlotCount; index++)
            {
                PublishRow(snapshot.GetAhead(index), aheadProperties[index], playerClass);
                PublishRow(snapshot.GetBehind(index), behindProperties[index], playerClass);
            }
        }

        private static string[][] CreatePropertyMatrix()
        {
            string[][] properties = new string[PublishedSlotCount][];
            for (int index = 0; index < PublishedSlotCount; index++) properties[index] = new string[RowFieldNames.Length];
            return properties;
        }

        private void BuildPropertyNames()
        {
            for (int slot = 0; slot < PublishedSlotCount; slot++)
            {
                for (int field = 0; field < RowFieldNames.Length; field++)
                {
                    aheadProperties[slot][field] = RelativePropertyNames.Ahead(slot, RowFieldNames[field]);
                    behindProperties[slot][field] = RelativePropertyNames.Behind(slot, RowFieldNames[field]);
                }
            }
        }

        private void RegisterProperties()
        {
            RegisterPlayerProperties();
            pluginManager.AddProperty(RelativePropertyNames.DisplayAheadCount, pluginType, 0, "Number of populated Relative display rows ahead");
            pluginManager.AddProperty(RelativePropertyNames.DisplayBehindCount, pluginType, 0, "Number of populated Relative display rows behind");

            for (int index = 0; index < PublishedSlotCount; index++)
            {
                RegisterRowProperties(aheadProperties[index], "Relative row ahead");
                RegisterRowProperties(behindProperties[index], "Relative row behind");
            }
        }

        private void RegisterPlayerProperties()
        {
            for (int index = 0; index < PlayerFieldNames.Length; index++)
            {
                string fieldName = PlayerFieldNames[index];
                pluginManager.AddProperty(RelativePropertyNames.Player(fieldName), pluginType, GetDefaultValue(fieldName), "Player Relative field " + fieldName);
            }
        }

        private void RegisterRowProperties(string[] properties, string description)
        {
            for (int index = 0; index < RowFieldNames.Length; index++)
            {
                pluginManager.AddProperty(properties[index], pluginType, GetDefaultValue(RowFieldNames[index]), description + " " + RowFieldNames[index]);
            }
        }

        private static object GetDefaultValue(string fieldName)
        {
            switch (fieldName)
            {
                case "HasData": case "IsPlayer": case "HasGap": case "IsSameClass":
                case "IsLappedByPlayer": case "IsAheadByLap": case "IsOnTrack":
                case "IsOffTrack": case "IsInPitStall": case "IsApproachingPits": case "IsInPits":
                case "HasLiveGap": case "IsOutLap": case "IsTowing": case "IsOnPitRoad":
                    return false;
                case "CarIndex": return -1;
                case "OverallPosition": case "ClassPosition": case "Lap": case "LapDifference": case "IRating":
                case "StintLap": return 0;
                case "TrackSurface": return RelativeTrackStatus.NotInWorld;
                case "LapDistancePercent": case "RelativeDistanceLaps": case "GapSeconds":
                case "GapLiveSeconds": case "LastLapTimeSeconds": return 0.0;
                default: return string.Empty;
            }
        }

        private void PublishPlayer(RelativeDisplayEntry entry)
        {
            bool has = entry != null && entry.HasData;
            SetPlayer("HasData", has);
            SetPlayer("IsPlayer", has && entry.IsPlayer);
            SetPlayer("CarIndex", has ? entry.CarIndex : -1);
            SetPlayer("OverallPosition", has ? entry.OverallPosition : 0);
            SetPlayer("OverallPositionText", PositionText(has ? entry.OverallPosition : 0));
            SetPlayer("ClassPosition", has ? entry.ClassPosition : 0);
            SetPlayer("ClassPositionText", PositionText(has ? entry.ClassPosition : 0));
            SetPlayer("Lap", has ? entry.Lap : 0);
            SetPlayer("LapDistancePercent", has ? (double)entry.LapDistancePercent : 0.0);
            SetPlayer("DriverName", Text(entry, 0));
            SetPlayer("CarNumber", Text(entry, 1));
            SetPlayer("TeamName", Text(entry, 2));
            SetPlayer("ClassName", Text(entry, 3));
            SetPlayer("Manufacturer", Text(entry, 4));
            SetPlayer("IRating", has ? entry.IRating : 0);
            SetPlayer("License", Text(entry, 5));
            SetPlayer("TrackSurface", has ? entry.TrackSurface : RelativeTrackStatus.NotInWorld);
            SetPlayer("TrackStatus", has ? entry.TrackStatus : "NotInWorld");
            SetPlayer("StatusText", StatusText(entry));
            SetPlayer("RowState", RowState(entry, true, true));
            SetPlayer("IsOnTrack", has && entry.IsOnTrack);
            SetPlayer("IsOffTrack", has && entry.IsOffTrack);
            SetPlayer("IsInPitStall", has && entry.IsInPitStall);
            SetPlayer("IsApproachingPits", has && entry.IsApproachingPits);
            SetPlayer("IsInPits", has && entry.IsInPits);
            SetPlayer("IsOnPitRoad", has && entry.IsOnPitRoad);
            SetPlayer("LastLapTimeSeconds", has ? (double)entry.LastLapTimeSeconds : 0.0);
            SetPlayer("LastLapTimeText", LapTimeText(entry));
            SetPlayer("StintLap", has ? entry.StintLap : 0);
            SetPlayer("StintText", StintText(entry));
            SetPlayer("IsOutLap", has && entry.IsOutLap);
            SetPlayer("IsTowing", has && entry.IsTowing);
        }

        private void PublishRow(RelativeDisplayEntry entry, string[] properties, string playerClass)
        {
            bool has = entry != null && entry.HasData;
            bool sameClass = has && !string.IsNullOrWhiteSpace(playerClass) && string.Equals(playerClass, entry.ClassName, StringComparison.OrdinalIgnoreCase);
            int i = 0;
            Set(properties, i++, has);
            Set(properties, i++, has && entry.IsPlayer);
            Set(properties, i++, has ? entry.CarIndex : -1);
            Set(properties, i++, has ? entry.OverallPosition : 0);
            Set(properties, i++, PositionText(has ? entry.OverallPosition : 0));
            Set(properties, i++, has ? entry.ClassPosition : 0);
            Set(properties, i++, PositionText(has ? entry.ClassPosition : 0));
            Set(properties, i++, has ? entry.Lap : 0);
            Set(properties, i++, has ? (double)entry.LapDistancePercent : 0.0);
            Set(properties, i++, has ? entry.LapDifference : 0);
            Set(properties, i++, has ? (double)entry.RelativeDistanceLaps : 0.0);
            Set(properties, i++, has ? (double)entry.GapSeconds : 0.0);
            Set(properties, i++, GapText(entry, false));
            Set(properties, i++, has && entry.HasGap);
            Set(properties, i++, has ? (double)entry.GapLiveSeconds : 0.0);
            Set(properties, i++, GapText(entry, true));
            Set(properties, i++, has && entry.HasLiveGap);
            Set(properties, i++, has ? (double)entry.LastLapTimeSeconds : 0.0);
            Set(properties, i++, LapTimeText(entry));
            Set(properties, i++, has ? entry.StintLap : 0);
            Set(properties, i++, StintText(entry));
            Set(properties, i++, has && entry.IsOutLap);
            Set(properties, i++, has && entry.IsTowing);
            Set(properties, i++, Text(entry, 0));
            Set(properties, i++, Text(entry, 1));
            Set(properties, i++, Text(entry, 2));
            Set(properties, i++, Text(entry, 3));
            Set(properties, i++, Text(entry, 4));
            Set(properties, i++, has ? entry.IRating : 0);
            Set(properties, i++, Text(entry, 5));
            Set(properties, i++, sameClass);
            Set(properties, i++, has && entry.LapDifference < 0);
            Set(properties, i++, has && entry.LapDifference > 0);
            Set(properties, i++, has ? entry.TrackSurface : RelativeTrackStatus.NotInWorld);
            Set(properties, i++, has ? entry.TrackStatus : "NotInWorld");
            Set(properties, i++, StatusText(entry));
            Set(properties, i++, RowState(entry, false, sameClass));
            Set(properties, i++, has && entry.IsOnTrack);
            Set(properties, i++, has && entry.IsOffTrack);
            Set(properties, i++, has && entry.IsInPitStall);
            Set(properties, i++, has && entry.IsApproachingPits);
            Set(properties, i++, has && entry.IsInPits);
        }

        private static string GapText(RelativeDisplayEntry entry, bool live)
        {
            if (entry == null || !entry.HasData) return string.Empty;
            if (entry.LapDifference > 0) return "+" + entry.LapDifference.ToString(CultureInfo.InvariantCulture) + "L";
            if (entry.LapDifference < 0) return entry.LapDifference.ToString(CultureInfo.InvariantCulture) + "L";
            if (live && !entry.HasLiveGap) return "--.-";
            if (!live && !entry.HasGap) return "--.-";
            float value = live ? entry.GapLiveSeconds : entry.GapSeconds;
            string sign = value > 0.0005f ? "+" : string.Empty;
            return sign + value.ToString("0.000", CultureInfo.InvariantCulture);
        }

        private static string LapTimeText(RelativeDisplayEntry entry)
        {
            if (entry == null || entry.LastLapTimeSeconds <= 0.0f) return "--:--.---";
            int minutes = (int)(entry.LastLapTimeSeconds / 60.0f);
            float seconds = entry.LastLapTimeSeconds - minutes * 60.0f;
            return minutes.ToString(CultureInfo.InvariantCulture) + ":" +
                seconds.ToString("00.000", CultureInfo.InvariantCulture);
        }

        private static string StintText(RelativeDisplayEntry entry)
        {
            if (entry == null || !entry.HasData) return string.Empty;
            if (entry.IsOutLap) return "OUT";
            return entry.StintLap > 0 ? entry.StintLap.ToString(CultureInfo.InvariantCulture) : "--";
        }

        private static string PositionText(int position)
        {
            return position > 0 ? "P" + position.ToString(CultureInfo.InvariantCulture) : string.Empty;
        }

        private static string StatusText(RelativeDisplayEntry entry)
        {
            if (entry == null || !entry.HasData) return string.Empty;
            if (entry.IsTowing) return "TOW";
            if (entry.IsOutLap) return "OUT";
            if (entry.IsInPits || entry.IsOnPitRoad || entry.IsInPitStall || entry.IsApproachingPits) return "PIT";
            return string.Empty;
        }

        private static string RowState(RelativeDisplayEntry entry, bool isPlayer, bool sameClass)
        {
            if (entry == null || !entry.HasData) return "Empty";
            if (isPlayer) return "Player";
            if (entry.IsInPits) return "Pits";
            if (entry.IsOffTrack) return "OffTrack";
            if (!entry.IsOnTrack) return "NotInWorld";
            if (entry.LapDifference < 0) return "Lapped";
            if (entry.LapDifference > 0) return "LapAhead";
            return sameClass ? "SameClass" : "OtherClass";
        }

        private static string Text(RelativeDisplayEntry entry, int field)
        {
            if (entry == null) return string.Empty;
            switch (field)
            {
                case 0: return entry.DriverName ?? string.Empty;
                case 1: return entry.CarNumber ?? string.Empty;
                case 2: return entry.TeamName ?? string.Empty;
                case 3: return entry.ClassName ?? string.Empty;
                case 4: return entry.Manufacturer ?? string.Empty;
                case 5: return entry.License ?? string.Empty;
                default: return string.Empty;
            }
        }

        private void SetPlayer(string fieldName, object value)
        {
            pluginManager.SetPropertyValue(RelativePropertyNames.Player(fieldName), pluginType, value);
        }

        private void Set(string[] properties, int index, object value)
        {
            pluginManager.SetPropertyValue(properties[index], pluginType, value);
        }
    }
}
