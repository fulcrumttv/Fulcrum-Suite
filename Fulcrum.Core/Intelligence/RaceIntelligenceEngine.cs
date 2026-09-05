using System;
using Fulcrum.Core.Radar;
using Fulcrum.Core.Relative.Display;
using Fulcrum.Core.Telemetry;

namespace Fulcrum.Core.Intelligence
{
    /// <summary>
    /// Combines the existing Fulcrum data sources into simple, stable decisions.
    /// It intentionally avoids predicting beyond what the telemetry supports.
    /// </summary>
    public sealed class RaceIntelligenceEngine
    {
        private bool hasPreviousSample;
        private DateTime previousSampleTime;
        private float previousAheadGap;
        private float previousBehindGap;
        private int previousAheadCarIndex;
        private int previousBehindCarIndex;

        public void Reset(RaceIntelligenceSnapshot snapshot)
        {
            hasPreviousSample = false;
            previousSampleTime = DateTime.MinValue;
            previousAheadGap = 0.0f;
            previousBehindGap = 0.0f;
            previousAheadCarIndex = -1;
            previousBehindCarIndex = -1;
            snapshot.Reset();
        }

        public void Update(
            TelemetrySnapshot telemetry,
            RelativeDisplaySnapshot relative,
            RadarSnapshot radar,
            RaceIntelligenceSnapshot output)
        {
            if (telemetry == null || relative == null || radar == null || output == null)
            {
                return;
            }

            RelativeDisplayEntry player = relative.Player;
            RelativeDisplayEntry ahead = relative.GetAhead(0);
            RelativeDisplayEntry behind = relative.GetBehind(0);

            output.Reset();
            output.CapturedAt = DateTime.UtcNow;
            output.Ready = telemetry.GameRunning && player.HasData;

            if (!output.Ready)
            {
                ResetHistory();
                return;
            }

            UpdateGapTrends(ahead, behind, output);
            UpdateTraffic(player, ahead, behind, output);
            UpdateAttack(ahead, player, output);
            UpdateDefense(behind, player, radar, output);
            UpdateThreat(ahead, behind, radar, telemetry, output);
            UpdateAdvice(radar, output);
            SaveHistory(ahead, behind);
        }

        private void UpdateGapTrends(
            RelativeDisplayEntry ahead,
            RelativeDisplayEntry behind,
            RaceIntelligenceSnapshot output)
        {
            DateTime now = DateTime.UtcNow;
            double seconds = hasPreviousSample
                ? (now - previousSampleTime).TotalSeconds
                : 0.0;

            if (seconds >= 0.05 && seconds <= 2.0)
            {
                if (ahead.HasData && ahead.HasGap &&
                    ahead.CarIndex == previousAheadCarIndex)
                {
                    output.ClosingRateAhead =
                        (previousAheadGap - Math.Abs(ahead.GapSeconds)) / seconds;
                    output.ClosingCarAhead = output.ClosingRateAhead > 0.15;
                }

                if (behind.HasData && behind.HasGap &&
                    behind.CarIndex == previousBehindCarIndex)
                {
                    output.ClosingRateBehind =
                        (previousBehindGap - Math.Abs(behind.GapSeconds)) / seconds;
                    output.ClosingCarBehind = output.ClosingRateBehind > 0.15;
                }
            }
        }

        private static void UpdateTraffic(
            RelativeDisplayEntry player,
            RelativeDisplayEntry ahead,
            RelativeDisplayEntry behind,
            RaceIntelligenceSnapshot output)
        {
            output.CarAheadInPits = ahead.HasData && ahead.IsInPits;
            output.CarBehindInPits = behind.HasData && behind.IsInPits;

            bool aheadOtherClass = IsOtherClass(player, ahead);
            bool behindOtherClass = IsOtherClass(player, behind);

            if (aheadOtherClass)
            {
                output.ClassTraffic = ahead.ClassName;
                output.SlowerClassAhead = true;
            }
            else if (behindOtherClass)
            {
                output.ClassTraffic = behind.ClassName;
                output.FasterClassApproaching = output.ClosingCarBehind;
            }
        }

        private static void UpdateAttack(
            RelativeDisplayEntry ahead,
            RelativeDisplayEntry player,
            RaceIntelligenceSnapshot output)
        {
            if (!ahead.HasData || !ahead.HasGap || ahead.IsInPits || ahead.IsOffTrack ||
                IsOtherClass(player, ahead) || ahead.LapDifference != 0)
            {
                return;
            }

            double gap = Math.Abs(ahead.GapSeconds);
            if (gap <= 0.35 && output.ClosingCarAhead)
            {
                output.AttackOpportunity = "Excellent";
            }
            else if (gap <= 0.70 && output.ClosingCarAhead)
            {
                output.AttackOpportunity = "Good";
            }
            else if (gap <= 1.20)
            {
                output.AttackOpportunity = "Possible";
            }

            output.HasAttackOpportunity = output.AttackOpportunity != "None";
        }

        private static void UpdateDefense(
            RelativeDisplayEntry behind,
            RelativeDisplayEntry player,
            RadarSnapshot radar,
            RaceIntelligenceSnapshot output)
        {
            if (!behind.HasData || behind.IsInPits || behind.IsOffTrack ||
                IsOtherClass(player, behind) || behind.LapDifference != 0)
            {
                output.DefenseRequired = radar.IsActive;
                return;
            }

            double gap = behind.HasGap ? Math.Abs(behind.GapSeconds) : 99.0;
            output.DefenseRequired =
                radar.IsActive ||
                gap <= 0.55 ||
                (gap <= 1.10 && output.ClosingCarBehind);
        }

        private static void UpdateThreat(
            RelativeDisplayEntry ahead,
            RelativeDisplayEntry behind,
            RadarSnapshot radar,
            TelemetrySnapshot telemetry,
            RaceIntelligenceSnapshot output)
        {
            int score = radar.Severity * 30;
            string reason = radar.IsActive ? radar.Callout : string.Empty;

            if (output.DefenseRequired)
            {
                score += 20;
                reason = AppendReason(reason, "car attacking behind");
            }

            if (behind.HasData && behind.HasGap && Math.Abs(behind.GapSeconds) <= 0.35)
            {
                score += 20;
                reason = AppendReason(reason, "very small rear gap");
            }

            if (ahead.HasData && ahead.IsOffTrack && ahead.HasGap && Math.Abs(ahead.GapSeconds) <= 2.0)
            {
                score += 20;
                reason = AppendReason(reason, "car off track ahead");
            }

            if (!telemetry.IsOnTrack || telemetry.IsOnPitRoad)
            {
                score = Math.Min(score, 25);
            }

            output.ThreatScore = Math.Min(score, 100);
            output.ThreatReason = reason;

            if (output.ThreatScore >= 80) output.ThreatLevel = "Critical";
            else if (output.ThreatScore >= 50) output.ThreatLevel = "Danger";
            else if (output.ThreatScore >= 25) output.ThreatLevel = "Caution";
            else output.ThreatLevel = "Safe";
        }

        private static void UpdateAdvice(
            RadarSnapshot radar,
            RaceIntelligenceSnapshot output)
        {
            if (radar.HasCarsBothSides)
            {
                output.SuggestedAction = "Hold line";
            }
            else if (radar.HasCarLeft)
            {
                output.SuggestedAction = "Keep right";
            }
            else if (radar.HasCarRight)
            {
                output.SuggestedAction = "Keep left";
            }
            else if (output.DefenseRequired)
            {
                output.SuggestedAction = "Prepare to defend";
            }
            else if (output.HasAttackOpportunity)
            {
                output.SuggestedAction = "Prepare attack";
            }
            else if (output.FasterClassApproaching)
            {
                output.SuggestedAction = "Expect faster-class traffic";
            }
            else if (output.SlowerClassAhead)
            {
                output.SuggestedAction = "Plan traffic pass";
            }

            output.Summary = output.ThreatLevel + " - " + output.SuggestedAction;
        }

        private void SaveHistory(RelativeDisplayEntry ahead, RelativeDisplayEntry behind)
        {
            hasPreviousSample = true;
            previousSampleTime = DateTime.UtcNow;
            previousAheadCarIndex = ahead.HasData ? ahead.CarIndex : -1;
            previousBehindCarIndex = behind.HasData ? behind.CarIndex : -1;
            previousAheadGap = ahead.HasGap ? Math.Abs(ahead.GapSeconds) : 0.0f;
            previousBehindGap = behind.HasGap ? Math.Abs(behind.GapSeconds) : 0.0f;
        }

        private void ResetHistory()
        {
            hasPreviousSample = false;
            previousSampleTime = DateTime.MinValue;
            previousAheadCarIndex = -1;
            previousBehindCarIndex = -1;
        }

        private static bool IsOtherClass(RelativeDisplayEntry player, RelativeDisplayEntry other)
        {
            if (!player.HasData || !other.HasData ||
                string.IsNullOrWhiteSpace(player.ClassName) ||
                string.IsNullOrWhiteSpace(other.ClassName))
            {
                return false;
            }

            return !string.Equals(player.ClassName, other.ClassName, StringComparison.OrdinalIgnoreCase);
        }

        private static string AppendReason(string current, string reason)
        {
            return string.IsNullOrEmpty(current) ? reason : current + ", " + reason;
        }
    }
}
