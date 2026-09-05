using System;

namespace Fulcrum.Core.Relative
{
    /// <summary>
    /// Keeps one continuous race coordinate per car. A start/finish crossing
    /// changes the coordinate by centimetres, not by an extra whole lap.
    /// Raw lap counters anchor new/reappearing cars; they are not subtracted
    /// directly for colors and cannot count an observed crossing twice.
    /// </summary>
    public sealed class RelativeLapTracker
    {
        private readonly bool[] known = new bool[64];
        private readonly bool[] previousValid = new bool[64];
        private readonly bool[] trusted = new bool[64];
        private readonly int[] completed = new int[64];
        private readonly float[] previousPct = new float[64];
        private readonly double[] mismatchSince = new double[64];
        private readonly int[] anchorRaw = new int[64];
        private readonly float[] anchorPct = new float[64];
        private readonly double[] anchorSince = new double[64];
        private readonly double[] anchorLastTime = new double[64];
        private readonly int[] anchorSamples = new int[64];
        private bool colorsEnabled;
        private double time;

        public void Reset()
        {
            for (int i = 0; i < known.Length; i++)
            {
                known[i] = false;
                previousValid[i] = false;
                trusted[i] = false;
                completed[i] = -1;
                previousPct[i] = 0.0f;
                mismatchSince[i] = -1.0;
                anchorSince[i] = -1.0;
                anchorLastTime[i] = -1.0;
                anchorSamples[i] = 0;
            }
            colorsEnabled = false;
        }

        public void SetContext(bool enabled, double sessionTime)
        {
            colorsEnabled = enabled;
            time = sessionTime;
        }

        public void Update(ParticipantBuffer buffer)
        {
            for (int i = 0; i < buffer.Capacity && i < known.Length; i++)
            {
                ParticipantSnapshot p = buffer[i];
                bool valid = p.IsValid && p.LapDistancePercent >= 0.0f &&
                    p.LapDistancePercent <= 1.0f && (p.Lap >= 0 || p.LapCompleted >= 0);
                if (!valid)
                {
                    previousValid[i] = false;
                    trusted[i] = false;
                    anchorSince[i] = -1.0;
                    anchorSamples[i] = 0;
                    continue;
                }

                float pct = p.LapDistancePercent;
                // Lap is laps STARTED. Before the first start-line crossing,
                // Lap=0 corresponds to coordinate -1+pct, not 0+pct.
                int raw = p.Lap >= 0 ? p.Lap - 1 : p.LapCompleted;
                bool lineZone = pct < 0.03f || pct > 0.97f;
                double step = CircularDistance(previousPct[i], pct);
                bool discontinuity = !previousValid[i] || Math.Abs(step) > 0.10 ||
                    (p.TrackSurface == 1 && Math.Abs(step) > 0.005);
                bool crossedLine = false;

                if (!known[i] || discontinuity)
                {
                    completed[i] = raw;
                    known[i] = true;
                    // A first sample at the line cannot tell which of two
                    // independently updated counters arrived first. Be neutral.
                    trusted[i] = !lineZone;
                    mismatchSince[i] = -1.0;
                }
                else
                {
                    if (previousPct[i] > 0.80f && pct < 0.20f && step >= 0.0)
                    {
                        completed[i]++;
                        crossedLine = true;
                    }
                    else if (previousPct[i] < 0.20f && pct > 0.80f && step < 0.0)
                    {
                        completed[i]--;
                        crossedLine = true;
                    }

                    if (!trusted[i] && !lineZone)
                    {
                        completed[i] = raw;
                        trusted[i] = true;
                    }

                    // Early/late raw increments near the line never override
                    // the observed wrap. Reconcile a persistent scoring change
                    // away from the line, with neutral colors while uncertain.
                    if (!lineZone && raw != completed[i])
                    {
                        if (mismatchSince[i] < 0.0) mismatchSince[i] = time;
                        if (time >= 0.0 && time - mismatchSince[i] >= 2.0)
                        {
                            completed[i] = raw;
                            mismatchSince[i] = -1.0;
                        }
                    }
                    else mismatchSince[i] = -1.0;
                }

                // A pit stall may be inside the finish-line guard zone. After
                // a teleport, reconnect or first sample there, waiting until
                // the car LEAVES that zone used to suppress every lap color
                // indefinitely. Re-anchor a stationary car from stable raw
                // scoring instead. This applies equally to every car/class,
                // without depending on PIT/OUT, a stint, or an overtake.
                if (lineZone)
                {
                    if (HasStationaryAnchor(i, raw, pct, discontinuity || crossedLine))
                    {
                        completed[i] = raw;
                        trusted[i] = true;
                        mismatchSince[i] = -1.0;
                    }
                }
                else anchorSince[i] = -1.0;
                previousPct[i] = pct;
                previousValid[i] = true;
            }
        }

        private bool HasStationaryAnchor(int i, int raw, float pct, bool reset)
        {
            if (double.IsNaN(time) || double.IsInfinity(time) || time < 0.0)
            {
                anchorSince[i] = -1.0;
                anchorSamples[i] = 0;
                return false;
            }

            // Two seconds and at least three distinct telemetry timestamps.
            // Measure motion against the anchor, not only the previous frame:
            // a slowly moving car must not be mistaken for a stationary car.
            // Any raw-counter change, crossing or clock rewind restarts this
            // short settling window; normal moving crossings keep their
            // existing wrap-based protection against early/late lap updates.
            if (reset || anchorSince[i] < 0.0 || time < anchorLastTime[i] ||
                raw != anchorRaw[i] || Math.Abs(CircularDistance(anchorPct[i], pct)) > 0.00005)
            {
                anchorRaw[i] = raw;
                anchorPct[i] = pct;
                anchorSince[i] = time;
                anchorSamples[i] = 1;
            }
            else if (time > anchorLastTime[i] && anchorSamples[i] < 3)
                anchorSamples[i]++;
            anchorLastTime[i] = time;
            return anchorSamples[i] >= 3 && time - anchorSince[i] >= 2.0;
        }

        public int CompletedLaps(ParticipantSnapshot p)
        {
            int i = p.CarIndex;
            return i >= 0 && i < known.Length && known[i]
                ? completed[i] : (p.Lap >= 0 ? p.Lap - 1 : p.LapCompleted);
        }

        public double ContinuousPosition(ParticipantSnapshot p)
        {
            return CompletedLaps(p) + p.LapDistancePercent;
        }

        public int LapDifference(ParticipantSnapshot player, ParticipantSnapshot other)
        {
            int a = player.CarIndex;
            int b = other.CarIndex;
            if (!colorsEnabled || a < 0 || b < 0 || a >= known.Length || b >= known.Length ||
                !trusted[a] || !trusted[b] || !previousValid[a] || !previousValid[b] ||
                mismatchSince[a] >= 0.0 || mismatchSince[b] >= 0.0) return 0;

            // On the initial lap there cannot yet be a lapping encounter.
            // This does NOT suppress a real lapper when the player remains
            // on lap one: the other car will already have completed a lap.
            if (completed[a] < 1 && completed[b] < 1) return 0;

            double physicalOffset = CircularDistance(player.LapDistancePercent, other.LapDistancePercent);
            double raceOffset = ContinuousPosition(other) - ContinuousPosition(player);
            // Remove the small physical offset. The remainder is the number
            // of race laps separating these two nearby cars, including a
            // lapper approaching from behind BEFORE it passes the player.
            return (int)Math.Round(raceOffset - physicalOffset);
        }

        private static double CircularDistance(double from, double to)
        {
            double delta = to - from;
            if (delta > 0.5) delta -= 1.0;
            else if (delta < -0.5) delta += 1.0;
            return delta;
        }
    }
}
