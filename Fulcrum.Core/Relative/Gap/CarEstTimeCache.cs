// SPDX-License-Identifier: GPL-3.0-or-later
// Fulcrum Suite modification: Copyright (C) 2026 FulcrumTTV.
// This file contains implementation derived/adapted in part from concepts/code
// in benofficial2's Official Overlays (Copyright (C) 2023-2026 benofficial2),
// licensed under GPL-3.0-or-later.
// Fulcrum version modified for the v4.1.34 source snapshot on 2026-08-31.
// See the repository LICENSE and NOTICE files for details.

using System;
using System.Collections.Generic;

namespace Fulcrum.Core.Relative.Gap
{
    /// <summary>
    /// Per-car-model time/position cache based on Ben's Relative approach.
    ///
    /// Version 3.5.14 added a cold-start model. Every CarID receives a
    /// provisional linear lap curve as soon as CarClassEstLapTime is known.
    /// Real CarIdxEstTime samples then replace that provisional curve and are
    /// interpolated independently for each model. This produces useful gaps
    /// immediately, while converging to the native iRacing timing geometry as
    /// the session supplies more samples.
    /// </summary>
    public sealed class CarEstTimeCache
    {
        private const int BinCount = 512;
        private const int SeedAnchorCount = 33;
        private const float MinimumEstTime = 0.00001f;
        private const float MaximumEstTime = 900.0f;
        private const float MinimumLapTime = 10.0f;
        private const float MaximumLapTime = 900.0f;

        private sealed class CarCurve
        {
            public readonly float[] Position = new float[BinCount];
            public readonly float[] Time = new float[BinCount];
            public readonly bool[] Valid = new bool[BinCount];
            public readonly bool[] Real = new bool[BinCount];
            public float EstimatedLapTime;
            public int RealSampleCount;
            public bool IsSeeded;
        }

        private readonly Dictionary<int, CarCurve> curves =
            new Dictionary<int, CarCurve>();

        public void Reset()
        {
            curves.Clear();
        }

        public void Update(ParticipantBuffer buffer)
        {
            if (buffer == null)
            {
                return;
            }

            // Pass one: create/seed all known CarIDs before inserting live
            // samples. This means every participant of the same model can use
            // a complete provisional curve on the first telemetry update.
            for (int index = 0; index < buffer.Capacity; index++)
            {
                ParticipantSnapshot participant = buffer[index];
                if (participant == null || !participant.IsValid ||
                    participant.CarId <= 0)
                {
                    continue;
                }

                EnsureCurve(
                    participant.CarId,
                    participant.CarClassEstimatedLapTime);
            }

            // Pass two: add every valid live sample from every car of that
            // CarID. Real samples always take precedence over seeded anchors.
            for (int index = 0; index < buffer.Capacity; index++)
            {
                ParticipantSnapshot participant = buffer[index];
                if (!IsUsable(participant))
                {
                    continue;
                }

                AddEstTime(
                    participant.CarId,
                    participant.LapDistancePercent,
                    participant.EstimatedTime,
                    participant.CarClassEstimatedLapTime);
            }
        }

        public void AddEstTime(
            int carId,
            float positionPercent,
            float estimatedTime,
            float estimatedLapTime)
        {
            if (carId <= 0 ||
                !IsFinite(positionPercent) ||
                !IsFinite(estimatedTime) ||
                estimatedTime <= MinimumEstTime ||
                estimatedTime > MaximumEstTime)
            {
                return;
            }

            CarCurve curve = EnsureCurve(carId, estimatedLapTime);

            float position = Clamp01(positionPercent);
            int bin = ToBin(position);

            bool wasReal = curve.Real[bin];
            if (wasReal)
            {
                // CarIdxEstTime can move by tiny amounts from frame to frame.
                // Blend repeated observations in the same bucket instead of
                // replacing the point outright; this keeps the learned curve
                // stable without adding visible lag to the final Relative.
                curve.Position[bin] =
                    curve.Position[bin] * 0.75f + position * 0.25f;
                curve.Time[bin] =
                    curve.Time[bin] * 0.75f + estimatedTime * 0.25f;
            }
            else
            {
                curve.Position[bin] = position;
                curve.Time[bin] = estimatedTime;
                curve.Valid[bin] = true;
                curve.Real[bin] = true;
                curve.RealSampleCount++;
            }

            if (IsValidLapTime(estimatedLapTime))
            {
                UpdateLapTime(curve, estimatedLapTime);
            }
        }

        /// <summary>
        /// Returns estimated time and a 0..1 confidence score. Confidence is
        /// based on how many real samples exist and how close the requested
        /// position is to real samples on both sides. Callers can blend this
        /// result with a safe fallback during the first seconds of a session.
        /// </summary>
        public float GetEstTime(
            int carId,
            float positionPercent,
            out float confidence)
        {
            confidence = 0.0f;

            CarCurve curve;
            if (carId <= 0 || !curves.TryGetValue(carId, out curve))
            {
                return 0.0f;
            }

            float position = Clamp01(positionPercent);
            int requested = ToBin(position);

            int lower = FindLower(curve, requested);
            int upper = FindUpper(curve, requested);

            if (lower < 0 && upper < 0)
            {
                return 0.0f;
            }

            float value;
            if (lower < 0)
            {
                float upperPosition = curve.Position[upper];
                if (upperPosition <= 0.0f)
                {
                    return 0.0f;
                }

                value = curve.Time[upper] * Clamp01(position / upperPosition);
            }
            else if (upper < 0)
            {
                if (!IsValidLapTime(curve.EstimatedLapTime))
                {
                    return 0.0f;
                }

                float lowerPosition = curve.Position[lower];
                float remaining = 1.0f - lowerPosition;
                if (remaining <= 0.0f)
                {
                    value = curve.EstimatedLapTime;
                }
                else
                {
                    float factor = Clamp01((position - lowerPosition) / remaining);
                    value = curve.Time[lower] * (1.0f - factor) +
                            curve.EstimatedLapTime * factor;
                }
            }
            else
            {
                float p0 = curve.Position[lower];
                float p1 = curve.Position[upper];
                float span = p1 - p0;
                if (span <= 0.0f)
                {
                    value = curve.Time[lower];
                }
                else
                {
                    float blend = Clamp01((position - p0) / span);
                    value = curve.Time[lower] * (1.0f - blend) +
                            curve.Time[upper] * blend;
                }
            }

            confidence = CalculateConfidence(curve, requested);
            return value;
        }

        public float GetEstTime(int carId, float positionPercent)
        {
            float confidence;
            return GetEstTime(carId, positionPercent, out confidence);
        }

        private CarCurve EnsureCurve(int carId, float estimatedLapTime)
        {
            CarCurve curve;
            if (!curves.TryGetValue(carId, out curve))
            {
                curve = new CarCurve();
                curves.Add(carId, curve);
            }

            if (IsValidLapTime(estimatedLapTime))
            {
                UpdateLapTime(curve, estimatedLapTime);
            }

            if (!curve.IsSeeded && IsValidLapTime(curve.EstimatedLapTime))
            {
                SeedLinearCurve(curve);
            }

            return curve;
        }

        private static void SeedLinearCurve(CarCurve curve)
        {
            if (curve == null || !IsValidLapTime(curve.EstimatedLapTime))
            {
                return;
            }

            for (int anchor = 0; anchor < SeedAnchorCount; anchor++)
            {
                float position = (float)anchor / (SeedAnchorCount - 1);
                int bin = ToBin(position);

                if (curve.Real[bin])
                {
                    continue;
                }

                curve.Position[bin] = position;
                curve.Time[bin] = position * curve.EstimatedLapTime;
                curve.Valid[bin] = true;
                curve.Real[bin] = false;
            }

            curve.IsSeeded = true;
        }

        private static void UpdateLapTime(CarCurve curve, float lapTime)
        {
            if (!IsValidLapTime(lapTime))
            {
                return;
            }

            if (!IsValidLapTime(curve.EstimatedLapTime))
            {
                curve.EstimatedLapTime = lapTime;
                return;
            }

            // SessionInfo should be stable, but use a gentle blend to avoid a
            // one-frame metadata correction reshaping the provisional curve.
            curve.EstimatedLapTime =
                curve.EstimatedLapTime * 0.9f + lapTime * 0.1f;
        }

        private static float CalculateConfidence(CarCurve curve, int requested)
        {
            if (curve == null || curve.RealSampleCount <= 0)
            {
                return 0.0f;
            }

            int lowerReal = FindLowerReal(curve, requested);
            int upperReal = FindUpperReal(curve, requested);

            float sampleConfidence = Clamp01(curve.RealSampleCount / 12.0f);
            float proximityConfidence = 0.0f;

            if (lowerReal >= 0 && upperReal >= 0)
            {
                int span = upperReal - lowerReal;
                proximityConfidence = Clamp01(1.0f - span / 128.0f);
                proximityConfidence = Math.Max(0.35f, proximityConfidence);
            }
            else
            {
                int nearest = lowerReal >= 0
                    ? requested - lowerReal
                    : upperReal - requested;
                proximityConfidence = Clamp01(1.0f - nearest / 96.0f) * 0.65f;
            }

            return Clamp01(0.25f * sampleConfidence +
                           0.75f * proximityConfidence);
        }

        private static int FindLower(CarCurve curve, int start)
        {
            for (int index = start; index >= 0; index--)
            {
                if (curve.Valid[index]) return index;
            }
            return -1;
        }

        private static int FindUpper(CarCurve curve, int start)
        {
            for (int index = start + 1; index < BinCount; index++)
            {
                if (curve.Valid[index]) return index;
            }
            return -1;
        }

        private static int FindLowerReal(CarCurve curve, int start)
        {
            for (int index = start; index >= 0; index--)
            {
                if (curve.Valid[index] && curve.Real[index]) return index;
            }
            return -1;
        }

        private static int FindUpperReal(CarCurve curve, int start)
        {
            for (int index = start + 1; index < BinCount; index++)
            {
                if (curve.Valid[index] && curve.Real[index]) return index;
            }
            return -1;
        }

        private static bool IsUsable(ParticipantSnapshot participant)
        {
            return participant != null &&
                   participant.IsValid &&
                   participant.CarId > 0 &&
                   participant.LapDistancePercent >= 0.0f &&
                   participant.LapDistancePercent <= 1.0f &&
                   IsFinite(participant.EstimatedTime) &&
                   participant.EstimatedTime > MinimumEstTime &&
                   participant.EstimatedTime <= MaximumEstTime;
        }

        private static int ToBin(float position)
        {
            int bin = (int)(Clamp01(position) * BinCount);
            if (bin >= BinCount) bin = BinCount - 1;
            if (bin < 0) bin = 0;
            return bin;
        }

        private static float Clamp01(float value)
        {
            if (value < 0.0f) return 0.0f;
            if (value > 1.0f) return 1.0f;
            return value;
        }

        private static bool IsValidLapTime(float value)
        {
            return IsFinite(value) &&
                   value >= MinimumLapTime &&
                   value <= MaximumLapTime;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
