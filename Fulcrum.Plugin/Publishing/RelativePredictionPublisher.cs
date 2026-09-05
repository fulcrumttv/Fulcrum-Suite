using System;
using Fulcrum.Core.Relative.Prediction;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Publishing
{
    internal sealed class RelativePredictionPublisher
    {
        private const string Root = "Fulcrum.Relative.Prediction.";

        private readonly PluginManager pluginManager;
        private readonly Type pluginType;

        public RelativePredictionPublisher(
            PluginManager pluginManager,
            Type pluginType)
        {
            if (pluginManager == null)
            {
                throw new ArgumentNullException(nameof(pluginManager));
            }

            if (pluginType == null)
            {
                throw new ArgumentNullException(nameof(pluginType));
            }

            this.pluginManager = pluginManager;
            this.pluginType = pluginType;

            RegisterProperties();
        }

        public void Publish(RelativePredictionSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            Set("Ready", snapshot.Ready);

            Set("Ahead.CarIndex", snapshot.AheadCarIndex);
            Set("Ahead.GapSeconds", snapshot.AheadGapSeconds);
            Set("Ahead.ClosingRate", snapshot.AheadClosingRate);
            Set("Ahead.TimeToCatchSeconds", snapshot.AheadTimeToCatchSeconds);
            Set("Ahead.TimeToCatchText", FormatTimeToCatch(snapshot.AheadTimeToCatchSeconds));
            Set("Ahead.IsCatching", snapshot.IsCatchingAhead);
            Set("Ahead.IsBattle", snapshot.BattleAhead);

            Set("Behind.CarIndex", snapshot.BehindCarIndex);
            Set("Behind.GapSeconds", snapshot.BehindGapSeconds);
            Set("Behind.ClosingRate", snapshot.BehindClosingRate);
            Set("Behind.TimeToCatchSeconds", snapshot.BehindTimeToCatchSeconds);
            Set("Behind.TimeToCatchText", FormatTimeToCatch(snapshot.BehindTimeToCatchSeconds));
            Set("Behind.IsClosing", snapshot.IsBeingCaught);
            Set("Behind.IsBattle", snapshot.BattleBehind);

            Set("PressureLevel", snapshot.PressureLevel);
            Set("BattleState", snapshot.BattleState);
            Set("Recommendation", snapshot.Recommendation);
            Set("Summary", snapshot.Summary);
        }

        private void RegisterProperties()
        {
            Add("Ready", false, "True when predictive Relative data is available");

            Add("Ahead.CarIndex", -1, "Nearest predicted car ahead");
            Add("Ahead.GapSeconds", 0.0, "Current absolute gap ahead");
            Add("Ahead.ClosingRate", 0.0, "Gap change rate ahead in seconds per second");
            Add("Ahead.TimeToCatchSeconds", 0.0, "Estimated time until catching the car ahead");
            Add("Ahead.TimeToCatchText", string.Empty, "Formatted time until catching the car ahead");
            Add("Ahead.IsCatching", false, "True when the player is catching the car ahead");
            Add("Ahead.IsBattle", false, "True when the car ahead is in the battle zone");

            Add("Behind.CarIndex", -1, "Nearest predicted car behind");
            Add("Behind.GapSeconds", 0.0, "Current absolute gap behind");
            Add("Behind.ClosingRate", 0.0, "Gap change rate behind in seconds per second");
            Add("Behind.TimeToCatchSeconds", 0.0, "Estimated time until the car behind catches the player");
            Add("Behind.TimeToCatchText", string.Empty, "Formatted time until the car behind catches the player");
            Add("Behind.IsClosing", false, "True when the car behind is closing");
            Add("Behind.IsBattle", false, "True when the car behind is in the battle zone");

            Add("PressureLevel", "None", "Rear pressure classification");
            Add("BattleState", "Clear", "Current nearby battle state");
            Add("Recommendation", "Maintain pace", "Predictive Relative recommendation");
            Add("Summary", "No nearby battle", "Predictive Relative summary");
        }

        private void Add(
            string suffix,
            object defaultValue,
            string description)
        {
            pluginManager.AddProperty(
                Root + suffix,
                pluginType,
                defaultValue,
                description);
        }

        private void Set(
            string suffix,
            object value)
        {
            pluginManager.SetPropertyValue(
                Root + suffix,
                pluginType,
                value);
        }

        private static string FormatTimeToCatch(double seconds)
        {
            if (seconds <= 0.0 ||
                double.IsNaN(seconds) ||
                double.IsInfinity(seconds))
            {
                return "--.-";
            }

            if (seconds < 10.0)
            {
                return seconds.ToString("0.0") + "s";
            }

            if (seconds < 100.0)
            {
                return seconds.ToString("0") + "s";
            }

            return "99s+";
        }
    }
}
