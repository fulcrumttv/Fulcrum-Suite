using System;
using System.Globalization;
using Fulcrum.Core.Relative.Display;
using Fulcrum.Plugin.Settings;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Publishing
{
    /// <summary>
    /// Publishes one stable nine-row visual table.
    ///
    /// v4.1.19: the displayed window is adaptive. RowsAhead/RowsBehind define
    /// the preferred split and total size; if one side cannot provide enough
    /// cars, unused slots are borrowed by the other side. The player therefore
    /// moves toward the top/bottom of the visual window near the ends of the field.
    /// </summary>
    internal sealed class RelativeTablePublisher
    {
        private const int RowCount = 9;

        private static readonly string[] Fields =
        {
            "Visible", "IsPlayer", "IsSameClass", "LapDifference", "IsLappedByPlayer", "IsAheadByLap", "RowState", "CarIndex", "Position", "ClassSize", "PositionGainLoss", "PositionGainLossAvailable", "CarNumber",
            "DriverName", "UserId", "CarId", "ClassId", "CarPath", "CarScreenName", "CarName", "Manufacturer", "ManufacturerAlias", "LogoResourceKey", "ClassName", "DriverInfoRaw", "FlagText", "CountryAlias", "FlagResourceKey", "ClubName", "License", "IRating", "LicenseIRatingText", "LicenseClass", "ClassColorSlot", "TireCompound", "TireCompoundText", "TireCompoundIconKey", "OvertakeSupported", "OvertakeActive", "OvertakeRemaining", "OvertakeText", "GapLiveSeconds",
            "GapLiveText", "GapTrend", "Status", "StatusIconKey", "StatusIconVisible", "StatusStintText", "LastLapTimeText", "StintLap",
            "StintText", "IsInPits", "IsOutLap", "IsTowing", "HasBlackFlag", "HasSlowDownFlag", "HasMeatballFlag", "IsDisqualified", "SessionFlagsRaw",
            "DiagPlayerLapDistPct", "DiagOtherLapDistPct", "DiagPlayerLapCompleted", "DiagOtherLapCompleted",
            "DiagPlayerEstTime", "DiagOtherEstTime", "DiagPlayerF2Time", "DiagOtherF2Time",
            "DiagDirectEstDifference", "DiagCandidateMinusLap", "DiagCandidatePlusLap", "DiagLapDuration",
            "DiagPlayerMapTime", "DiagOtherMapTime", "DiagGapMethod", "DiagSummary"
        };

        private readonly PluginManager pluginManager;
        private readonly Type pluginType;
        private readonly string[][] propertyNames;
        private readonly RelativeOverlaySettings settings;
        private readonly RelativeDisplayEntry[] adaptiveEntries = new RelativeDisplayEntry[RowCount];
        private readonly RelativeDisplayEntry[] mappedRows = new RelativeDisplayEntry[RowCount];
        private readonly int[] activeRows = new int[RowCount];
        private readonly float[] previousGapByCarIndex = new float[64];
        private readonly bool[] hasPreviousGapByCarIndex = new bool[64];
        private readonly float[] displayedGapByCarIndex = new float[64];
        private readonly bool[] hasDisplayedGapByCarIndex = new bool[64];
        private const float DisplayHysteresisSeconds = 0.08f;

        public RelativeTablePublisher(
            PluginManager pluginManager,
            Type pluginType,
            RelativeOverlaySettings settings)
        {
            if (pluginManager == null) throw new ArgumentNullException(nameof(pluginManager));
            if (pluginType == null) throw new ArgumentNullException(nameof(pluginType));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            this.pluginManager = pluginManager;
            this.pluginType = pluginType;
            this.settings = settings;
            propertyNames = new string[RowCount][];

            for (int row = 0; row < RowCount; row++)
            {
                propertyNames[row] = new string[Fields.Length];

                for (int field = 0; field < Fields.Length; field++)
                {
                    string name =
                        "Fulcrum.Relative.Table.Row" +
                        (row + 1).ToString("00", CultureInfo.InvariantCulture) +
                        "." +
                        Fields[field];

                    propertyNames[row][field] = name;
                    pluginManager.AddProperty(
                        name,
                        pluginType,
                        GetDefaultValue(Fields[field]),
                        "Stable Relative table row field");
                }
            }

            pluginManager.AddProperty(
                "Fulcrum.Relative.Table.VisibleRowCount",
                pluginType,
                0,
                "Number of populated rows in the stable Relative table");

            pluginManager.AddProperty(
                "Fulcrum.Relative.Table.OvertakeColumnVisible",
                pluginType,
                false,
                "True when the current Relative contains a P2P/overtake-capable car");
            pluginManager.AddProperty("Fulcrum.Relative.Context.SessionType", pluginType, string.Empty, "Resolved relative session type");
            pluginManager.AddProperty("Fulcrum.Relative.Context.SessionState", pluginType, -1, "Resolved nested telemetry session state");
            pluginManager.AddProperty("Fulcrum.Relative.Context.LapColorsEnabled", pluginType, false, "Race lap-color calculation is enabled");
        }

        public void PublishContext(string sessionType, int state, bool lapColorsEnabled)
        {
            pluginManager.SetPropertyValue("Fulcrum.Relative.Context.SessionType", pluginType, sessionType ?? string.Empty);
            pluginManager.SetPropertyValue("Fulcrum.Relative.Context.SessionState", pluginType, state);
            pluginManager.SetPropertyValue("Fulcrum.Relative.Context.LapColorsEnabled", pluginType, lapColorsEnabled);
        }

        public void Publish(
            RelativeDisplaySnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            int visibleCount = 0;
            string playerClassName =
                snapshot.Player != null
                    ? snapshot.Player.ClassName ?? string.Empty
                    : string.Empty;

            int playerCarId =
                snapshot.Player != null
                    ? snapshot.Player.CarId
                    : 0;

            int playerClassId =
                snapshot.Player != null
                    ? snapshot.Player.ClassId
                    : -1;

            float playerClassEstimatedLapTime =
                snapshot.Player != null
                    ? snapshot.Player.CarClassEstimatedLapTime
                    : 0.0f;

            BuildAdaptiveRowMap(snapshot);

            bool overtakeColumnVisible = HasOvertakeSupport(mappedRows);
            pluginManager.SetPropertyValue(
                "Fulcrum.Relative.Table.OvertakeColumnVisible",
                pluginType,
                overtakeColumnVisible);

            for (int row = 0; row < RowCount; row++)
            {
                PublishRow(
                    row,
                    mappedRows[row],
                    playerClassName,
                    playerCarId,
                    playerClassId,
                    playerClassEstimatedLapTime,
                    ref visibleCount);
            }

            pluginManager.SetPropertyValue(
                "Fulcrum.Relative.Table.VisibleRowCount",
                pluginType,
                visibleCount);
        }



        private void BuildAdaptiveRowMap(
            RelativeDisplaySnapshot snapshot)
        {
            for (int index = 0; index < RowCount; index++)
            {
                adaptiveEntries[index] = null;
                mappedRows[index] = null;
                activeRows[index] = -1;
            }

            int rowsAhead = Clamp(settings.RowsAhead, 0, 4);
            int rowsBehind = Clamp(settings.RowsBehind, 0, 4);
            bool showPlayer = settings.ShowPlayer;

            int availableAhead =
                Math.Min(
                    snapshot.AheadCount,
                    RelativeDisplaySnapshot.SlotCount);

            int availableBehind =
                Math.Min(
                    snapshot.BehindCount,
                    RelativeDisplaySnapshot.SlotCount);

            int takeAhead =
                Math.Min(rowsAhead, availableAhead);

            int takeBehind =
                Math.Min(rowsBehind, availableBehind);

            int requestedOtherCars =
                rowsAhead + rowsBehind;

            int missing =
                requestedOtherCars -
                takeAhead -
                takeBehind;

            // If one side cannot fill its preference, lend those slots
            // to the other side. This keeps the requested total row count.
            if (missing > 0)
            {
                int extraBehind =
                    Math.Min(
                        missing,
                        availableBehind - takeBehind);

                if (extraBehind > 0)
                {
                    takeBehind += extraBehind;
                    missing -= extraBehind;
                }
            }

            if (missing > 0)
            {
                int extraAhead =
                    Math.Min(
                        missing,
                        availableAhead - takeAhead);

                if (extraAhead > 0)
                {
                    takeAhead += extraAhead;
                    missing -= extraAhead;
                }
            }

            int entryCount = 0;

            // Ahead entries are stored nearest-first internally.
            // Visual order is farthest-to-nearest above the player.
            for (int index = takeAhead - 1;
                 index >= 0 &&
                 entryCount < RowCount;
                 index--)
            {
                adaptiveEntries[entryCount++] =
                    snapshot.GetAhead(index);
            }

            if (showPlayer &&
                entryCount < RowCount)
            {
                adaptiveEntries[entryCount++] =
                    snapshot.Player;
            }

            // Behind entries are already nearest-to-farthest.
            for (int index = 0;
                 index < takeBehind &&
                 entryCount < RowCount;
                 index++)
            {
                adaptiveEntries[entryCount++] =
                    snapshot.GetBehind(index);
            }

            // Keep the existing dashboard row-enable semantics so older
            // Relative dashboards remain compatible:
            // enabled ahead slots, optional player slot, enabled behind slots.
            int activeCount = 0;

            for (int row = 4 - rowsAhead;
                 row <= 3 &&
                 activeCount < RowCount;
                 row++)
            {
                if (row >= 0)
                {
                    activeRows[activeCount++] = row;
                }
            }

            if (showPlayer &&
                activeCount < RowCount)
            {
                activeRows[activeCount++] = 4;
            }

            for (int index = 0;
                 index < rowsBehind &&
                 activeCount < RowCount;
                 index++)
            {
                activeRows[activeCount++] =
                    5 + index;
            }

            int mappingCount =
                Math.Min(
                    entryCount,
                    activeCount);

            for (int index = 0;
                 index < mappingCount;
                 index++)
            {
                int targetRow =
                    activeRows[index];

                if (targetRow >= 0 &&
                    targetRow < RowCount)
                {
                    mappedRows[targetRow] =
                        adaptiveEntries[index];
                }
            }
        }

        private static bool IsSameClass(
            RelativeDisplayEntry entry,
            string playerClassName,
            int playerCarId,
            int playerClassId,
            float playerClassEstimatedLapTime)
        {
            if (entry == null ||
                !entry.HasData)
            {
                return false;
            }

            if (entry.IsPlayer)
            {
                return true;
            }

            // Primary path: iRacing's direct per-car class identifier.
            // This is more reliable than model IDs and avoids AI grids where
            // SessionInfo class names are incomplete/inconsistent.
            if (playerClassId >= 0 &&
                entry.ClassId >= 0)
            {
                return entry.ClassId == playerClassId;
            }

            string otherClassName =
                entry.ClassName ?? string.Empty;

            // Secondary path: normal SessionInfo class identity.
            if (!string.IsNullOrWhiteSpace(playerClassName) &&
                !string.IsNullOrWhiteSpace(otherClassName) &&
                string.Equals(
                    otherClassName.Trim(),
                    playerClassName.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // AI / inconsistent SessionInfo fallback:
            // CarClassEstimatedLapTime is associated with the iRacing class,
            // not merely the individual vehicle model. Keep the tolerance
            // deliberately tight so nearby but distinct multiclass categories
            // are not merged accidentally.
            float otherClassEstimatedLapTime =
                entry.CarClassEstimatedLapTime;

            if (playerClassEstimatedLapTime > 5.0f &&
                otherClassEstimatedLapTime > 5.0f &&
                Math.Abs(
                    otherClassEstimatedLapTime -
                    playerClassEstimatedLapTime) <= 0.50f)
            {
                return true;
            }

            // Last conservative fallback for true single-make sessions.
            if (playerCarId > 0 &&
                entry.CarId > 0 &&
                entry.CarId == playerCarId)
            {
                return true;
            }

            return false;
        }

        private static int Clamp(
            int value,
            int minimum,
            int maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }


        private void PublishRow(
            int row,
            RelativeDisplayEntry entry,
            string playerClassName,
            int playerCarId,
            int playerClassId,
            float playerClassEstimatedLapTime,
            ref int visibleCount)
        {
            bool visible =
                entry != null &&
                entry.HasData &&
                entry.CarIndex >= 0;

            if (visible)
            {
                visibleCount++;
            }

            int field = 0;
            Set(row, field++, visible);
            Set(row, field++, visible && entry.IsPlayer);
            bool isSameClass =
                visible &&
                IsSameClass(
                    entry,
                    playerClassName,
                    playerCarId,
                    playerClassId,
                    playerClassEstimatedLapTime);
            Set(row, field++, isSameClass);

            int lapDifference =
                visible && !entry.IsPlayer
                    ? entry.LapDifference
                    : 0;

            bool isLappedByPlayer =
                visible &&
                !entry.IsPlayer &&
                lapDifference < 0;

            bool isAheadByLap =
                visible &&
                !entry.IsPlayer &&
                lapDifference > 0;

            Set(row, field++, lapDifference);
            Set(row, field++, isLappedByPlayer);
            Set(row, field++, isAheadByLap);
            Set(row, field++,
                isLappedByPlayer
                    ? "Lapped"
                    : (isAheadByLap
                        ? "LapAhead"
                        : (isSameClass ? "SameClass" : "OtherClass")));

            Set(row, field++, visible ? entry.CarIndex : -1);
            // Position is always class-relative. Never substitute the overall
            // field position when CarIdxClassPosition is transiently missing
            // in a pit stall; Core reconstructs it when enough data exists.
            int sessionClassPosition =
                visible && entry.ClassPosition > 0 && entry.ClassPosition <= entry.ClassSize
                    ? entry.ClassPosition
                    : 0;
            Set(row, field++, sessionClassPosition);
            Set(row, field++, visible ? entry.ClassSize : 0);
            Set(row, field++, visible ? entry.PositionGainLoss : 0);
            Set(row, field++, visible && entry.PositionGainLossAvailable);
            Set(row, field++, visible ? entry.CarNumber ?? string.Empty : string.Empty);
            Set(row, field++, visible ? entry.DriverName ?? string.Empty : string.Empty);
            Set(row, field++, visible ? entry.UserId : 0);
            Set(row, field++, visible ? entry.CarId : 0);
            Set(row, field++, visible ? entry.ClassId : -1);
            Set(row, field++, visible ? entry.CarPath ?? string.Empty : string.Empty);
            Set(row, field++, visible ? entry.CarScreenName ?? string.Empty : string.Empty);
            Set(row, field++, visible ? entry.CarName ?? string.Empty : string.Empty);
            Set(row, field++, visible ? entry.Manufacturer ?? string.Empty : string.Empty);
            Set(row, field++, visible ? entry.ManufacturerAlias ?? string.Empty : string.Empty);
            Set(row, field++, visible ? entry.LogoResourceKey ?? string.Empty : string.Empty);
            Set(row, field++, visible ? entry.ClassName ?? string.Empty : string.Empty);
            Set(row, field++, visible ? entry.DriverInfoRaw ?? string.Empty : string.Empty);
            Set(row, field++, visible ? entry.FlagText ?? string.Empty : string.Empty);
            Set(row, field++, visible ? entry.CountryAlias ?? string.Empty : string.Empty);
            Set(row, field++, visible ? entry.FlagResourceKey ?? string.Empty : string.Empty);
            Set(row, field++, visible ? entry.ClubName ?? string.Empty : string.Empty);
            Set(row, field++, visible ? entry.License ?? string.Empty : string.Empty);
            Set(row, field++, visible ? entry.IRating : 0);
            Set(row, field++, visible ? LicenseIRatingText(entry) : string.Empty);
            Set(row, field++, visible ? LicenseClass(entry.License) : string.Empty);
            Set(row, field++, visible ? ClassColorSlot(entry.ClassName) : 0);
            Set(row, field++, visible ? entry.TireCompound : -1);
            Set(row, field++, visible ? TireCompoundText(entry) : string.Empty);
            Set(row, field++, visible ? TireCompoundIconKey(entry) : string.Empty);
            Set(row, field++, visible && entry.OvertakeSupported);
            Set(row, field++, visible && entry.OvertakeSupported && entry.OvertakeActive);
            Set(row, field++, visible && entry.OvertakeSupported ? entry.OvertakeRemaining : 0);
            Set(row, field++, visible && entry.OvertakeSupported ? OvertakeText(entry) : string.Empty);

            // v4.1.49: publish the common-reference gap calculated and
            // filtered by Fulcrum.Core.  Do not overwrite it with a direct
            // opponent CarIdxEstTime - player CarIdxEstTime subtraction here.
            // The direct EST path can disagree with physical ahead/behind
            // ordering mid-lap; forcing its sign by +/- one lap can turn a
            // small real gap into an almost full-lap value.
            float publishedGap =
                visible && entry.HasGap
                    ? entry.GapSeconds
                    : 0.0f;

            bool hasPublishedGap =
                visible && entry.HasGap;

            Set(row, field++, hasPublishedGap ? (double)publishedGap : 0.0);
            Set(row, field++, StableGapText(entry, visible, publishedGap, hasPublishedGap));
            Set(row, field++, GapTrendText(entry, visible));
            Set(row, field++, StatusText(entry, visible));
            Set(row, field++, StatusIconKey(entry, visible));
            Set(row, field++, StatusIconVisible(entry, visible));
            Set(row, field++, StatusStintText(entry, visible));
            Set(row, field++, visible ? LapTimeText(entry.LastLapTimeSeconds) : string.Empty);
            Set(row, field++, visible ? entry.StintLap : 0);
            Set(row, field++, visible ? StintText(entry) : string.Empty);
            Set(row, field++, visible && entry.IsInPits);
            Set(row, field++, visible && entry.IsOutLap);
            Set(row, field++, visible && entry.IsTowing);
            Set(row, field++, visible && entry.HasBlackFlag);
            Set(row, field++, visible && HasSlowDownFlag(entry));
            Set(row, field++, visible && entry.HasMeatballFlag);
            Set(row, field++, visible && entry.IsDisqualified);
            Set(row, field++, visible ? entry.SessionFlags : 0L);
            Set(row, field++, visible ? (double)entry.DiagnosticPlayerLapDistPct : 0.0);
            Set(row, field++, visible ? (double)entry.DiagnosticOtherLapDistPct : 0.0);
            Set(row, field++, visible ? entry.DiagnosticPlayerLapCompleted : 0);
            Set(row, field++, visible ? entry.DiagnosticOtherLapCompleted : 0);
            Set(row, field++, visible ? (double)entry.DiagnosticPlayerEstTime : 0.0);
            Set(row, field++, visible ? (double)entry.DiagnosticOtherEstTime : 0.0);
            Set(row, field++, visible ? (double)entry.DiagnosticPlayerF2Time : 0.0);
            Set(row, field++, visible ? (double)entry.DiagnosticOtherF2Time : 0.0);
            Set(row, field++, visible ? (double)entry.DiagnosticDirectEstDifference : 0.0);
            Set(row, field++, visible ? (double)entry.DiagnosticCandidateMinusLap : 0.0);
            Set(row, field++, visible ? (double)entry.DiagnosticCandidatePlusLap : 0.0);
            Set(row, field++, visible ? (double)entry.DiagnosticLapDuration : 0.0);
            Set(row, field++, visible ? (double)entry.DiagnosticPlayerMapTime : 0.0);
            Set(row, field++, visible ? (double)entry.DiagnosticOtherMapTime : 0.0);
            Set(row, field++, visible ? entry.DiagnosticGapMethod ?? string.Empty : string.Empty);
            Set(row, field++, visible ? BuildDiagnosticSummary(entry) : string.Empty);
        }


        private static bool HasOvertakeSupport(RelativeDisplayEntry[] rows)
        {
            if (rows == null) return false;

            for (int index = 0; index < rows.Length; index++)
            {
                RelativeDisplayEntry entry = rows[index];

                if (entry != null &&
                    entry.HasData &&
                    entry.OvertakeSupported)
                {
                    return true;
                }
            }

            return false;
        }

        private static string OvertakeText(RelativeDisplayEntry entry)
        {
            if (entry == null || !entry.OvertakeSupported) return string.Empty;
            return entry.OvertakeRemaining.ToString(CultureInfo.InvariantCulture);
        }

        private static string BuildDiagnosticSummary(RelativeDisplayEntry entry)
        {
            if (entry == null || !entry.HasData) return string.Empty;
            return string.Format(CultureInfo.InvariantCulture,
                "car={0};id={1};path={2};screen={3};name={4};mfg={5};logo={6};pLap={7};oLap={8};pPct={9:0.000000};oPct={10:0.000000};dist={11:0.000000};pEst={12:0.000};oEst={13:0.000};pF2={14:0.000};oF2={15:0.000};mapP={16:0.000};mapO={17:0.000};direct={18:0.000};minus={19:0.000};plus={20:0.000};lapDur={21:0.000};gap={22:0.000};method={23}",
                entry.CarIndex, entry.CarId, entry.CarPath ?? string.Empty,
                entry.CarScreenName ?? string.Empty, entry.CarName ?? string.Empty,
                entry.Manufacturer ?? string.Empty, entry.LogoResourceKey ?? string.Empty,
                entry.DiagnosticPlayerLapCompleted, entry.DiagnosticOtherLapCompleted,
                entry.DiagnosticPlayerLapDistPct, entry.DiagnosticOtherLapDistPct,
                entry.RelativeDistanceLaps, entry.DiagnosticPlayerEstTime,
                entry.DiagnosticOtherEstTime, entry.DiagnosticPlayerF2Time,
                entry.DiagnosticOtherF2Time, entry.DiagnosticPlayerMapTime,
                entry.DiagnosticOtherMapTime, entry.DiagnosticDirectEstDifference,
                entry.DiagnosticCandidateMinusLap, entry.DiagnosticCandidatePlusLap,
                entry.DiagnosticLapDuration, entry.GapLiveSeconds,
                entry.DiagnosticGapMethod ?? string.Empty);
        }

        private string StableGapText(
            RelativeDisplayEntry entry,
            bool visible,
            float signedGap,
            bool hasGap)
        {
            if (!visible || entry.IsPlayer) return string.Empty;
            if (!hasGap) return "--.-";

            int carIndex = entry.CarIndex;

            if (carIndex < 0 || carIndex >= displayedGapByCarIndex.Length)
            {
                return signedGap.ToString("0.0", CultureInfo.InvariantCulture);
            }

            float rounded =
                (float)(System.Math.Round(signedGap * 10.0,
                    MidpointRounding.AwayFromZero) / 10.0);

            if (!hasDisplayedGapByCarIndex[carIndex])
            {
                displayedGapByCarIndex[carIndex] = rounded;
                hasDisplayedGapByCarIndex[carIndex] = true;
            }
            else
            {
                float displayed = displayedGapByCarIndex[carIndex];

                // Do not change the visible tenth merely because the filtered
                // value is hovering around a rounding boundary. The displayed
                // number changes only after it moves decisively away from the
                // current tenth.
                if (System.Math.Abs(signedGap - displayed) >=
                    DisplayHysteresisSeconds)
                {
                    displayedGapByCarIndex[carIndex] = rounded;
                }
            }

            return displayedGapByCarIndex[carIndex]
                .ToString("0.0", CultureInfo.InvariantCulture);
        }

        private string GapTrendText(RelativeDisplayEntry entry, bool visible)
        {
            // v3.4.8: Trend arrows were visually noisy and could flicker.
            // Keep the property registered for dashboard compatibility, but
            // publish no symbol.
            return string.Empty;
        }

        private static string StatusText(
            RelativeDisplayEntry entry,
            bool visible)
        {
            if (!visible) return string.Empty;
            if (entry.IsDisqualified) return "DQ";
            if (entry.HasMeatballFlag) return string.Empty;
            if (entry.HasBlackFlag) return string.Empty;
            if (HasSlowDownFlag(entry)) return "SLOW";
            if (entry.IsTowing) return "TOW";
            if (entry.IsOutLap) return "OUT";
            if (entry.IsInPits || entry.IsOnPitRoad ||
                entry.IsInPitStall || entry.IsApproachingPits)
            {
                return "PIT";
            }

            return string.Empty;
        }



        private static string StatusIconKey(RelativeDisplayEntry entry, bool visible)
        {
            if (!visible) return string.Empty;
            if (entry.HasMeatballFlag) return "StatusFlag_Meatball";
            if (entry.HasBlackFlag) return "StatusFlag_Black";
            if (HasSlowDownFlag(entry)) return "StatusFlag_Black";
            return string.Empty;
        }

        private static bool StatusIconVisible(RelativeDisplayEntry entry, bool visible)
        {
            return visible && (entry.HasMeatballFlag || entry.HasBlackFlag || HasSlowDownFlag(entry));
        }

        private static bool HasSlowDownFlag(RelativeDisplayEntry entry)
        {
            // iRacing exposes Slow Down / give-back-time warnings as the
            // per-driver Furled Black flag in CarIdxSessionFlags.
            return entry != null &&
                Fulcrum.Core.Telemetry.SessionStateInterpreter.HasFurledBlack(entry.SessionFlags);
        }

        private static string LicenseIRatingText(RelativeDisplayEntry entry)
        {
            string license = entry.License ?? string.Empty;
            string ir = entry.IRating >= 1000
                ? (entry.IRating / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + "k"
                : entry.IRating.ToString(CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(license) ? ir : license + "  " + ir;
        }

        private static string LicenseClass(string license)
        {
            if (string.IsNullOrWhiteSpace(license)) return string.Empty;
            char c = char.ToUpperInvariant(license.Trim()[0]);
            return c == 'R' ? "R" : (c >= 'A' && c <= 'D' ? c.ToString() : string.Empty);
        }

        private static int ClassColorSlot(string className)
        {
            if (string.IsNullOrWhiteSpace(className)) return 0;

            // Stable semantic slots for the iRacing multiclass families used by
            // Fulcrum Relative.  The previous hash % 3 implementation could map
            // two different classes (for example LMP3 and GT4) to the same color.
            // These explicit slots keep the class identity deterministic between
            // drivers, sessions and changing Relative windows.
            string value = className.Trim().ToUpperInvariant();

            if (value.Contains("GTP") || value.Contains("LMDH") || value.Contains("HYPERCAR")) return 1;
            if (value.Contains("LMP2")) return 2;
            if (value.Contains("LMP3")) return 3;

            // Porsche Cup must be resolved before the generic GT3 rule because
            // iRacing class names such as "Porsche 911 GT3 Cup" contain GT3.
            if ((value.Contains("PORSCHE") && value.Contains("CUP")) ||
                value.Contains("911 CUP") || value.Contains("992 CUP")) return 7;

            if (value.Contains("GT3")) return 4;
            if (value.Contains("GT4")) return 5;
            if (value.Contains("TCR") || value.Contains("TOURING CAR")) return 6;

            // Fallback for classes outside the common multiclass families. Seven
            // slots greatly reduces accidental collisions while remaining stable.
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < value.Length; i++) hash = hash * 31 + value[i];
                return System.Math.Abs(hash % 7) + 1;
            }
        }

        private static string TireCompoundText(RelativeDisplayEntry entry)
        {
            int compound = entry != null ? entry.TireCompound : -1;
            if (IsMercedesW12(entry))
            {
                switch (compound)
                {
                    case 0: return "SOFT";
                    case 1: return "MEDIUM";
                    case 2: return "HARD";
                    default: return "DRY";
                }
            }

            if (IsIndyCar(entry))
            {
                switch (compound)
                {
                    case 0: return "PRIMARY";
                    case 1: return "ALTERNATE";
                    case 2: return "WET";
                    default: return "PRIMARY";
                }
            }

            switch (compound)
            {
                case 0: return "DRY";
                case 1: return "WET";
                case 2: return "SOFT";
                case 3: return "MEDIUM";
                case 4: return "HARD";
                case 5: return "ALTERNATE";
                case 6: return "PRIMARY";
                case 7: return "INTERMEDIATE";
                default: return "DRY";
            }
        }

        private static string TireCompoundIconKey(RelativeDisplayEntry entry)
        {
            string text = TireCompoundText(entry);
            switch (text)
            {
                case "WET": return "Tire_Wet";
                case "SOFT": return "Tire_Soft";
                case "MEDIUM": return "Tire_Medium";
                case "HARD": return "Tire_Hard";
                case "ALTERNATE": return "Tire_Alternate";
                case "PRIMARY": return "Tire_Primary";
                case "INTERMEDIATE": return "Tire_Intermediate";
                case "DRY":
                default: return "Tire_Dry";
            }
        }

        private static bool IsMercedesW12(RelativeDisplayEntry entry)
        {
            string value = CarDescriptor(entry);
            return value.Contains("w12") ||
                   value.Contains("mercedesamgw12") ||
                   value.Contains("mercedes amg w12") ||
                   value.Contains("mercedes w12");
        }

        private static bool IsIndyCar(RelativeDisplayEntry entry)
        {
            string value = CarDescriptor(entry);
            return value.Contains("dallarair18") ||
                   value.Contains("dallara ir18") ||
                   value.Contains("ir 18") ||
                   value.Contains("ir18") ||
                   value.Contains("indycar");
        }

        private static string CarDescriptor(RelativeDisplayEntry entry)
        {
            if (entry == null) return string.Empty;
            return ((entry.CarPath ?? string.Empty) + " " +
                    (entry.CarScreenName ?? string.Empty) + " " +
                    (entry.CarName ?? string.Empty) + " " +
                    (entry.ClassName ?? string.Empty))
                .ToLowerInvariant()
                .Replace("_", " ")
                .Replace("-", " ")
                .Replace("/", " ");
        }

        private static string StatusStintText(RelativeDisplayEntry entry, bool visible)
        {
            string status = StatusText(entry, visible);
            if (!string.IsNullOrEmpty(status)) return status;
            return visible && entry.StintLap > 0
                ? "L" + entry.StintLap.ToString(CultureInfo.InvariantCulture)
                : "--";
        }

        private static string LapTimeText(float seconds)
        {
            if (seconds <= 0.0f) return "--:--.---";

            int minutes = (int)(seconds / 60.0f);
            float remaining = seconds - minutes * 60.0f;

            return minutes.ToString(CultureInfo.InvariantCulture) +
                ":" +
                remaining.ToString("00.000", CultureInfo.InvariantCulture);
        }

        private static string StintText(RelativeDisplayEntry entry)
        {
            if (entry.IsOutLap) return "OUT";
            return entry.StintLap > 0
                ? entry.StintLap.ToString(CultureInfo.InvariantCulture)
                : "--";
        }

        private static object GetDefaultValue(string field)
        {
            switch (field)
            {
                case "PositionGainLossAvailable":
                case "Visible":
                case "IsPlayer":
                case "IsSameClass":
                case "IsLappedByPlayer":
                case "IsAheadByLap":
                case "IsInPits":
                case "IsOutLap":
                case "IsTowing":
                case "HasBlackFlag":
                case "HasSlowDownFlag":
                case "HasMeatballFlag":
                case "IsDisqualified":
                case "StatusIconVisible":
                case "OvertakeSupported":
                case "OvertakeActive":
                    return false;

                case "CarIndex":
                    return -1;

                case "CarId":
                case "SessionFlagsRaw":
                case "ClassSize":
                case "PositionGainLoss":
                case "LapDifference":
                case "Position":
                case "StintLap":
                case "DiagPlayerLapCompleted":
                case "DiagOtherLapCompleted":
                case "IRating":
                case "ClassColorSlot":
                case "TireCompound":
                case "OvertakeRemaining":
                    return 0;

                case "GapLiveSeconds":
                case "DiagPlayerLapDistPct":
                case "DiagOtherLapDistPct":
                case "DiagPlayerEstTime":
                case "DiagOtherEstTime":
                case "DiagPlayerF2Time":
                case "DiagOtherF2Time":
                case "DiagDirectEstDifference":
                case "DiagCandidateMinusLap":
                case "DiagCandidatePlusLap":
                case "DiagLapDuration":
                case "DiagPlayerMapTime":
                case "DiagOtherMapTime":
                    return 0.0;

                default:
                    return string.Empty;
            }
        }

        private void Set(int row, int field, object value)
        {
            pluginManager.SetPropertyValue(
                propertyNames[row][field],
                pluginType,
                value);
        }
    }
}
