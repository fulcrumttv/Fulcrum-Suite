using System;
using System.Globalization;
using Fulcrum.Core.Standings;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Publishing
{
    public sealed class StandingsPublisher
    {
        private const string Prefix = "Fulcrum.Standings.";
        private readonly PluginManager pluginManager;
        private readonly Type pluginType;
        private readonly string[] rowPrefixes;

        public StandingsPublisher(PluginManager pluginManager, Type pluginType)
        {
            this.pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
            this.pluginType = pluginType ?? throw new ArgumentNullException(nameof(pluginType));
            rowPrefixes = new string[StandingsSnapshot.PublishedRowCount];
            RegisterProperties();
        }

        public void Publish(StandingsSnapshot snapshot)
        {
            Set("Ready", snapshot.Ready);
            Set("ParticipantCount", snapshot.ParticipantCount);
            Set("PublishedCount", snapshot.PublishedCount);
            Set("PlayerRow", snapshot.PlayerRow);
            Set("LeaderCarIndex", snapshot.LeaderCarIndex);
            Set("LeaderName", snapshot.LeaderName);
            Set("Error", snapshot.Error);

            for (int index = 0; index < StandingsSnapshot.PublishedRowCount; index++)
            {
                PublishRow(rowPrefixes[index], snapshot.GetRow(index));
            }
        }

        private void RegisterProperties()
        {
            Add("Ready", false, "True when the standings table contains valid data");
            Add("ParticipantCount", 0, "Number of valid classified participants");
            Add("PublishedCount", 0, "Number of standings rows currently published");
            Add("PlayerRow", 0, "One-based published row containing the player");
            Add("LeaderCarIndex", -1, "Current overall leader car index");
            Add("LeaderName", string.Empty, "Current overall leader name");
            Add("Error", string.Empty, "Latest standings engine error");

            for (int index = 0; index < StandingsSnapshot.PublishedRowCount; index++)
            {
                string row = "Row" + (index + 1).ToString("00", CultureInfo.InvariantCulture) + ".";
                rowPrefixes[index] = Prefix + row;
                AddRow(row);
            }
        }

        private void AddRow(string row)
        {
            Add(row + "HasData", false, "True when this standings row is populated");
            Add(row + "IsPlayer", false, "True when this row represents the player");
            Add(row + "IsSameClass", false, "True when this row is in the player's class");
            Add(row + "CarIndex", -1, "iRacing car index");
            Add(row + "OverallPosition", 0, "Overall race position");
            Add(row + "ClassPosition", 0, "Class race position");
            Add(row + "DriverName", string.Empty, "Driver name");
            Add(row + "CarNumber", string.Empty, "Displayed car number");
            Add(row + "TeamName", string.Empty, "Team name");
            Add(row + "ClassName", string.Empty, "Car class name");
            Add(row + "Manufacturer", string.Empty, "Vehicle manufacturer");
            Add(row + "IRating", 0, "Driver iRating");
            Add(row + "License", string.Empty, "Driver license");
            Add(row + "Lap", 0, "Current lap");
            Add(row + "LapCompleted", 0, "Completed laps");
            Add(row + "LapDifferenceToLeader", 0, "Completed-lap difference to leader");
            Add(row + "GapToLeaderSeconds", 0.0, "Estimated same-lap gap to leader");
            Add(row + "GapToLeaderText", string.Empty, "Dashboard-ready gap to leader");
            Add(row + "HasGapToLeader", false, "True when same-lap gap is available");
            Add(row + "LastLapTime", 0.0, "Last completed lap time");
            Add(row + "LastLapTimeText", string.Empty, "Formatted last lap time");
            Add(row + "BestLapTime", 0.0, "Best lap time");
            Add(row + "BestLapTimeText", string.Empty, "Formatted best lap time");
            Add(row + "TrackStatus", "NotInWorld", "Current track or pit status");
            Add(row + "IsInPits", false, "True while the participant is in pits");
        }

        private void PublishRow(string prefix, StandingsEntry row)
        {
            SetAbsolute(prefix + "HasData", row.HasData);
            SetAbsolute(prefix + "IsPlayer", row.IsPlayer);
            SetAbsolute(prefix + "IsSameClass", row.IsSameClass);
            SetAbsolute(prefix + "CarIndex", row.CarIndex);
            SetAbsolute(prefix + "OverallPosition", row.OverallPosition);
            SetAbsolute(prefix + "ClassPosition", row.ClassPosition);
            SetAbsolute(prefix + "DriverName", row.DriverName);
            SetAbsolute(prefix + "CarNumber", row.CarNumber);
            SetAbsolute(prefix + "TeamName", row.TeamName);
            SetAbsolute(prefix + "ClassName", row.ClassName);
            SetAbsolute(prefix + "Manufacturer", row.Manufacturer);
            SetAbsolute(prefix + "IRating", row.IRating);
            SetAbsolute(prefix + "License", row.License);
            SetAbsolute(prefix + "Lap", row.Lap);
            SetAbsolute(prefix + "LapCompleted", row.LapCompleted);
            SetAbsolute(prefix + "LapDifferenceToLeader", row.LapDifferenceToLeader);
            SetAbsolute(prefix + "GapToLeaderSeconds", (double)row.GapToLeaderSeconds);
            SetAbsolute(prefix + "GapToLeaderText", FormatGap(row));
            SetAbsolute(prefix + "HasGapToLeader", row.HasGapToLeader);
            SetAbsolute(prefix + "LastLapTime", (double)row.LastLapTime);
            SetAbsolute(prefix + "LastLapTimeText", FormatLapTime(row.LastLapTime));
            SetAbsolute(prefix + "BestLapTime", (double)row.BestLapTime);
            SetAbsolute(prefix + "BestLapTimeText", FormatLapTime(row.BestLapTime));
            SetAbsolute(prefix + "TrackStatus", row.TrackStatus);
            SetAbsolute(prefix + "IsInPits", row.IsInPits);
        }

        private static string FormatGap(StandingsEntry row)
        {
            if (!row.HasData) return string.Empty;
            if (row.OverallPosition == 1) return "LEADER";
            if (row.LapDifferenceToLeader < 0)
            {
                return row.LapDifferenceToLeader.ToString(CultureInfo.InvariantCulture) + "L";
            }
            if (!row.HasGapToLeader) return "--.-";
            return "+" + row.GapToLeaderSeconds.ToString("0.000", CultureInfo.InvariantCulture);
        }

        private static string FormatLapTime(float seconds)
        {
            if (seconds <= 0.0f || float.IsNaN(seconds) || float.IsInfinity(seconds))
            {
                return string.Empty;
            }

            int minutes = (int)(seconds / 60.0f);
            float remaining = seconds - minutes * 60.0f;
            return minutes.ToString(CultureInfo.InvariantCulture) + ":" +
                   remaining.ToString("00.000", CultureInfo.InvariantCulture);
        }

        private void Add(string name, object defaultValue, string description)
        {
            pluginManager.AddProperty(Prefix + name, pluginType, defaultValue, description);
        }

        private void Set(string name, object value)
        {
            pluginManager.SetPropertyValue(Prefix + name, pluginType, value);
        }

        private void SetAbsolute(string name, object value)
        {
            pluginManager.SetPropertyValue(name, pluginType, value);
        }
    }
}
