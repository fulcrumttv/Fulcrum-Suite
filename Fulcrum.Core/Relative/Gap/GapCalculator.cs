// SPDX-License-Identifier: GPL-3.0-or-later
// Fulcrum Suite modification: Copyright (C) 2026 FulcrumTTV.
// This file contains implementation derived/adapted in part from concepts/code
// in benofficial2's Official Overlays (Copyright (C) 2023-2026 benofficial2),
// licensed under GPL-3.0-or-later.
// Fulcrum version modified for the v4.1.34 source snapshot on 2026-08-31.
// See the repository LICENSE and NOTICE files for details.

using System;

namespace Fulcrum.Core.Relative.Gap
{
    /// <summary>
    /// High-precision Relative calculation matching Ben's method:
    /// 1) cache CarIdxEstTime by CarID and track position;
    /// 2) evaluate the PLAYER car model's time curve at the opponent position;
    /// 3) evaluate the same common curve at the player's current position;
    /// 4) subtract the two common-reference position times;
    /// 5) normalize to +/- half a class-estimated lap and reject/fallback
    ///    when the timing sign disagrees with physical ahead/behind order.
    /// </summary>
    public sealed class GapCalculator
    {
        private const float MinimumTime = 0.00001f;
        private const float MaximumAbsoluteGap = 900.0f;
        private readonly CarEstTimeCache cache = new CarEstTimeCache();

        public void Reset()
        {
            cache.Reset();
        }

        public void Calculate(
            ParticipantBuffer buffer,
            RelativeSnapshot snapshot)
        {
            if (buffer == null || snapshot == null)
            {
                return;
            }

            ParticipantSnapshot player;
            if (!buffer.TryGetParticipant(snapshot.PlayerCarIndex, out player) ||
                player == null || !player.IsValid)
            {
                ClearAllGaps(snapshot);
                return;
            }

            cache.Update(buffer);

            for (int index = 0; index < snapshot.Capacity; index++)
            {
                CalculateEntry(buffer, snapshot.GetAhead(index), player, true);
                CalculateEntry(buffer, snapshot.GetBehind(index), player, false);
            }
        }

        private void CalculateEntry(
            ParticipantBuffer buffer,
            RelativeEntry entry,
            ParticipantSnapshot player,
            bool isAhead)
        {
            if (entry == null || !entry.IsValid || entry.CarIndex < 0)
            {
                ClearEntryGap(entry);
                return;
            }

            ParticipantSnapshot opponent;
            if (!buffer.TryGetParticipant(entry.CarIndex, out opponent) ||
                opponent == null || !opponent.IsValid)
            {
                ClearEntryGap(entry);
                return;
            }

            PopulateDiagnostics(entry, player, opponent);

            float lapTime = ResolvePlayerClassLapTime(player);
            float estTimeAtPlayerPosition =
                GetEstTimeAtPlayerPosition(player);
            float estTimeAtOpponentPosition =
                GetEstTimeAtOpponentPosition(opponent, player);

            entry.DiagnosticPlayerMapTime = estTimeAtPlayerPosition;
            entry.DiagnosticOtherMapTime = estTimeAtOpponentPosition;

            float gap = GetEstTimeDifference(
                lapTime,
                estTimeAtOpponentPosition,
                estTimeAtPlayerPosition);

            if (!IsFinite(gap))
            {
                ClearEntryGap(entry);
                return;
            }

            // v4.1.34: after circular normalization the sign should already
            // match the physical ahead/behind ordering.  If it does not, do not
            // add/subtract a whole lap: that is the failure mode that can turn
            // a 2-3 second car behind into an 80-100 second displayed gap.
            // Fall back to the circular physical track distance converted with
            // the player's estimated lap time.  It is conservative but bounded
            // and preserves the correct side until the timing curve is coherent.
            bool signMismatch =
                (isAhead && gap < 0.0f) ||
                (!isAhead && gap > 0.0f);

            if (signMismatch)
            {
                gap = entry.RelativeDistanceLaps * lapTime;
                entry.DiagnosticGapMethod = "COMMON_CURVE_DISTANCE_FALLBACK";
            }
            else
            {
                entry.DiagnosticGapMethod = "COMMON_PLAYER_CURVE";
            }

            float plausibleLimit = Math.Min(
                MaximumAbsoluteGap,
                Math.Max(5.0f, lapTime * 0.55f));

            if (!IsFinite(gap) || Math.Abs(gap) > plausibleLimit)
            {
                ClearEntryGap(entry);
                return;
            }

            entry.DiagnosticDirectEstDifference =
                opponent.EstimatedTime - player.EstimatedTime;
            entry.DiagnosticCandidateMinusLap = gap - lapTime;
            entry.DiagnosticCandidatePlusLap = gap + lapTime;
            entry.DiagnosticLapDuration = lapTime;
            entry.RawGapSeconds = gap;
            entry.HasValidRawGap = true;
            entry.FilteredGapSeconds = gap;
            entry.HasValidFilteredGap = true;
        }

        private float GetEstTimeAtPlayerPosition(
            ParticipantSnapshot player)
        {
            if (player == null) return 0.0f;

            float confidence;
            float result = cache.GetEstTime(
                player.CarId,
                player.LapDistancePercent,
                out confidence);

            if (result < MinimumTime &&
                player.EstimatedTime > MinimumTime)
            {
                result = player.EstimatedTime;
            }

            return result;
        }

        private float GetEstTimeAtOpponentPosition(
            ParticipantSnapshot opponent,
            ParticipantSnapshot player)
        {
            float result = 0.0f;
            float confidence = 0.0f;

            // v4.1.34: use one common player-car timing curve for every
            // opponent, including same-model cars.  Subtracting two independent
            // live CarIdxEstTime values can drift through the lap even when both
            // cars share a model; evaluating both positions on the same cached
            // curve keeps the reference geometry consistent.
            result = cache.GetEstTime(
                player.CarId,
                opponent.LapDistancePercent,
                out confidence);

            // Scaled emergency fallback for the rare case where no common
            // player-car curve is available yet.  A seeded common curve, even
            // at low confidence, remains preferable to mixing independent EST
            // references into the displayed gap.
            float fallback = 0.0f;
            float opponentLap = opponent.CarClassEstimatedLapTime;
            float playerLap = ResolvePlayerClassLapTime(player);

            if (opponent.EstimatedTime > MinimumTime &&
                opponentLap > MinimumTime &&
                playerLap > MinimumTime)
            {
                fallback = opponent.EstimatedTime * playerLap / opponentLap;
            }

            // The common curve is authoritative whenever it exists.  Only use
            // the scaled per-opponent value when no common curve can be built at
            // all; blending the live opponent EST back in would reintroduce the
            // mid-lap drift this build is designed to remove.
            if (result < MinimumTime)
            {
                result = fallback;
            }

            return result;
        }

        private static float GetEstTimeDifference(
            float estimatedLapTime,
            float opponentPositionTime,
            float playerPositionTime)
        {
            if (estimatedLapTime < MinimumTime ||
                opponentPositionTime < MinimumTime ||
                playerPositionTime < MinimumTime)
            {
                return float.NaN;
            }

            float difference = opponentPositionTime - playerPositionTime;
            float halfLap = 0.5f * estimatedLapTime;

            while (difference < -halfLap)
            {
                difference += estimatedLapTime;
            }

            while (difference > halfLap)
            {
                difference -= estimatedLapTime;
            }

            return difference;
        }

        private static float ResolvePlayerClassLapTime(
            ParticipantSnapshot player)
        {
            if (player != null &&
                IsFinite(player.CarClassEstimatedLapTime) &&
                player.CarClassEstimatedLapTime > MinimumTime)
            {
                return player.CarClassEstimatedLapTime;
            }

            if (player != null &&
                IsFinite(player.BestLapTime) &&
                player.BestLapTime > MinimumTime)
            {
                return player.BestLapTime;
            }

            if (player != null &&
                IsFinite(player.LastLapTime) &&
                player.LastLapTime > MinimumTime)
            {
                return player.LastLapTime;
            }

            return 120.0f;
        }

        private static void PopulateDiagnostics(
            RelativeEntry entry,
            ParticipantSnapshot player,
            ParticipantSnapshot opponent)
        {
            entry.DiagnosticPlayerLapDistPct = player.LapDistancePercent;
            entry.DiagnosticOtherLapDistPct = opponent.LapDistancePercent;
            entry.DiagnosticPlayerLapCompleted = player.LapCompleted;
            entry.DiagnosticOtherLapCompleted = opponent.LapCompleted;
            entry.DiagnosticPlayerEstTime = player.EstimatedTime;
            entry.DiagnosticOtherEstTime = opponent.EstimatedTime;
            entry.DiagnosticPlayerF2Time = player.F2Time;
            entry.DiagnosticOtherF2Time = opponent.F2Time;
        }

        private static void ClearAllGaps(RelativeSnapshot snapshot)
        {
            for (int index = 0; index < snapshot.Capacity; index++)
            {
                ClearEntryGap(snapshot.GetAhead(index));
                ClearEntryGap(snapshot.GetBehind(index));
            }
        }

        private static void ClearEntryGap(RelativeEntry entry)
        {
            if (entry == null) return;
            entry.RawGapSeconds = 0.0f;
            entry.HasValidRawGap = false;
            entry.FilteredGapSeconds = 0.0f;
            entry.HasValidFilteredGap = false;
            entry.DiagnosticGapMethod = "NONE";
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
