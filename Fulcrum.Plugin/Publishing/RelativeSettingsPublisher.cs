using System;
using Fulcrum.Plugin.Settings;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Publishing
{
    internal sealed class RelativeSettingsPublisher
    {
        private const string Root = "Fulcrum.Settings.Relative.";

        private readonly PluginManager pluginManager;
        private readonly Type pluginType;

        public RelativeSettingsPublisher(
            PluginManager pluginManager,
            Type pluginType)
        {
            if (pluginManager == null) throw new ArgumentNullException(nameof(pluginManager));
            if (pluginType == null) throw new ArgumentNullException(nameof(pluginType));

            this.pluginManager = pluginManager;
            this.pluginType = pluginType;

            RegisterProperties();
        }

        public void Publish(RelativeOverlaySettings settings)
        {
            if (settings == null) return;

            settings.Normalize();

            Set("RowsAhead", settings.RowsAhead);
            Set("RowsBehind", settings.RowsBehind);
            Set("ShowPlayer", settings.ShowPlayer);
            Set("HideEmptyRows", settings.HideEmptyRows);
            Set("SameClassOnly", settings.SameClassOnly);
            Set("DeveloperMode", settings.DeveloperMode);

            Set("Show.Position", settings.ShowPosition);
            Set("Show.GainLoss", settings.ShowGainLoss);
            Set("Show.CarNumber", settings.ShowCarNumber);
            Set("Show.Logo", settings.ShowManufacturerLogo);
            Set("Show.Flag", settings.ShowFlag);
            Set("Show.Driver", settings.ShowDriverName);
            Set("Show.License", settings.ShowLicense);
            Set("Show.Gap", settings.ShowGap);
            Set("Show.Status", settings.ShowStatus);
            Set("Show.LastLap", settings.ShowLastLap);
            Set("Show.Stint", false); // legacy property retained, modern Relative never allocates a separate stint column
            Set("Show.Compound", settings.ShowCompound);
            Set("Show.Overtake", settings.ShowOvertake);

            Set("Header.Visible", settings.ShowHeader);
            Set("Header.ShowSOF", settings.ShowHeaderSOF);
            Set("Header.ShowIncidents", settings.ShowHeaderIncidents);
            Set("Header.ShowTrackTemperature", settings.ShowHeaderTrackTemperature);
            Set("Header.FontScale", settings.HeaderFontScale);
            Set("Footer.Visible", settings.ShowFooter);
            Set("Footer.ShowSessionType", settings.ShowFooterSessionType);
            Set("Footer.ShowDriverCount", settings.ShowFooterDriverCount);
            Set("Footer.ShowLap", settings.ShowFooterLap);
            Set("Footer.ShowRemaining", settings.ShowFooterRemaining);
            Set("Footer.FontScale", settings.FooterFontScale);

            Set("Width.Position", settings.PositionWidth);
            Set("Width.GainLoss", settings.GainLossWidth);
            Set("Width.CarNumber", settings.CarNumberWidth);
            Set("Width.Logo", settings.LogoWidth);
            Set("Width.Flag", settings.FlagWidth);
            Set("Width.Driver", settings.DriverWidth);
            Set("Width.License", settings.LicenseWidth);
            Set("Width.Gap", settings.GapWidth);
            Set("Width.Status", settings.StatusWidth);
            Set("Width.LastLap", settings.LastLapWidth);
            Set("Width.Stint", settings.StintWidth);
            Set("Width.Compound", settings.CompoundWidth);
            Set("Width.Overtake", settings.OvertakeWidth);

            Set("FontScale", settings.FontScale);
            Set("RowHeight", settings.RowHeight);
            Set("BackgroundOpacity", settings.BackgroundOpacity);
            Set("PlayerHighlightOpacity", settings.PlayerHighlightOpacity);

            Set("Behavior.OutLapFullLap", settings.OutLapFullLap);
            Set("Behavior.KeepCarsInPits", settings.KeepCarsInPits);
            Set("Behavior.KeepTowingCars", settings.KeepTowingCars);

            double totalWidth =
                VisibleWidth(settings.ShowPosition, settings.PositionWidth) +
                VisibleWidth(settings.ShowGainLoss, settings.GainLossWidth) +
                VisibleWidth(settings.ShowCarNumber, settings.CarNumberWidth) +
                VisibleWidth(settings.ShowFlag, settings.FlagWidth) +
                VisibleWidth(settings.ShowDriverName, settings.DriverWidth) +
                VisibleWidth(settings.ShowManufacturerLogo, settings.LogoWidth) +
                VisibleWidth(settings.ShowLicense, settings.LicenseWidth) +
                VisibleWidth(settings.ShowCompound, settings.CompoundWidth) +
                VisibleWidth(settings.ShowOvertake, settings.OvertakeWidth) +
                VisibleWidth(settings.ShowGap, settings.GapWidth) +
                VisibleWidth(settings.ShowStatus, settings.StatusWidth) +
                VisibleWidth(settings.ShowLastLap, settings.LastLapWidth);

            Set("Calculated.TableWidth", totalWidth);
            Set("Calculated.VisibleColumnCount", VisibleColumnCount(settings));
        }

        private void RegisterProperties()
        {
            Add("RowsAhead", 4);
            Add("RowsBehind", 4);
            Add("ShowPlayer", true);
            Add("HideEmptyRows", true);
            Add("SameClassOnly", false);
            Add("DeveloperMode", false);

            Add("Show.Position", true);
            Add("Show.GainLoss", false);
            Add("Show.CarNumber", true);
            Add("Show.Logo", true);
            Add("Show.Flag", true);
            Add("Show.Driver", true);
            Add("Show.License", true);
            Add("Show.Gap", true);
            Add("Show.Status", true);
            Add("Show.LastLap", true);
            Add("Show.Stint", false); // compatibility only
            Add("Show.Compound", true);
            Add("Show.Overtake", true);

            Add("Header.Visible", true);
            Add("Header.ShowSOF", true);
            Add("Header.ShowIncidents", true);
            Add("Header.ShowTrackTemperature", false);
            Add("Header.FontScale", 1.20);
            Add("Footer.Visible", true);
            Add("Footer.ShowSessionType", true);
            Add("Footer.ShowDriverCount", true);
            Add("Footer.ShowLap", true);
            Add("Footer.ShowRemaining", true);
            Add("Footer.FontScale", 1.20);

            Add("Width.Position", 48.0);
            Add("Width.GainLoss", 44.0);
            Add("Width.CarNumber", 48.0);
            Add("Width.Logo", 70.0);
            Add("Width.Flag", 40.0);
            Add("Width.Driver", 300.0);
            Add("Width.License", 180.0);
            Add("Width.Gap", 120.0);
            Add("Width.Status", 90.0);
            Add("Width.LastLap", 155.0);
            Add("Width.Stint", 72.0);
            Add("Width.Compound", 58.0);
            Add("Width.Overtake", 64.0);

            Add("FontScale", 1.0);
            Add("RowHeight", 44.0);
            Add("BackgroundOpacity", 0.86);
            Add("PlayerHighlightOpacity", 0.78);

            Add("Behavior.OutLapFullLap", true);
            Add("Behavior.KeepCarsInPits", true);
            Add("Behavior.KeepTowingCars", true);

            Add("Calculated.TableWidth", 1053.0);
            Add("Calculated.VisibleColumnCount", 11);
        }

        private void Add(string suffix, object defaultValue)
        {
            pluginManager.AddProperty(
                Root + suffix,
                pluginType,
                defaultValue,
                "Fulcrum Relative user setting");
        }

        private void Set(string suffix, object value)
        {
            pluginManager.SetPropertyValue(
                Root + suffix,
                pluginType,
                value);
        }

        private static double VisibleWidth(bool visible, double width)
        {
            return visible ? width : 0.0;
        }

        private static int VisibleColumnCount(RelativeOverlaySettings settings)
        {
            int count = 0;
            if (settings.ShowPosition) count++;
            if (settings.ShowGainLoss) count++;
            if (settings.ShowCarNumber) count++;
            if (settings.ShowFlag) count++;
            if (settings.ShowDriverName) count++;
            if (settings.ShowManufacturerLogo) count++;
            if (settings.ShowLicense) count++;
            if (settings.ShowGap) count++;
            if (settings.ShowStatus) count++;
            if (settings.ShowLastLap) count++;
            if (settings.ShowCompound) count++;
            if (settings.ShowOvertake) count++;
            return count;
        }
    }
}
