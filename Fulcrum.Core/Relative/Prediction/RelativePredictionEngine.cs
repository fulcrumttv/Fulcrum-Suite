using System;
using Fulcrum.Core.Relative.Display;

namespace Fulcrum.Core.Relative.Prediction
{
    /// <summary>
    /// Estimates gap trends for the closest valid cars around the player.
    /// It uses only filtered Relative gaps and retains no collections.
    /// </summary>
    public sealed class RelativePredictionEngine
    {
        private const double MinimumSampleSeconds = 0.04;
        private const double MaximumSampleSeconds = 1.5;
        private const double MinimumClosingRate = 0.08;
        private const double MaximumClosingRate = 12.0;
        private const double RateSmoothingFactor = 0.30;

        private bool hasPreviousSample;
        private DateTime previousSampleTime;

        private int previousAheadCarIndex;
        private double previousAheadGap;
        private double filteredAheadRate;

        private int previousBehindCarIndex;
        private double previousBehindGap;
        private double filteredBehindRate;

        public RelativePredictionEngine()
        {
            ResetHistory();
        }

        public void Reset(RelativePredictionSnapshot output)
        {
            ResetHistory();

            if (output != null)
            {
                output.Reset();
            }
        }

        public void Update(
            RelativeDisplaySnapshot relative,
            RelativePredictionSnapshot output)
        {
            if (relative == null || output == null)
            {
                return;
            }

            RelativeDisplayEntry player = relative.Player;
            RelativeDisplayEntry ahead = relative.GetAhead(0);
            RelativeDisplayEntry behind = relative.GetBehind(0);
            DateTime now = DateTime.UtcNow;

            output.Reset();
            output.CapturedAt = now;
            output.Ready = player != null && player.HasData;

            if (!output.Ready)
            {
                ResetHistory();
                return;
            }

            double elapsedSeconds = hasPreviousSample
                ? (now - previousSampleTime).TotalSeconds
                : 0.0;

            UpdateAhead(ahead, player, elapsedSeconds, output);
            UpdateBehind(behind, player, elapsedSeconds, output);
            UpdateBattleContext(output);
            SaveHistory(ahead, behind, now);
        }

        private void UpdateAhead(
            RelativeDisplayEntry ahead,
            RelativeDisplayEntry player,
            double elapsedSeconds,
            RelativePredictionSnapshot output)
        {
            if (!IsUsableEntry(ahead))
            {
                filteredAheadRate = 0.0;
                return;
            }

            double gap = Math.Abs(ahead.GapSeconds);
            output.AheadCarIndex = ahead.CarIndex;
            output.AheadGapSeconds = gap;

            if (CanCalculateRate(
                    elapsedSeconds,
                    ahead.CarIndex,
                    previousAheadCarIndex))
            {
                double rawRate =
                    (previousAheadGap - gap) /
                    elapsedSeconds;

                filteredAheadRate = SmoothAndClamp(
                    filteredAheadRate,
                    rawRate);
            }
            else if (ahead.CarIndex != previousAheadCarIndex)
            {
                filteredAheadRate = 0.0;
            }

            output.AheadClosingRate = filteredAheadRate;
            output.IsCatchingAhead =
                filteredAheadRate >= MinimumClosingRate;

            if (output.IsCatchingAhead)
            {
                output.AheadTimeToCatchSeconds =
                    CalculateTimeToCatch(gap, filteredAheadRate);
            }

            output.BattleAhead =
                IsSameBattleGroup(player, ahead) &&
                gap <= 1.50;
        }

        private void UpdateBehind(
            RelativeDisplayEntry behind,
            RelativeDisplayEntry player,
            double elapsedSeconds,
            RelativePredictionSnapshot output)
        {
            if (!IsUsableEntry(behind))
            {
                filteredBehindRate = 0.0;
                return;
            }

            double gap = Math.Abs(behind.GapSeconds);
            output.BehindCarIndex = behind.CarIndex;
            output.BehindGapSeconds = gap;

            if (CanCalculateRate(
                    elapsedSeconds,
                    behind.CarIndex,
                    previousBehindCarIndex))
            {
                double rawRate =
                    (previousBehindGap - gap) /
                    elapsedSeconds;

                filteredBehindRate = SmoothAndClamp(
                    filteredBehindRate,
                    rawRate);
            }
            else if (behind.CarIndex != previousBehindCarIndex)
            {
                filteredBehindRate = 0.0;
            }

            output.BehindClosingRate = filteredBehindRate;
            output.IsBeingCaught =
                filteredBehindRate >= MinimumClosingRate;

            if (output.IsBeingCaught)
            {
                output.BehindTimeToCatchSeconds =
                    CalculateTimeToCatch(gap, filteredBehindRate);
            }

            output.BattleBehind =
                IsSameBattleGroup(player, behind) &&
                gap <= 1.50;
        }

        private static void UpdateBattleContext(
            RelativePredictionSnapshot output)
        {
            if (output.BattleAhead && output.BattleBehind)
            {
                output.BattleState = "Sandwiched";
            }
            else if (output.BattleBehind)
            {
                output.BattleState = "Defending";
            }
            else if (output.BattleAhead)
            {
                output.BattleState = "Attacking";
            }
            else
            {
                output.BattleState = "Clear";
            }

            if (output.BehindGapSeconds > 0.0 &&
                output.BehindGapSeconds <= 0.40)
            {
                output.PressureLevel = "Critical";
            }
            else if (output.BattleBehind &&
                     output.IsBeingCaught &&
                     output.BehindTimeToCatchSeconds > 0.0 &&
                     output.BehindTimeToCatchSeconds <= 4.0)
            {
                output.PressureLevel = "High";
            }
            else if (output.BattleBehind)
            {
                output.PressureLevel = "Medium";
            }
            else if (output.BehindGapSeconds > 0.0 &&
                     output.BehindGapSeconds <= 2.5)
            {
                output.PressureLevel = "Low";
            }
            else
            {
                output.PressureLevel = "None";
            }

            if (output.BattleState == "Sandwiched")
            {
                output.Recommendation = "Balance attack and defense";
            }
            else if (output.PressureLevel == "Critical" ||
                     output.PressureLevel == "High")
            {
                output.Recommendation = "Prepare to defend";
            }
            else if (output.BattleAhead && output.IsCatchingAhead)
            {
                output.Recommendation = "Prepare attack";
            }
            else if (output.BattleAhead)
            {
                output.Recommendation = "Stay in range";
            }
            else
            {
                output.Recommendation = "Maintain pace";
            }

            output.Summary =
                output.BattleState +
                " · Pressure " +
                output.PressureLevel;
        }

        private void SaveHistory(
            RelativeDisplayEntry ahead,
            RelativeDisplayEntry behind,
            DateTime now)
        {
            hasPreviousSample = true;
            previousSampleTime = now;

            previousAheadCarIndex =
                IsUsableEntry(ahead)
                    ? ahead.CarIndex
                    : -1;

            previousAheadGap =
                IsUsableEntry(ahead)
                    ? Math.Abs(ahead.GapSeconds)
                    : 0.0;

            previousBehindCarIndex =
                IsUsableEntry(behind)
                    ? behind.CarIndex
                    : -1;

            previousBehindGap =
                IsUsableEntry(behind)
                    ? Math.Abs(behind.GapSeconds)
                    : 0.0;
        }

        private void ResetHistory()
        {
            hasPreviousSample = false;
            previousSampleTime = DateTime.MinValue;

            previousAheadCarIndex = -1;
            previousAheadGap = 0.0;
            filteredAheadRate = 0.0;

            previousBehindCarIndex = -1;
            previousBehindGap = 0.0;
            filteredBehindRate = 0.0;
        }

        private static bool IsUsableEntry(
            RelativeDisplayEntry entry)
        {
            return entry != null &&
                   entry.HasData &&
                   entry.HasGap &&
                   !entry.IsInPits &&
                   !entry.IsOffTrack &&
                   entry.LapDifference == 0;
        }

        private static bool IsSameBattleGroup(
            RelativeDisplayEntry player,
            RelativeDisplayEntry other)
        {
            if (!IsUsableEntry(other) ||
                player == null ||
                !player.HasData)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(player.ClassName) ||
                string.IsNullOrWhiteSpace(other.ClassName))
            {
                return true;
            }

            return string.Equals(
                player.ClassName,
                other.ClassName,
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool CanCalculateRate(
            double elapsedSeconds,
            int currentCarIndex,
            int previousCarIndex)
        {
            return elapsedSeconds >= MinimumSampleSeconds &&
                   elapsedSeconds <= MaximumSampleSeconds &&
                   currentCarIndex >= 0 &&
                   currentCarIndex == previousCarIndex;
        }

        private static double SmoothAndClamp(
            double previous,
            double current)
        {
            if (double.IsNaN(current) ||
                double.IsInfinity(current))
            {
                return 0.0;
            }

            current = Math.Max(
                -MaximumClosingRate,
                Math.Min(MaximumClosingRate, current));

            if (Math.Abs(previous) < 0.0001)
            {
                return current;
            }

            return previous +
                   RateSmoothingFactor *
                   (current - previous);
        }

        private static double CalculateTimeToCatch(
            double gapSeconds,
            double closingRate)
        {
            if (gapSeconds <= 0.0 ||
                closingRate < MinimumClosingRate)
            {
                return 0.0;
            }

            double value = gapSeconds / closingRate;

            if (double.IsNaN(value) ||
                double.IsInfinity(value) ||
                value > 999.0)
            {
                return 0.0;
            }

            return value;
        }
    }
}
