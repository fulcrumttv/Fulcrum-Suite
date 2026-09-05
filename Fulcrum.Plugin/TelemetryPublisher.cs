using System;
using Fulcrum.Core.Telemetry;
using SimHub.Plugins;

namespace Fulcrum.Plugin
{
    public class TelemetryPublisher
    {
        private readonly PluginManager pluginManager;
        private readonly Type pluginType;

        public TelemetryPublisher(PluginManager pluginManager, Type pluginType)
        {
            this.pluginManager = pluginManager;
            this.pluginType = pluginType;
        }

        public void RegisterProperties()
        {
            Add("Fulcrum.Telemetry.SessionType", string.Empty, "Current iRacing session type");
            Add("Fulcrum.Telemetry.SessionTime", 0.0, "Elapsed session time in seconds");
            Add("Fulcrum.Telemetry.SessionTimeRemaining", 0.0, "Remaining session time in seconds");
            Add("Fulcrum.Telemetry.SessionTimeText", "00:00", "Formatted elapsed session time");
            Add("Fulcrum.Telemetry.SessionTimeRemainingText", "00:00", "Formatted remaining session time");
            Add("Fulcrum.Telemetry.SessionProgressPercent", 0.0, "Timed-session progress from 0 to 100");
            Add("Fulcrum.Telemetry.IsTimedSession", false, "True when usable session timing data is available");
            Add("Fulcrum.Telemetry.IsLapLimitedSession", false, "True when a scheduled lap total is available");

            Add("Fulcrum.SessionState.Number", -1, "Current iRacing session number");
            Add("Fulcrum.SessionState.StateCode", 0, "Raw iRacing session state");
            Add("Fulcrum.SessionState.State", "Invalid", "Readable session state");
            Add("Fulcrum.SessionState.FlagsRaw", 0L, "Raw iRacing session flags bitmask");
            Add("Fulcrum.SessionState.PrimaryFlag", "None", "Highest priority active flag");
            Add("Fulcrum.SessionState.LapsRemaining", 0, "Remaining session laps");
            Add("Fulcrum.SessionState.LapsTotal", 0, "Total scheduled session laps");
            Add("Fulcrum.SessionState.LapProgressPercent", 0.0, "Lap-limited session progress from 0 to 100");
            Add("Fulcrum.SessionState.LapsCompleted", 0, "Derived completed session laps");
            Add("Fulcrum.SessionState.IsRacing", false, "True while the session is racing");
            Add("Fulcrum.SessionState.IsFinished", false, "True after checkered or cooldown");
            Add("Fulcrum.SessionState.Flag.Green", false, "Green flag active");
            Add("Fulcrum.SessionState.Flag.Yellow", false, "Yellow or caution flag active");
            Add("Fulcrum.SessionState.Flag.Red", false, "Red flag active");
            Add("Fulcrum.SessionState.Flag.Blue", false, "Blue flag active");
            Add("Fulcrum.SessionState.Flag.White", false, "White flag active");
            Add("Fulcrum.SessionState.Flag.Checkered", false, "Checkered flag active");
            Add("Fulcrum.SessionState.Flag.Black", false, "Black flag active");
            Add("Fulcrum.SessionState.Flag.OneLapToGreen", false, "One lap to green flag active");
            Add("Fulcrum.SessionState.Flag.YellowLocal", false, "Local yellow bit active");
            Add("Fulcrum.SessionState.Flag.YellowWaving", false, "Waving yellow bit active");
            Add("Fulcrum.SessionState.Flag.Caution", false, "Full-course caution bit active");
            Add("Fulcrum.SessionState.Flag.CautionWaving", false, "Caution-waving bit active");
            Add("Fulcrum.SessionState.Flag.Debris", false, "Debris flag bit active");
            Add("Fulcrum.SessionState.Flag.Repair", false, "Repair/meatball bit active");
            Add("Fulcrum.SessionState.Flag.Disqualify", false, "Disqualification bit active");
            Add("Fulcrum.SessionState.Flag.FurledBlack", false, "Furled black warning bit active");
            Add("Fulcrum.SessionState.Flag.StartReady", false, "Start ready bit active");
            Add("Fulcrum.SessionState.Flag.StartSet", false, "Start set bit active");
            Add("Fulcrum.SessionState.Flag.StartGo", false, "Start go bit active");

            Add("Fulcrum.Telemetry.PlayerCarIndex", -1, "Player car index");
            Add("Fulcrum.Telemetry.PlayerPosition", -1, "Player overall position");
            Add("Fulcrum.Telemetry.PlayerClassPosition", -1, "Player class position");
            Add("Fulcrum.Telemetry.PlayerClassId", -1, "Player class identifier");
            Add("Fulcrum.Telemetry.Lap", 0, "Current lap");
            Add("Fulcrum.Telemetry.LapCompleted", 0, "Completed laps");
            Add("Fulcrum.Telemetry.LapDistancePercent", 0.0f, "Current lap distance from 0 to 1");
            Add("Fulcrum.Telemetry.SpeedMetersPerSecond", 0.0f, "Vehicle speed in meters per second");
            Add("Fulcrum.Telemetry.SpeedKmh", 0.0f, "Vehicle speed in kilometers per hour");
            Add("Fulcrum.Telemetry.Throttle", 0.0f, "Throttle input from 0 to 1");
            Add("Fulcrum.Telemetry.Brake", 0.0f, "Brake input from 0 to 1");
            Add("Fulcrum.Telemetry.Clutch", 0.0f, "Clutch input from 0 to 1");
            Add("Fulcrum.Telemetry.Gear", 0, "Current gear");
            Add("Fulcrum.Telemetry.Rpm", 0.0f, "Current engine RPM");
            Add("Fulcrum.Telemetry.IsOnTrack", false, "True while the player car is on track");
            Add("Fulcrum.Telemetry.IsOnPitRoad", false, "True while the player car is on pit road");
            Add("Fulcrum.Telemetry.IsReplayPlaying", false, "True while an iRacing replay is playing");
            Add("Fulcrum.Telemetry.TrackTemperatureCelsius", 0.0f, "Track temperature in Celsius");
            Add("Fulcrum.Telemetry.AirTemperatureCelsius", 0.0f, "Air temperature in Celsius");
        }

        public void Publish(TelemetrySnapshot snapshot)
        {
            if (snapshot == null) return;

            bool isTimed = snapshot.SessionTime >= 0.0 && snapshot.SessionTimeRemaining > 0.0;
            double totalTime = isTimed ? snapshot.SessionTime + snapshot.SessionTimeRemaining : 0.0;
            double timeProgress = totalTime > 0.0 ? ClampPercent(snapshot.SessionTime / totalTime * 100.0) : 0.0;
            bool isLapLimited = snapshot.SessionLapsTotal > 0;
            int lapsCompleted = isLapLimited ? Math.Max(0, snapshot.SessionLapsTotal - Math.Max(0, snapshot.SessionLapsRemaining)) : 0;
            double lapProgress = isLapLimited ? ClampPercent((double)lapsCompleted / snapshot.SessionLapsTotal * 100.0) : 0.0;

            Set("Fulcrum.Telemetry.SessionType", snapshot.SessionType);
            Set("Fulcrum.Telemetry.SessionTime", snapshot.SessionTime);
            Set("Fulcrum.Telemetry.SessionTimeRemaining", snapshot.SessionTimeRemaining);
            Set("Fulcrum.Telemetry.SessionTimeText", FormatDuration(snapshot.SessionTime));
            Set("Fulcrum.Telemetry.SessionTimeRemainingText", FormatDuration(snapshot.SessionTimeRemaining));
            Set("Fulcrum.Telemetry.SessionProgressPercent", timeProgress);
            Set("Fulcrum.Telemetry.IsTimedSession", isTimed);
            Set("Fulcrum.Telemetry.IsLapLimitedSession", isLapLimited);

            Set("Fulcrum.SessionState.Number", snapshot.SessionNumber);
            Set("Fulcrum.SessionState.StateCode", snapshot.SessionState);
            Set("Fulcrum.SessionState.State", SessionStateInterpreter.GetSessionStateName(snapshot.SessionState));
            Set("Fulcrum.SessionState.FlagsRaw", snapshot.SessionFlags);
            Set("Fulcrum.SessionState.PrimaryFlag", SessionStateInterpreter.GetPrimaryFlag(snapshot.SessionFlags));
            Set("Fulcrum.SessionState.LapsRemaining", snapshot.SessionLapsRemaining);
            Set("Fulcrum.SessionState.LapsTotal", snapshot.SessionLapsTotal);
            Set("Fulcrum.SessionState.LapsCompleted", lapsCompleted);
            Set("Fulcrum.SessionState.LapProgressPercent", lapProgress);
            Set("Fulcrum.SessionState.IsRacing", SessionStateInterpreter.IsRacing(snapshot.SessionState));
            Set("Fulcrum.SessionState.IsFinished", SessionStateInterpreter.IsFinished(snapshot.SessionState, snapshot.SessionFlags));
            Set("Fulcrum.SessionState.Flag.Green", SessionStateInterpreter.HasGreen(snapshot.SessionFlags));
            Set("Fulcrum.SessionState.Flag.Yellow", SessionStateInterpreter.HasYellow(snapshot.SessionFlags));
            Set("Fulcrum.SessionState.Flag.Red", SessionStateInterpreter.HasRed(snapshot.SessionFlags));
            Set("Fulcrum.SessionState.Flag.Blue", SessionStateInterpreter.HasBlue(snapshot.SessionFlags));
            Set("Fulcrum.SessionState.Flag.White", SessionStateInterpreter.HasWhite(snapshot.SessionFlags));
            Set("Fulcrum.SessionState.Flag.Checkered", SessionStateInterpreter.HasCheckered(snapshot.SessionFlags));
            Set("Fulcrum.SessionState.Flag.Black", SessionStateInterpreter.HasBlack(snapshot.SessionFlags));
            Set("Fulcrum.SessionState.Flag.OneLapToGreen", SessionStateInterpreter.HasOneLapToGreen(snapshot.SessionFlags));
            Set("Fulcrum.SessionState.Flag.YellowLocal", HasRawFlag(snapshot.SessionFlags, 0x00000008L));
            Set("Fulcrum.SessionState.Flag.YellowWaving", HasRawFlag(snapshot.SessionFlags, 0x00000100L));
            Set("Fulcrum.SessionState.Flag.Caution", HasRawFlag(snapshot.SessionFlags, 0x00004000L));
            Set("Fulcrum.SessionState.Flag.CautionWaving", HasRawFlag(snapshot.SessionFlags, 0x00008000L));
            Set("Fulcrum.SessionState.Flag.Debris", HasRawFlag(snapshot.SessionFlags, 0x00000040L));
            Set("Fulcrum.SessionState.Flag.Repair", SessionStateInterpreter.HasRepair(snapshot.SessionFlags));
            Set("Fulcrum.SessionState.Flag.Disqualify", SessionStateInterpreter.HasDisqualify(snapshot.SessionFlags));
            Set("Fulcrum.SessionState.Flag.FurledBlack", HasRawFlag(snapshot.SessionFlags, 0x00080000L));
            Set("Fulcrum.SessionState.Flag.StartReady", HasRawFlag(snapshot.SessionFlags, 0x20000000L));
            Set("Fulcrum.SessionState.Flag.StartSet", HasRawFlag(snapshot.SessionFlags, 0x40000000L));
            Set("Fulcrum.SessionState.Flag.StartGo", HasRawFlag(snapshot.SessionFlags, 0x80000000L));

            Set("Fulcrum.Telemetry.PlayerCarIndex", snapshot.PlayerCarIndex);
            Set("Fulcrum.Telemetry.PlayerPosition", snapshot.PlayerPosition);
            Set("Fulcrum.Telemetry.PlayerClassPosition", snapshot.PlayerClassPosition);
            Set("Fulcrum.Telemetry.PlayerClassId", snapshot.PlayerClassId);
            Set("Fulcrum.Telemetry.Lap", snapshot.Lap);
            Set("Fulcrum.Telemetry.LapCompleted", snapshot.LapCompleted);
            Set("Fulcrum.Telemetry.LapDistancePercent", snapshot.LapDistancePercent);
            Set("Fulcrum.Telemetry.SpeedMetersPerSecond", snapshot.SpeedMetersPerSecond);
            Set("Fulcrum.Telemetry.SpeedKmh", snapshot.SpeedMetersPerSecond * 3.6f);
            Set("Fulcrum.Telemetry.Throttle", snapshot.Throttle);
            Set("Fulcrum.Telemetry.Brake", snapshot.Brake);
            Set("Fulcrum.Telemetry.Clutch", snapshot.Clutch);
            Set("Fulcrum.Telemetry.Gear", snapshot.Gear);
            Set("Fulcrum.Telemetry.Rpm", snapshot.Rpm);
            Set("Fulcrum.Telemetry.IsOnTrack", snapshot.IsOnTrack);
            Set("Fulcrum.Telemetry.IsOnPitRoad", snapshot.IsOnPitRoad);
            Set("Fulcrum.Telemetry.IsReplayPlaying", snapshot.IsReplayPlaying);
            Set("Fulcrum.Telemetry.TrackTemperatureCelsius", snapshot.TrackTemperatureCelsius);
            Set("Fulcrum.Telemetry.AirTemperatureCelsius", snapshot.AirTemperatureCelsius);
        }

        private void Add(string name, object defaultValue, string description)
        {
            pluginManager.AddProperty(name, pluginType, defaultValue, description);
        }

        private void Set(string propertyName, object value)
        {
            pluginManager.SetPropertyValue(propertyName, pluginType, value);
        }

        // These extended iRacing flag bits are decoded locally so this plugin remains
        // binary-compatible with the user's current Fulcrum.Core.dll.
        private static bool HasRawFlag(long flags, long mask)
        {
            return (flags & mask) != 0;
        }

        private static double ClampPercent(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 0.0;
            if (value < 0.0) return 0.0;
            if (value > 100.0) return 100.0;
            return value;
        }

        private static string FormatDuration(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0.0) return "--:--";
            TimeSpan duration = TimeSpan.FromSeconds(seconds);
            if (duration.TotalHours >= 1.0)
            {
                return ((int)duration.TotalHours).ToString("00") + ":" + duration.Minutes.ToString("00") + ":" + duration.Seconds.ToString("00");
            }
            return duration.Minutes.ToString("00") + ":" + duration.Seconds.ToString("00");
        }
    }
}
