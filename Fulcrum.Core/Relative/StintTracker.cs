using System;

namespace Fulcrum.Core.Relative
{
    /// <summary>
    /// One stint policy for all cars. Formation/grid placement is not a pit
    /// exit. Race timing starts at L1, and a real pit exit is OUT until S/F.
    /// </summary>
    public sealed class StintTracker
    {
        private const int Capacity = ParticipantBuffer.DefaultCapacity;
        private readonly RelativeLapTracker laps = new RelativeLapTracker();
        private readonly bool[] initialized = new bool[Capacity];
        private readonly bool[] previousValid = new bool[Capacity];
        private readonly bool[] previousPitRoad = new bool[Capacity];
        private readonly int[] stintStartCompleted = new int[Capacity];
        private readonly int[] stintLaps = new int[Capacity];
        private readonly bool[] outLapActive = new bool[Capacity];
        private readonly int[] outLapStartCompleted = new int[Capacity];
        private readonly bool[] previouslyInWorld = new bool[Capacity];
        private readonly bool[] towing = new bool[Capacity];
        private readonly float[] previousDistance = new float[Capacity];
        private bool isRace;
        private int sessionState = -1;
        private bool startPending;
        private double sessionTime;

        public void SetContext(bool race, int state, double time)
        {
            if (race && state >= 4 && (sessionState < 4 || !isRace))
                startPending = true;
            isRace = race;
            sessionState = state;
            sessionTime = time;
        }

        public void Update(ParticipantBuffer buffer)
        {
            if (buffer == null) return;
            laps.SetContext(false, sessionTime);
            laps.Update(buffer);
            bool formation = isRace && sessionState >= 1 && sessionState <= 3;
            for (int i = 0; i < buffer.Capacity && i < Capacity; i++)
            {
                ParticipantSnapshot p = buffer[i];
                bool inWorld = p.TrackSurface >= 0;
                bool valid = p.IsValid && p.LapDistancePercent >= 0.0f &&
                    p.LapDistancePercent <= 1.0f && (p.Lap >= 0 || p.LapCompleted >= 0);
                if (!valid)
                {
                    previousValid[i] = false;
                    towing[i] = !formation && !inWorld && previouslyInWorld[i] && !previousPitRoad[i];
                    continue;
                }
                bool pit = p.IsOnPitRoad || p.TrackSurface == 1;
                int completed = laps.CompletedLaps(p);
                float pct = p.LapDistancePercent;

                if (formation)
                {
                    // Grid placement, rolling formation and standing starts
                    // all share this branch, including cars previously in pits.
                    initialized[i] = true;
                    stintStartCompleted[i] = 0;
                    stintLaps[i] = 0;
                    outLapActive[i] = false;
                    towing[i] = false;
                }
                else if (!initialized[i] || startPending)
                {
                    initialized[i] = true;
                    // CarIdxLap=0 before the first start-line crossing gives
                    // completed=-1. The start crossing itself is not a stint lap.
                    stintStartCompleted[i] = isRace ? 0 : completed;
                    stintLaps[i] = pit ? 0 : Math.Max(1, completed - stintStartCompleted[i] + 1);
                    outLapActive[i] = false;
                    towing[i] = false;
                }
                else if (pit)
                {
                    stintLaps[i] = 0;
                    outLapActive[i] = false;
                    towing[i] = false;
                }
                else
                {
                    if (previousValid[i] && previousPitRoad[i])
                    {
                        outLapActive[i] = true;
                        outLapStartCompleted[i] = completed;
                        stintLaps[i] = 0;
                    }
                    if (outLapActive[i])
                    {
                        bool crossed = previousValid[i] && previousDistance[i] > 0.80f && pct < 0.20f;
                        // The second condition recovers a missed crossing after
                        // an invalid sample. No player-specific offset is used.
                        if (!previousPitRoad[i] && (crossed || (completed > outLapStartCompleted[i] && pct > 0.03f && pct < 0.97f)))
                        {
                            outLapActive[i] = false;
                            stintStartCompleted[i] = completed;
                            stintLaps[i] = 1;
                        }
                    }
                    else
                    {
                        stintLaps[i] = Math.Max(1, completed - stintStartCompleted[i] + 1);
                    }
                    towing[i] = false;
                }
                previousValid[i] = true;
                previousPitRoad[i] = pit;
                previouslyInWorld[i] = inWorld;
                previousDistance[i] = pct;
            }
            startPending = false;
        }

        public int GetStintLap(int carIndex)
        {
            return carIndex >= 0 && carIndex < Capacity ? stintLaps[carIndex] : 0;
        }

        public bool IsOutLap(int carIndex)
        {
            return carIndex >= 0 && carIndex < Capacity && outLapActive[carIndex];
        }

        public bool IsTowing(int carIndex)
        {
            return carIndex >= 0 && carIndex < Capacity && towing[carIndex];
        }

        public void Reset()
        {
            laps.Reset();
            Array.Clear(initialized, 0, Capacity);
            Array.Clear(previousValid, 0, Capacity);
            Array.Clear(previousPitRoad, 0, Capacity);
            Array.Clear(stintStartCompleted, 0, Capacity);
            Array.Clear(stintLaps, 0, Capacity);
            Array.Clear(outLapActive, 0, Capacity);
            Array.Clear(outLapStartCompleted, 0, Capacity);
            Array.Clear(previouslyInWorld, 0, Capacity);
            Array.Clear(towing, 0, Capacity);
            Array.Clear(previousDistance, 0, Capacity);
            isRace = false;
            sessionState = -1;
            startPending = false;
            sessionTime = 0.0;
        }
    }
}
