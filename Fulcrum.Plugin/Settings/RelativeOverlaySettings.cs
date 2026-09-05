using System;

namespace Fulcrum.Plugin.Settings
{
    [Serializable]
    public sealed class RelativeOverlaySettings
    {
        public int RowsAhead { get; set; } = 4;
        public int RowsBehind { get; set; } = 4;
        public bool ShowPlayer { get; set; } = true;
        public bool HideEmptyRows { get; set; } = true;
        public bool SameClassOnly { get; set; } = false;
        public bool DeveloperMode { get; set; } = false;

        public bool ShowPosition { get; set; } = true;
        public bool ShowGainLoss { get; set; } = false;
        public bool ShowCarNumber { get; set; } = true;
        public bool ShowManufacturerLogo { get; set; } = true;
        public bool ShowFlag { get; set; } = true;
        public bool ShowDriverName { get; set; } = true;
        public bool ShowLicense { get; set; } = true;
        public bool ShowGap { get; set; } = true;
        public bool ShowStatus { get; set; } = true;
        public bool ShowLastLap { get; set; } = true;
        public bool ShowStint { get; set; } = false;
        public bool ShowCompound { get; set; } = true;
        public bool ShowOvertake { get; set; } = true;

        // Optional header/footer blocks. Disabled individually by the user.
        public bool ShowHeader { get; set; } = true;
        public bool ShowHeaderSOF { get; set; } = true;
        public bool ShowHeaderIncidents { get; set; } = true;
        public bool ShowHeaderTrackTemperature { get; set; } = false;
        public bool ShowFooter { get; set; } = true;
        public bool ShowFooterSessionType { get; set; } = true;
        public bool ShowFooterDriverCount { get; set; } = true;
        public bool ShowFooterLap { get; set; } = true;
        public bool ShowFooterRemaining { get; set; } = true;
        public double HeaderFontScale { get; set; } = 1.20;
        public double FooterFontScale { get; set; } = 1.20;

        public double PositionWidth { get; set; } = 48.0;
        public double GainLossWidth { get; set; } = 44.0;
        public double CarNumberWidth { get; set; } = 48.0;
        public double LogoWidth { get; set; } = 72.0;
        public double FlagWidth { get; set; } = 40.0;
        public double DriverWidth { get; set; } = 350.0;
        public double LicenseWidth { get; set; } = 180.0;
        public double GapWidth { get; set; } = 120.0;
        public double StatusWidth { get; set; } = 90.0;
        public double LastLapWidth { get; set; } = 155.0;
        public double StintWidth { get; set; } = 72.0;
        public double CompoundWidth { get; set; } = 58.0;
        public double OvertakeWidth { get; set; } = 64.0;

        public double FontScale { get; set; } = 1.0;
        public double RowHeight { get; set; } = 44.0;
        public double BackgroundOpacity { get; set; } = 0.86;
        public double PlayerHighlightOpacity { get; set; } = 0.78;

        public bool OutLapFullLap { get; set; } = true;
        public bool KeepCarsInPits { get; set; } = true;
        public bool KeepTowingCars { get; set; } = true;

        public void Normalize()
        {
            RowsAhead = Clamp(RowsAhead, 0, 4);
            RowsBehind = Clamp(RowsBehind, 0, 4);

            // Separate stint column is retained only for legacy XML compatibility.
            ShowStint = false;

            PositionWidth = Clamp(PositionWidth, 32.0, 90.0);
            GainLossWidth = Clamp(GainLossWidth, 32.0, 80.0);
            CarNumberWidth = Clamp(CarNumberWidth, 32.0, 100.0);
            LogoWidth = Clamp(LogoWidth, 40.0, 120.0);
            FlagWidth = Clamp(FlagWidth, 40.0, 90.0);
            DriverWidth = Clamp(DriverWidth, 150.0, 520.0);
            // v4.1.11: the license/SR/iRating text now uses the same visual scale as driver names.
            // Preserve user intent when upgrading from the old hard maximum (150 px).
            if (LicenseWidth >= 149.5 && LicenseWidth <= 150.5)
            {
                LicenseWidth = 190.0;
            }
            LicenseWidth = Clamp(LicenseWidth, 70.0, 240.0);
            GapWidth = Clamp(GapWidth, 80.0, 180.0);
            StatusWidth = Clamp(StatusWidth, 60.0, 140.0);
            LastLapWidth = Clamp(LastLapWidth, 100.0, 220.0);
            StintWidth = Clamp(StintWidth, 50.0, 120.0);
            CompoundWidth = Clamp(CompoundWidth, 44.0, 100.0);
            OvertakeWidth = Clamp(OvertakeWidth, 48.0, 100.0);

            FontScale = Clamp(FontScale, 0.75, 1.50);
            HeaderFontScale = Clamp(HeaderFontScale, 0.85, 1.75);
            FooterFontScale = Clamp(FooterFontScale, 0.85, 1.75);
            RowHeight = Clamp(RowHeight, 32.0, 64.0);
            BackgroundOpacity = Clamp(BackgroundOpacity, 0.30, 1.0);
            PlayerHighlightOpacity = Clamp(PlayerHighlightOpacity, 0.30, 1.0);
        }

        public void ResetDefaults()
        {
            RelativeOverlaySettings defaults = new RelativeOverlaySettings();

            RowsAhead = defaults.RowsAhead;
            RowsBehind = defaults.RowsBehind;
            ShowPlayer = defaults.ShowPlayer;
            HideEmptyRows = defaults.HideEmptyRows;
            SameClassOnly = defaults.SameClassOnly;
            DeveloperMode = defaults.DeveloperMode;

            ShowPosition = defaults.ShowPosition;
            ShowGainLoss = defaults.ShowGainLoss;
            ShowCarNumber = defaults.ShowCarNumber;
            ShowManufacturerLogo = defaults.ShowManufacturerLogo;
            ShowFlag = defaults.ShowFlag;
            ShowDriverName = defaults.ShowDriverName;
            ShowLicense = defaults.ShowLicense;
            ShowGap = defaults.ShowGap;
            ShowStatus = defaults.ShowStatus;
            ShowLastLap = defaults.ShowLastLap;
            ShowStint = false;
            ShowCompound = defaults.ShowCompound;
            ShowOvertake = defaults.ShowOvertake;
            ShowHeader = defaults.ShowHeader;
            ShowHeaderSOF = defaults.ShowHeaderSOF;
            ShowHeaderIncidents = defaults.ShowHeaderIncidents;
            ShowHeaderTrackTemperature = defaults.ShowHeaderTrackTemperature;
            ShowFooter = defaults.ShowFooter;
            ShowFooterSessionType = defaults.ShowFooterSessionType;
            ShowFooterDriverCount = defaults.ShowFooterDriverCount;
            ShowFooterLap = defaults.ShowFooterLap;
            ShowFooterRemaining = defaults.ShowFooterRemaining;

            PositionWidth = defaults.PositionWidth;
            GainLossWidth = defaults.GainLossWidth;
            CarNumberWidth = defaults.CarNumberWidth;
            LogoWidth = defaults.LogoWidth;
            FlagWidth = defaults.FlagWidth;
            DriverWidth = defaults.DriverWidth;
            LicenseWidth = defaults.LicenseWidth;
            GapWidth = defaults.GapWidth;
            StatusWidth = defaults.StatusWidth;
            LastLapWidth = defaults.LastLapWidth;
            StintWidth = defaults.StintWidth;
            CompoundWidth = defaults.CompoundWidth;
            OvertakeWidth = defaults.OvertakeWidth;

            FontScale = defaults.FontScale;
            HeaderFontScale = defaults.HeaderFontScale;
            FooterFontScale = defaults.FooterFontScale;
            RowHeight = defaults.RowHeight;
            BackgroundOpacity = defaults.BackgroundOpacity;
            PlayerHighlightOpacity = defaults.PlayerHighlightOpacity;

            OutLapFullLap = defaults.OutLapFullLap;
            KeepCarsInPits = defaults.KeepCarsInPits;
            KeepTowingCars = defaults.KeepTowingCars;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
