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
    /// Lightweight display stabilizer. The timing geometry is already handled
    /// by GapCalculator using Ben's per-CarID cache, so this class deliberately
    /// avoids motion integration or rate limiting that could create lag and
    /// artificial jumps. It only rejects a single-frame spike with a 3-sample
    /// median and applies a small one-decimal display hysteresis.
    /// </summary>
    public sealed class GapFilter
    {
        private const int MaximumParticipantCount = 64;
        private readonly GapState[] states = new GapState[MaximumParticipantCount];

        public GapFilter()
        {
            for (int index = 0; index < states.Length; index++)
            {
                states[index] = new GapState();
            }
        }

        public void Apply(
            ParticipantBuffer participantBuffer,
            RelativeSnapshot relativeSnapshot,
            double sessionTime,
            float playerSpeed)
        {
            if (relativeSnapshot == null)
            {
                Reset();
                return;
            }

            for (int index = 0; index < relativeSnapshot.Capacity; index++)
            {
                FilterEntry(relativeSnapshot.GetAhead(index));
                FilterEntry(relativeSnapshot.GetBehind(index));
            }
        }

        public void Reset()
        {
            for (int index = 0; index < states.Length; index++)
            {
                states[index].Reset();
            }
        }

        private void FilterEntry(RelativeEntry entry)
        {
            if (entry == null || !entry.IsValid ||
                entry.CarIndex < 0 || entry.CarIndex >= states.Length ||
                !entry.HasValidRawGap || !IsFinite(entry.RawGapSeconds))
            {
                if (entry != null)
                {
                    entry.FilteredGapSeconds = 0.0f;
                    entry.HasValidFilteredGap = false;
                }
                return;
            }

            GapState state = states[entry.CarIndex];
            float median = AddAndGetMedian(state, entry.RawGapSeconds);

            if (!state.IsInitialized)
            {
                state.IsInitialized = true;
                state.FilteredGapSeconds = median;
            }
            else
            {
                // Keep full responsiveness for meaningful movement. Only hold
                // tiny changes that would make the displayed tenth flicker.
                float previousRounded =
                    (float)Math.Round(state.FilteredGapSeconds, 1);
                float candidateRounded =
                    (float)Math.Round(median, 1);

                if (Math.Abs(median - state.FilteredGapSeconds) >= 0.12f ||
                    previousRounded != candidateRounded)
                {
                    state.FilteredGapSeconds = median;
                }
            }

            entry.FilteredGapSeconds = state.FilteredGapSeconds;
            entry.HasValidFilteredGap = true;
        }

        private static float AddAndGetMedian(GapState state, float value)
        {
            state.RawSample0 = state.RawSample1;
            state.RawSample1 = state.RawSample2;
            state.RawSample2 = value;

            if (state.RawSampleCount < 3)
            {
                state.RawSampleCount++;
            }

            if (state.RawSampleCount == 1) return state.RawSample2;
            if (state.RawSampleCount == 2)
                return 0.5f * (state.RawSample1 + state.RawSample2);

            float a = state.RawSample0;
            float b = state.RawSample1;
            float c = state.RawSample2;
            if (a > b) { float t = a; a = b; b = t; }
            if (b > c) { float t = b; b = c; c = t; }
            if (a > b) { float t = a; a = b; b = t; }
            return b;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
