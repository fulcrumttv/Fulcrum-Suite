using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Fulcrum.Plugin.Settings;

namespace Fulcrum.Plugin.UI
{
    /// <summary>
    /// Native SimHub configuration page for Fulcrum Suite.
    /// Built entirely in code to keep deployment to a single plugin DLL.
    /// </summary>
    public sealed class FulcrumSettingsControl : UserControl
    {
        private static readonly Brush AccentBrush =
            new SolidColorBrush(Color.FromRgb(0, 229, 235));

        private static readonly Brush PanelBrush =
            new SolidColorBrush(Color.FromRgb(18, 27, 32));

        private static readonly Brush SecondaryTextBrush =
            new SolidColorBrush(Color.FromRgb(165, 182, 189));

        private readonly FulcrumPlugin plugin;
        private readonly RelativeOverlaySettings settings;
        private readonly DigiFlagsSettings digiFlagsSettings;
        private readonly TimingReferenceSettings timingReferenceSettings;
        private StackPanel contentPanel;

        public FulcrumSettingsControl(FulcrumPlugin plugin)
        {
            if (plugin == null)
            {
                throw new ArgumentNullException(nameof(plugin));
            }

            this.plugin = plugin;
            settings = plugin.RelativeSettings;
            digiFlagsSettings = plugin.DigiFlagsSettings;
            timingReferenceSettings = plugin.TimingReferenceSettings;

            BuildInterface();
        }

        private void BuildInterface()
        {
            Background = new SolidColorBrush(Color.FromRgb(11, 18, 22));

            ScrollViewer scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(22)
            };

            contentPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                MaxWidth = 980,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            scrollViewer.Content = contentPanel;
            Content = scrollViewer;

            AddHeader();
            AddDigiFlagsSection();
            AddTimingReferenceSection();
            AddRowsSection();
            AddColumnsSection();
            AddAppearanceSection();
            AddHeaderFooterSection();
            AddBehaviorSection();
            AddFooterButtons();
        }

        private void AddHeader()
        {
            TextBlock title = new TextBlock
            {
                Text = "FULCRUM SUITE",
                Foreground = Brushes.White,
                FontSize = 30,
                FontWeight = FontWeights.SemiBold
            };

            TextBlock subtitle = new TextBlock
            {
                Text = "Fulcrum Suite · v4.1.57 · Start Grid Recovery",
                Foreground = AccentBrush,
                FontSize = 15,
                Margin = new Thickness(0, 2, 0, 8)
            };

            TextBlock description = new TextBlock
            {
                Text = "Choose the rows, columns and proportions used by Fulcrum Relative. " +
                       "Changes are saved automatically and published as SimHub properties.",
                Foreground = SecondaryTextBrush,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 820,
                Margin = new Thickness(0, 0, 0, 20)
            };

            contentPanel.Children.Add(title);
            contentPanel.Children.Add(subtitle);
            contentPanel.Children.Add(description);
        }


        private void AddDigiFlagsSection()
        {
            StackPanel panel = CreateSection(
                "DIGIFLAGS · LIVE PANEL CORE",
                "Two coordinated octagonal LED panels on a 3440-wide canvas. Spacing is center-based and supports ultrawide virtual mirrors.");

            panel.Children.Add(CreateCheckBox(
                "Enable DigiFlags", digiFlagsSettings.Enabled,
                value => { digiFlagsSettings.Enabled = value; plugin.NotifyDigiFlagsSettingsChanged(); }));

            panel.Children.Add(CreateCheckBox(
                "Preview panels while configuring", digiFlagsSettings.PreviewMode,
                value => { digiFlagsSettings.PreviewMode = value; plugin.NotifyDigiFlagsSettingsChanged(); }));

            panel.Children.Add(CreateDoubleSlider(
                "Panel spacing", digiFlagsSettings.PanelGap, 100, 3000, 10,
                value => { digiFlagsSettings.PanelGap = value; plugin.NotifyDigiFlagsSettingsChanged(); },
                value => value.ToString("0", CultureInfo.InvariantCulture) + " px"));

            panel.Children.Add(CreateDoubleSlider(
                "Panel width", digiFlagsSettings.PanelWidth, 60, 120, 1,
                value => { digiFlagsSettings.PanelWidth = value; plugin.NotifyDigiFlagsSettingsChanged(); },
                value => value.ToString("0", CultureInfo.InvariantCulture) + " px"));

            panel.Children.Add(CreateDoubleSlider(
                "LED columns", digiFlagsSettings.LedColumns, 5, 9, 2,
                value =>
                {
                    int columns = (int)Math.Round(value);
                    digiFlagsSettings.LedColumns = columns <= 6 ? 5 : (columns <= 8 ? 7 : 9);
                    plugin.NotifyDigiFlagsSettingsChanged();
                },
                value =>
                {
                    int columns = (int)Math.Round(value);
                    columns = columns <= 6 ? 5 : (columns <= 8 ? 7 : 9);
                    return columns.ToString(CultureInfo.InvariantCulture) + " LEDs";
                }));

            panel.Children.Add(CreateDoubleSlider(
                "Panel height", digiFlagsSettings.PanelHeight, 220, 460, 5,
                value => { digiFlagsSettings.PanelHeight = value; plugin.NotifyDigiFlagsSettingsChanged(); },
                value => value.ToString("0", CultureInfo.InvariantCulture) + " px"));

            panel.Children.Add(CreateDoubleSlider(
                "Horizontal offset", digiFlagsSettings.HorizontalOffset, -400, 400, 5,
                value => { digiFlagsSettings.HorizontalOffset = value; plugin.NotifyDigiFlagsSettingsChanged(); },
                value => value.ToString("0", CultureInfo.InvariantCulture) + " px"));

            panel.Children.Add(CreateDoubleSlider(
                "Vertical offset", digiFlagsSettings.VerticalOffset, -120, 120, 5,
                value => { digiFlagsSettings.VerticalOffset = value; plugin.NotifyDigiFlagsSettingsChanged(); },
                value => value.ToString("0", CultureInfo.InvariantCulture) + " px"));

            panel.Children.Add(CreateDoubleSlider(
                "Brightness", digiFlagsSettings.Brightness, 0.30, 1.0, 0.05,
                value => { digiFlagsSettings.Brightness = value; plugin.NotifyDigiFlagsSettingsChanged(); },
                value => (value * 100.0).ToString("0", CultureInfo.InvariantCulture) + "%"));

            panel.Children.Add(CreateDoubleSlider(
                "Incident alert hold", digiFlagsSettings.IncidentHoldSeconds, 1, 6, 0.25,
                value => { digiFlagsSettings.IncidentHoldSeconds = value; plugin.NotifyDigiFlagsSettingsChanged(); },
                value => value.ToString("0.00", CultureInfo.InvariantCulture) + " s"));

            panel.Children.Add(CreateCheckBox(
                "Auto-hide with no active signal", digiFlagsSettings.AutoHide,
                value => { digiFlagsSettings.AutoHide = value; plugin.NotifyDigiFlagsSettingsChanged(); }));
        }

        private void AddTimingReferenceSection()
        {
            StackPanel panel = CreateSection(
                "TIMING REFERENCE · DELTA + SECTORS",
                "Choose the lap used as the comparison reference. This setting affects Fulcrum Delta and both Sectors overlays; Relative GAP is unchanged.");

            RadioButton personal = new RadioButton
            {
                Content = "MY BEST LAP · fastest personal lap in the current session",
                GroupName = "FulcrumTimingReference",
                IsChecked = timingReferenceSettings.ReferenceMode == TimingReferenceMode.PersonalBest,
                Foreground = Brushes.White,
                FontSize = 14,
                Margin = new Thickness(0, 6, 0, 4)
            };

            RadioButton classBest = new RadioButton
            {
                Content = "CLASS SESSION BEST · fastest lap set by a driver in your class",
                GroupName = "FulcrumTimingReference",
                IsChecked = timingReferenceSettings.ReferenceMode == TimingReferenceMode.ClassBest,
                Foreground = Brushes.White,
                FontSize = 14,
                Margin = new Thickness(0, 4, 0, 6)
            };

            personal.Checked += (sender, args) =>
            {
                if (timingReferenceSettings.ReferenceMode != TimingReferenceMode.PersonalBest)
                {
                    timingReferenceSettings.ReferenceMode = TimingReferenceMode.PersonalBest;
                    plugin.NotifyTimingReferenceSettingsChanged();
                }
            };

            classBest.Checked += (sender, args) =>
            {
                if (timingReferenceSettings.ReferenceMode != TimingReferenceMode.ClassBest)
                {
                    timingReferenceSettings.ReferenceMode = TimingReferenceMode.ClassBest;
                    plugin.NotifyTimingReferenceSettingsChanged();
                }
            };

            panel.Children.Add(personal);
            panel.Children.Add(classBest);

            TextBlock note = new TextBlock
            {
                Text = "MY BEST uses iRacing's native LapDeltaToBestLap. CLASS SESSION BEST uses Fulcrum's validated same-class reference trace. Sector deltas automatically follow the same selection.",
                Foreground = SecondaryTextBrush,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0)
            };

            panel.Children.Add(note);
        }

        private void AddRowsSection()
        {
            StackPanel panel = CreateSection(
                "ROWS",
                "Select how much traffic is displayed around the player.");

            panel.Children.Add(CreateIntegerSlider(
                "Cars ahead",
                settings.RowsAhead,
                0,
                4,
                value => settings.RowsAhead = value));

            panel.Children.Add(CreateIntegerSlider(
                "Cars behind",
                settings.RowsBehind,
                0,
                4,
                value => settings.RowsBehind = value));

            panel.Children.Add(CreateCheckBox(
                "Show player row",
                settings.ShowPlayer,
                value => settings.ShowPlayer = value));

            panel.Children.Add(CreateCheckBox(
                "Hide empty rows",
                settings.HideEmptyRows,
                value => settings.HideEmptyRows = value));

            panel.Children.Add(CreateCheckBox(
                "Show same class only",
                settings.SameClassOnly,
                value => settings.SameClassOnly = value));
        }

        private void AddColumnsSection()
        {
            StackPanel panel = CreateSection(
                "COLUMNS",
                "Enable each column and set the space reserved for it. Hidden columns collapse completely.");

            AddColumnEditor(panel, "Position", settings.ShowPosition, settings.PositionWidth,
                value => settings.ShowPosition = value,
                value => settings.PositionWidth = value, 32, 90);

            AddColumnEditor(panel, "Position gain / loss", settings.ShowGainLoss, settings.GainLossWidth,
                value => settings.ShowGainLoss = value,
                value => settings.GainLossWidth = value, 32, 80);

            AddColumnEditor(panel, "Car number", settings.ShowCarNumber, settings.CarNumberWidth,
                value => settings.ShowCarNumber = value,
                value => settings.CarNumberWidth = value, 32, 100);

            AddColumnEditor(panel, "Country flags", settings.ShowFlag, settings.FlagWidth,
                value => settings.ShowFlag = value,
                value => settings.FlagWidth = value, 32, 72);

            AddColumnEditor(panel, "Driver", settings.ShowDriverName, settings.DriverWidth,
                value => settings.ShowDriverName = value,
                value => settings.DriverWidth = value, 150, 520);

            AddColumnEditor(panel, "Manufacturer logo", settings.ShowManufacturerLogo, settings.LogoWidth,
                value => settings.ShowManufacturerLogo = value,
                value => settings.LogoWidth = value, 40, 120);

            AddColumnEditor(panel, "License + Safety Rating + iRating", settings.ShowLicense, settings.LicenseWidth,
                value => settings.ShowLicense = value,
                value => settings.LicenseWidth = value, 70, 240);

            AddColumnEditor(panel, "Tire compound", settings.ShowCompound, settings.CompoundWidth,
                value => settings.ShowCompound = value,
                value => settings.CompoundWidth = value, 44, 100);

            AddColumnEditor(panel, "Overtake / Push-to-Pass", settings.ShowOvertake, settings.OvertakeWidth,
                value => settings.ShowOvertake = value,
                value => settings.OvertakeWidth = value, 48, 100);

            AddColumnEditor(panel, "Live gap", settings.ShowGap, settings.GapWidth,
                value => settings.ShowGap = value,
                value => settings.GapWidth = value, 80, 180);

            AddColumnEditor(panel, "Status / stint", settings.ShowStatus, settings.StatusWidth,
                value => settings.ShowStatus = value,
                value => settings.StatusWidth = value, 60, 140);

            AddColumnEditor(panel, "Last lap", settings.ShowLastLap, settings.LastLapWidth,
                value => settings.ShowLastLap = value,
                value => settings.LastLapWidth = value, 100, 220);
        }

        private void AddAppearanceSection()
        {
            StackPanel panel = CreateSection(
                "APPEARANCE",
                "Scale the dashboard without editing individual elements.");

            panel.Children.Add(CreateDoubleSlider(
                "Text scale",
                settings.FontScale,
                0.75,
                1.50,
                0.05,
                value => settings.FontScale = value,
                value => value.ToString("0.00", CultureInfo.InvariantCulture) + "×"));

            panel.Children.Add(CreateDoubleSlider(
                "Row height",
                settings.RowHeight,
                32,
                64,
                1,
                value => settings.RowHeight = value,
                value => value.ToString("0", CultureInfo.InvariantCulture) + " px"));

            panel.Children.Add(CreateDoubleSlider(
                "Background opacity",
                settings.BackgroundOpacity,
                0.30,
                1.0,
                0.02,
                value => settings.BackgroundOpacity = value,
                value => (value * 100.0).ToString("0", CultureInfo.InvariantCulture) + "%"));

            panel.Children.Add(CreateDoubleSlider(
                "Player highlight opacity",
                settings.PlayerHighlightOpacity,
                0.30,
                1.0,
                0.02,
                value => settings.PlayerHighlightOpacity = value,
                value => (value * 100.0).ToString("0", CultureInfo.InvariantCulture) + "%"));
        }

        private void AddHeaderFooterSection()
        {
            StackPanel panel = CreateSection(
                "HEADER & FOOTER",
                "Choose which session values appear around the Relative table.");

            panel.Children.Add(CreateCheckBox(
                "Show header", settings.ShowHeader, value => settings.ShowHeader = value));
            panel.Children.Add(CreateCheckBox(
                "Header: Strength of field", settings.ShowHeaderSOF, value => settings.ShowHeaderSOF = value));
            panel.Children.Add(CreateCheckBox(
                "Header: Incidents / limit", settings.ShowHeaderIncidents, value => settings.ShowHeaderIncidents = value));
            panel.Children.Add(CreateCheckBox(
                "Header: Track temperature", settings.ShowHeaderTrackTemperature, value => settings.ShowHeaderTrackTemperature = value));
            panel.Children.Add(CreateDoubleSlider(
                "Header text scale", settings.HeaderFontScale, 0.85, 1.75, 0.05,
                value => settings.HeaderFontScale = value,
                value => value.ToString("0.00", CultureInfo.InvariantCulture) + "×"));

            panel.Children.Add(CreateCheckBox(
                "Show footer", settings.ShowFooter, value => settings.ShowFooter = value));
            panel.Children.Add(CreateCheckBox(
                "Footer: Session type", settings.ShowFooterSessionType, value => settings.ShowFooterSessionType = value));
            panel.Children.Add(CreateCheckBox(
                "Footer: Driver count", settings.ShowFooterDriverCount, value => settings.ShowFooterDriverCount = value));
            panel.Children.Add(CreateCheckBox(
                "Footer: Lap / total", settings.ShowFooterLap, value => settings.ShowFooterLap = value));
            panel.Children.Add(CreateCheckBox(
                "Footer: Time remaining", settings.ShowFooterRemaining, value => settings.ShowFooterRemaining = value));
            panel.Children.Add(CreateDoubleSlider(
                "Footer text scale", settings.FooterFontScale, 0.85, 1.75, 0.05,
                value => settings.FooterFontScale = value,
                value => value.ToString("0.00", CultureInfo.InvariantCulture) + "×"));
        }

        private void AddBehaviorSection()
        {
            StackPanel panel = CreateSection(
                "RACE BEHAVIOR",
                "Controls designed for endurance and multiclass racing.");

            panel.Children.Add(CreateCheckBox(
                "Keep OUT status for the complete out lap",
                settings.OutLapFullLap,
                value => settings.OutLapFullLap = value));

            panel.Children.Add(CreateCheckBox(
                "Keep cars visible while in pits",
                settings.KeepCarsInPits,
                value => settings.KeepCarsInPits = value));

            panel.Children.Add(CreateCheckBox(
                "Keep cars visible while towing",
                settings.KeepTowingCars,
                value => settings.KeepTowingCars = value));

            TextBlock note = new TextBlock
            {
                Text = "The adaptive Relative PRO v4 dashboard will read these settings directly. " +
                       "This release establishes and validates the native settings page first.",
                Foreground = SecondaryTextBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 12, 0, 0)
            };
            panel.Children.Add(note);
        }

        private void AddFooterButtons()
        {
            StackPanel footer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 16, 0, 30)
            };

            Button resetButton = CreateButton("RESET DEFAULTS");
            resetButton.Click += (sender, args) =>
            {
                settings.ResetDefaults();
                plugin.NotifyRelativeSettingsChanged();
                BuildInterface();
            };

            Button saveButton = CreateButton("SAVE NOW");
            saveButton.Margin = new Thickness(10, 0, 0, 0);
            saveButton.Click += (sender, args) =>
                plugin.NotifyRelativeSettingsChanged();

            footer.Children.Add(resetButton);
            footer.Children.Add(saveButton);
            contentPanel.Children.Add(footer);
        }

        private StackPanel CreateSection(string title, string description)
        {
            Border border = new Border
            {
                Background = PanelBrush,
                BorderBrush = new SolidColorBrush(Color.FromRgb(29, 76, 81)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(18),
                Margin = new Thickness(0, 0, 0, 14)
            };

            StackPanel panel = new StackPanel();
            border.Child = panel;

            TextBlock titleText = new TextBlock
            {
                Text = title,
                Foreground = AccentBrush,
                FontSize = 17,
                FontWeight = FontWeights.SemiBold
            };

            TextBlock descriptionText = new TextBlock
            {
                Text = description,
                Foreground = SecondaryTextBrush,
                FontSize = 13,
                Margin = new Thickness(0, 2, 0, 12),
                TextWrapping = TextWrapping.Wrap
            };

            panel.Children.Add(titleText);
            panel.Children.Add(descriptionText);
            contentPanel.Children.Add(border);
            return panel;
        }

        private CheckBox CreateCheckBox(
            string text,
            bool initialValue,
            Action<bool> setter)
        {
            CheckBox checkBox = new CheckBox
            {
                Content = text,
                IsChecked = initialValue,
                Foreground = Brushes.White,
                FontSize = 14,
                Margin = new Thickness(0, 5, 0, 5)
            };

            checkBox.Checked += (sender, args) =>
            {
                setter(true);
                plugin.NotifyRelativeSettingsChanged();
            };

            checkBox.Unchecked += (sender, args) =>
            {
                setter(false);
                plugin.NotifyRelativeSettingsChanged();
            };

            return checkBox;
        }

        private FrameworkElement CreateIntegerSlider(
            string label,
            int initialValue,
            int minimum,
            int maximum,
            Action<int> setter)
        {
            return CreateDoubleSlider(
                label,
                initialValue,
                minimum,
                maximum,
                1,
                value => setter((int)Math.Round(value)),
                value => Math.Round(value).ToString(CultureInfo.InvariantCulture));
        }

        private FrameworkElement CreateDoubleSlider(
            string label,
            double initialValue,
            double minimum,
            double maximum,
            double step,
            Action<double> setter,
            Func<double, string> formatter)
        {
            Grid grid = CreateEditorGrid();

            TextBlock labelText = CreateLabel(label);
            TextBlock valueText = CreateValueText(formatter(initialValue));

            Slider slider = new Slider
            {
                Minimum = minimum,
                Maximum = maximum,
                Value = initialValue,
                TickFrequency = step,
                IsSnapToTickEnabled = true,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 12, 0)
            };

            Grid.SetColumn(labelText, 0);
            Grid.SetColumn(slider, 1);
            Grid.SetColumn(valueText, 2);

            slider.ValueChanged += (sender, args) =>
            {
                setter(args.NewValue);
                valueText.Text = formatter(args.NewValue);
                plugin.NotifyRelativeSettingsChanged();
            };

            grid.Children.Add(labelText);
            grid.Children.Add(slider);
            grid.Children.Add(valueText);
            return grid;
        }

        private void AddColumnEditor(
            StackPanel parent,
            string label,
            bool visible,
            double width,
            Action<bool> visibleSetter,
            Action<double> widthSetter,
            double minimum,
            double maximum)
        {
            Grid grid = CreateEditorGrid();

            CheckBox checkBox = new CheckBox
            {
                Content = label,
                IsChecked = visible,
                Foreground = Brushes.White,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock valueText = CreateValueText(
                width.ToString("0", CultureInfo.InvariantCulture) + " px");

            Slider slider = new Slider
            {
                Minimum = minimum,
                Maximum = maximum,
                Value = width,
                TickFrequency = 2,
                IsSnapToTickEnabled = true,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 12, 0)
            };

            Grid.SetColumn(checkBox, 0);
            Grid.SetColumn(slider, 1);
            Grid.SetColumn(valueText, 2);

            checkBox.Checked += (sender, args) =>
            {
                visibleSetter(true);
                plugin.NotifyRelativeSettingsChanged();
            };

            checkBox.Unchecked += (sender, args) =>
            {
                visibleSetter(false);
                plugin.NotifyRelativeSettingsChanged();
            };

            slider.ValueChanged += (sender, args) =>
            {
                widthSetter(args.NewValue);
                valueText.Text = args.NewValue.ToString("0", CultureInfo.InvariantCulture) + " px";
                plugin.NotifyRelativeSettingsChanged();
            };

            grid.Children.Add(checkBox);
            grid.Children.Add(slider);
            grid.Children.Add(valueText);
            parent.Children.Add(grid);
        }

        private static Grid CreateEditorGrid()
        {
            Grid grid = new Grid
            {
                Margin = new Thickness(0, 4, 0, 4),
                MinHeight = 34
            };

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
            return grid;
        }

        private static TextBlock CreateLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static TextBlock CreateValueText(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = AccentBrush,
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static Button CreateButton(string text)
        {
            return new Button
            {
                Content = text,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(15, 106, 112)),
                BorderBrush = AccentBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16, 8, 16, 8),
                FontWeight = FontWeights.SemiBold
            };
        }
    }
}
