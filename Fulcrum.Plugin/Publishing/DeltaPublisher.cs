using System;
using Fulcrum.Core.Delta;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Publishing
{
    public sealed class DeltaPublisher
    {
        private readonly PluginManager pluginManager;
        private readonly Type pluginType;

        public DeltaPublisher(PluginManager pluginManager, Type pluginType)
        {
            this.pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
            this.pluginType = pluginType ?? throw new ArgumentNullException(nameof(pluginType));
            RegisterProperties();
        }

        public void Publish(DeltaReader reader, DeltaSnapshot snapshot)
        {
            Set("Fulcrum.Delta.Ready", reader != null && reader.HasTelemetry && snapshot.Ready);
            Set("Fulcrum.Delta.Error", reader == null ? string.Empty : reader.Error);
            Set("Fulcrum.Delta.IsValid", snapshot.IsValid);
            Set("Fulcrum.Delta.Reference", snapshot.Reference);
            Set("Fulcrum.Delta.RawSeconds", snapshot.RawDeltaSeconds);
            Set("Fulcrum.Delta.Seconds", snapshot.DeltaSeconds);
            Set("Fulcrum.Delta.Text", snapshot.DeltaText);
            Set("Fulcrum.Delta.Direction", snapshot.Direction);
            Set("Fulcrum.Delta.Trend", snapshot.Trend);
            Set("Fulcrum.Delta.RateSecondsPerSecond", snapshot.DeltaRateSecondsPerSecond);
            Set("Fulcrum.Delta.IsImproving", snapshot.IsImproving);
            Set("Fulcrum.Delta.IsLosing", snapshot.IsLosing);
            Set("Fulcrum.Delta.IsNeutral", snapshot.IsNeutral);
            Set("Fulcrum.Delta.BarValue", snapshot.BarValue);
            Set("Fulcrum.Delta.BarLeftValue", snapshot.IsImproving ? -snapshot.BarValue : 0.0f);
            Set("Fulcrum.Delta.BarRightValue", snapshot.IsLosing ? snapshot.BarValue : 0.0f);
            Set("Fulcrum.Delta.CurrentLapTimeSeconds", snapshot.CurrentLapTimeSeconds);
            Set("Fulcrum.Delta.LastLapTimeSeconds", snapshot.LastLapTimeSeconds);
            Set("Fulcrum.Delta.BestLapTimeSeconds", snapshot.BestLapTimeSeconds);
            Set("Fulcrum.Delta.CurrentLapTimeText", snapshot.CurrentLapTimeText);
            Set("Fulcrum.Delta.LastLapTimeText", snapshot.LastLapTimeText);
            Set("Fulcrum.Delta.BestLapTimeText", snapshot.BestLapTimeText);
            Set("Fulcrum.Delta.Status", snapshot.Status);
        }

        private void RegisterProperties()
        {
            Add("Fulcrum.Delta.Ready", false, "True when native iRacing delta telemetry is available");
            Add("Fulcrum.Delta.Error", string.Empty, "Latest Delta module error");
            Add("Fulcrum.Delta.IsValid", false, "True when the current delta has a valid reference");
            Add("Fulcrum.Delta.Reference", "Unavailable", "Active delta reference");
            Add("Fulcrum.Delta.RawSeconds", 0.0f, "Unfiltered native delta in seconds");
            Add("Fulcrum.Delta.Seconds", 0.0f, "Filtered delta in seconds; negative is faster");
            Add("Fulcrum.Delta.Text", "--.---", "Formatted delta ready for display");
            Add("Fulcrum.Delta.Direction", "Neutral", "Improving, Losing or Neutral");
            Add("Fulcrum.Delta.Trend", "Stable", "Current delta movement trend");
            Add("Fulcrum.Delta.RateSecondsPerSecond", 0.0f, "Rate at which delta is changing");
            Add("Fulcrum.Delta.IsImproving", false, "True when delta is negative");
            Add("Fulcrum.Delta.IsLosing", false, "True when delta is positive");
            Add("Fulcrum.Delta.IsNeutral", true, "True when delta is near zero");
            Add("Fulcrum.Delta.BarValue", 0.0f, "Signed normalized bar value from -1 to +1");
            Add("Fulcrum.Delta.BarLeftValue", 0.0f, "Positive magnitude for the improving left bar");
            Add("Fulcrum.Delta.BarRightValue", 0.0f, "Positive magnitude for the losing right bar");
            Add("Fulcrum.Delta.CurrentLapTimeSeconds", 0.0f, "Current lap time in seconds");
            Add("Fulcrum.Delta.LastLapTimeSeconds", 0.0f, "Last lap time in seconds");
            Add("Fulcrum.Delta.BestLapTimeSeconds", 0.0f, "Best lap time in seconds");
            Add("Fulcrum.Delta.CurrentLapTimeText", "--:--.---", "Formatted current lap time");
            Add("Fulcrum.Delta.LastLapTimeText", "--:--.---", "Formatted last lap time");
            Add("Fulcrum.Delta.BestLapTimeText", "--:--.---", "Formatted best lap time");
            Add("Fulcrum.Delta.Status", "Unavailable", "Delta engine state");
        }

        private void Add(string name, object defaultValue, string description)
        {
            pluginManager.AddProperty(name, pluginType, defaultValue, description);
        }

        private void Set(string name, object value)
        {
            pluginManager.SetPropertyValue(name, pluginType, value);
        }
    }
}
