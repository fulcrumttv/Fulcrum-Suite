using System;
using Fulcrum.Core.Session;

namespace Fulcrum.Core.Relative
{
    /// <summary>
    /// One coherent classification per COMPLETE registered class, not per
    /// visible/in-world car. A pit stop, tow or caution cannot shrink a class.
    /// Gain/loss is available in every active iRacing session type.
    /// </summary>
    public sealed class ClassPositionResolver
    {
        private readonly int[] grid = new int[64];
        private readonly int[] gridClass = new int[64];
        private readonly int[] gridSize = new int[64];
        private readonly int[] gridGeneration = new int[64];
        private readonly int[] gridQuality = new int[64];
        private readonly int[] nativePosition = new int[64];
        private readonly int[] overallPosition = new int[64];
        private readonly int[] startingClassOrder = new int[64];
        private readonly int[] qualifyingOrder = new int[64];
        private readonly int[] resultPosition = new int[64];
        private readonly int[] resultOverall = new int[64];
        private readonly int[] resolvedPosition = new int[64];
        private readonly int[] lastPosition = new int[64];
        private readonly int[] lastClass = new int[64];
        private readonly int[] lastSize = new int[64];
        private readonly int[] lastGeneration = new int[64];
        private readonly bool[] handled = new bool[64];
        private int generation;
        private int historicalRapidPolls;
        private int historicalPollDelay;

        public void Reset()
        {
            Array.Clear(grid, 0, grid.Length);
            Array.Clear(gridSize, 0, gridSize.Length);
            Array.Clear(gridGeneration, 0, gridGeneration.Length);
            Array.Clear(gridQuality, 0, gridQuality.Length);
            Array.Clear(resolvedPosition, 0, resolvedPosition.Length);
            Array.Clear(lastPosition, 0, lastPosition.Length);
            Array.Clear(lastSize, 0, lastSize.Length);
            Array.Clear(lastGeneration, 0, lastGeneration.Length);
            for (int i = 0; i < gridClass.Length; i++)
            {
                gridClass[i] = -1;
                lastClass[i] = -1;
            }
            generation = 0;
            historicalRapidPolls = 0;
            historicalPollDelay = 0;
        }

        public void Update(ParticipantBuffer buffer, SessionDatabase session, bool isRace, int state, object rawData = null)
        {
            bool activeSession = state >= 1 && state <= 6;
            bool formation = isRace && state >= 1 && state <= 3;

            // Snapshot all raw ranks BEFORE publishing any resolved rank.
            // Validating already-modified neighbours used to permit duplicates.
            for (int i = 0; i < buffer.Capacity; i++)
            {
                ParticipantSnapshot p = buffer[i];
                nativePosition[i] = p.ClassPosition;
                overallPosition[i] = p.OverallPosition;
                DriverIdentity identity = session.Get(i);
                p.IsClassifiedParticipant = identity != null && identity.IsValid && !identity.IsNonCompetitor;
                if (p.IsClassifiedParticipant && identity.ClassId >= 0) p.ClassId = identity.ClassId;
                if (identity == null && (p.OverallPosition > 0 || p.ClassPosition > 0))
                    p.IsClassifiedParticipant = true;
                p.ClassSize = 0;
                p.ClassPosition = 0;
                p.PositionGainLoss = 0;
                p.PositionGainLossAvailable = false;
                handled[i] = false;
            }

            bool needHistoricalGrid = false;
            for (int i = 0; i < buffer.Capacity && !needHistoricalGrid; i++)
            {
                ParticipantSnapshot p = buffer[i];
                if (!p.IsClassifiedParticipant || p.ClassId < 0) continue;
                int count = CountClass(buffer, p.ClassId);
                bool valid = SameMembership(buffer, p.ClassId, count, gridClass, gridSize, gridGeneration) &&
                    CompleteOrder(buffer, p.ClassId, grid, count);
                int quality = valid ? SnapshotQuality(buffer, p.ClassId, gridQuality) : 0;
                // Quality 1 is the provisional first live frame. Continue
                // looking so delayed iRacing start metadata can upgrade it.
                needHistoricalGrid = quality < 2;
            }
            bool pollHistoricalGrid = false;
            if (isRace && needHistoricalGrid)
            {
                if (historicalPollDelay <= 0)
                {
                    pollHistoricalGrid = true;
                    if (historicalRapidPolls < 120) historicalRapidPolls++;
                    else historicalPollDelay = 59;
                }
                else historicalPollDelay--;
            }
            bool hasStartingClass = pollHistoricalGrid &&
                RelativeSessionReader.ReadStartingClassOrder(rawData, startingClassOrder);
            bool hasQualifying = pollHistoricalGrid &&
                RelativeSessionReader.ReadQualifyingOrder(rawData, qualifyingOrder);
            bool resultsRead = false;
            bool hasResults = false;

            for (int i = 0; i < buffer.Capacity; i++)
            {
                ParticipantSnapshot first = buffer[i];
                if (handled[i] || !first.IsClassifiedParticipant || first.ClassId < 0) continue;
                int classId = first.ClassId;
                int count = CountClass(buffer, classId);
                bool gridValid = SameMembership(buffer, classId, count, gridClass, gridSize, gridGeneration) &&
                    CompleteOrder(buffer, classId, grid, count);
                int gridSourceQuality = gridValid ? SnapshotQuality(buffer, classId, gridQuality) : 0;
                if (gridSourceQuality == 0) gridValid = false;
                bool nativeValid = CompleteOrder(buffer, classId, nativePosition, count);
                bool overallValid = CompleteOrder(buffer, classId, overallPosition, buffer.Capacity);
                bool startingClassValid = hasStartingClass &&
                    CompleteOrder(buffer, classId, startingClassOrder, count);
                bool qualifyingValid = hasQualifying && CompleteOrder(buffer, classId, qualifyingOrder, buffer.Capacity);
                if (activeSession && !nativeValid && !overallValid && !resultsRead)
                {
                    hasResults = RelativeSessionReader.ReadSessionResults(rawData, resultPosition, resultOverall);
                    resultsRead = true;
                }
                bool resultClassValid = hasResults && CompleteOrder(buffer, classId, resultPosition, count);
                bool resultOverallValid = hasResults && CompleteOrder(buffer, classId, resultOverall, buffer.Capacity);
                bool cachedValid = SameMembership(buffer, classId, count, lastClass, lastSize, lastGeneration) &&
                    CompleteOrder(buffer, classId, lastPosition, count);

                int[] fallback = cachedValid ? lastPosition : gridValid ? grid :
                    startingClassValid ? startingClassOrder : qualifyingValid ? qualifyingOrder : null;
                int fallbackMax = cachedValid || gridValid || startingClassValid ? count : buffer.Capacity;
                bool nativePartialValid = !nativeValid &&
                    BuildPartialOrder(buffer, classId, nativePosition, count, fallback, fallbackMax, resolvedPosition);
                bool resultPartialValid = !nativePartialValid && hasResults && !resultClassValid &&
                    BuildPartialOrder(buffer, classId, resultPosition, count, fallback, fallbackMax, resolvedPosition);

                // Select one coherent source for the complete class. A single
                // missing/out-of-range AI rank may be repaired only when the
                // unused class slots determine it unambiguously (or a complete
                // prior class snapshot orders multiple missing cars). Duplicate
                // positive ranks are rejected; CarIdx is never a tie-breaker.
                int source = nativeValid ? 1 : overallValid ? 2 :
                    resultClassValid ? 3 : resultOverallValid ? 4 :
                    nativePartialValid ? 5 : resultPartialValid ? 6 :
                    formation && startingClassValid ? 7 :
                    formation && qualifyingValid ? 8 : cachedValid ? 9 : 0;
                bool sourceFresh = source >= 1 && source <= 8;
                int cacheToken = sourceFresh ? ++generation : 0;
                for (int j = 0; j < buffer.Capacity; j++)
                {
                    ParticipantSnapshot p = buffer[j];
                    if (!InClass(p, classId)) continue;
                    handled[j] = true;
                    p.ClassSize = count;
                    p.ClassPosition = source == 1 ? nativePosition[j] :
                        source == 2 ? RankInClass(buffer, classId, overallPosition, j) :
                        source == 3 ? resultPosition[j] :
                        source == 4 ? RankInClass(buffer, classId, resultOverall, j) :
                        source == 5 || source == 6 ? resolvedPosition[j] :
                        source == 7 ? startingClassOrder[j] :
                        source == 8 ? RankInClass(buffer, classId, qualifyingOrder, j) :
                        source == 9 ? lastPosition[j] : 0;
                    if (sourceFresh)
                    {
                        lastPosition[j] = p.ClassPosition;
                        lastClass[j] = classId;
                        lastSize[j] = count;
                        lastGeneration[j] = cacheToken;
                    }
                }

                // Reference hierarchy per complete class:
                // 3 = actually observed pre-green classification;
                // 2 = iRacing's persistent original class grid;
                // 1 = provisional first live frame when no history exists.
                // A delayed quality-2 source may replace quality 1 exactly
                // once, but no later pit/tow/caution frame can recapture it.
                bool observedFormation = formation &&
                    (source == 1 || source == 2 || source == 5);
                int captureQuality = observedFormation && gridSourceQuality < 3 ? 3 :
                    isRace && startingClassValid && gridSourceQuality < 2 ? 2 :
                    isRace && qualifyingValid && gridSourceQuality < 2 ? 2 :
                    gridSourceQuality == 0 && sourceFresh &&
                        ((!isRace && activeSession) || (isRace && state >= 4 && state <= 6)) ? 1 : 0;
                if (captureQuality > 0)
                {
                    int gridToken = ++generation;
                    for (int j = 0; j < buffer.Capacity; j++)
                    {
                        if (!InClass(buffer[j], classId)) continue;
                        grid[j] = captureQuality == 3 || captureQuality == 1 ? buffer[j].ClassPosition :
                            startingClassValid ? startingClassOrder[j] :
                            RankInClass(buffer, classId, qualifyingOrder, j);
                        gridClass[j] = classId;
                        gridSize[j] = count;
                        gridGeneration[j] = gridToken;
                        gridQuality[j] = captureQuality;
                    }
                    gridValid = true;
                    gridSourceQuality = captureQuality;
                }

                bool gainPhase = isRace ? state >= 4 && state <= 6 : activeSession;
                for (int j = 0; j < buffer.Capacity; j++)
                {
                    ParticipantSnapshot p = buffer[j];
                    if (!InClass(p, classId)) continue;
                    if (gainPhase && gridValid && p.ClassPosition > 0)
                    {
                        p.PositionGainLossAvailable = true;
                        p.PositionGainLoss = grid[j] - p.ClassPosition;
                    }
                }
            }
        }

        private static bool InClass(ParticipantSnapshot p, int classId)
        {
            return p.IsClassifiedParticipant && p.ClassId == classId;
        }

        private static int CountClass(ParticipantBuffer buffer, int classId)
        {
            int count = 0;
            for (int i = 0; i < buffer.Capacity; i++)
                if (InClass(buffer[i], classId)) count++;
            return count;
        }

        private static bool CompleteOrder(ParticipantBuffer buffer, int classId, int[] order, int maxRank)
        {
            for (int i = 0; i < buffer.Capacity; i++)
            {
                if (!InClass(buffer[i], classId)) continue;
                if (order[i] < 1 || order[i] > maxRank) return false;
                for (int j = 0; j < i; j++)
                    if (InClass(buffer[j], classId) && order[j] == order[i]) return false;
            }
            return true;
        }

        private static bool BuildPartialOrder(
            ParticipantBuffer buffer,
            int classId,
            int[] primary,
            int count,
            int[] fallback,
            int fallbackMax,
            int[] output)
        {
            Array.Clear(output, 0, output.Length);
            int known = 0;
            int missing = 0;
            for (int i = 0; i < buffer.Capacity; i++)
            {
                if (!InClass(buffer[i], classId)) continue;
                int rank = primary[i];
                if (rank >= 1 && rank <= count)
                {
                    for (int j = 0; j < i; j++)
                        if (InClass(buffer[j], classId) && output[j] == rank) return false;
                    output[i] = rank;
                    known++;
                }
                else missing++;
            }
            if (known == 0 || missing == 0) return false;

            // More than one absent rank needs a trustworthy order among only
            // those absent cars. With one missing car, the sole unused class
            // slot is mathematically determined and needs no fallback.
            if (missing > 1)
            {
                if (fallback == null) return false;
                for (int i = 0; i < buffer.Capacity; i++)
                {
                    if (!InClass(buffer[i], classId) || output[i] != 0) continue;
                    if (fallback[i] < 1 || fallback[i] > fallbackMax) return false;
                    for (int j = 0; j < i; j++)
                        if (InClass(buffer[j], classId) && output[j] == 0 && fallback[j] == fallback[i]) return false;
                }
            }

            for (int rank = 1; rank <= count; rank++)
            {
                bool used = false;
                for (int i = 0; i < buffer.Capacity; i++)
                    if (InClass(buffer[i], classId) && output[i] == rank) used = true;
                if (used) continue;

                int best = -1;
                for (int i = 0; i < buffer.Capacity; i++)
                {
                    if (!InClass(buffer[i], classId) || output[i] != 0) continue;
                    if (best < 0 || missing == 1 || fallback[i] < fallback[best]) best = i;
                }
                if (best < 0) return false;
                output[best] = rank;
                missing--;
            }
            return CompleteOrder(buffer, classId, output, count);
        }

        private static int RankInClass(ParticipantBuffer buffer, int classId, int[] order, int car)
        {
            int rank = 1;
            for (int i = 0; i < buffer.Capacity; i++)
                if (InClass(buffer[i], classId) && order[i] < order[car]) rank++;
            return rank;
        }

        private static bool SameMembership(ParticipantBuffer buffer, int classId, int count, int[] classes, int[] sizes, int[] tokens)
        {
            // Size/class alone are insufficient when members leave/rejoin.
            // Every cached member must belong to the SAME captured snapshot.
            int token = 0;
            for (int i = 0; i < buffer.Capacity; i++)
            {
                if (!InClass(buffer[i], classId)) continue;
                if (classes[i] != classId || sizes[i] != count || tokens[i] <= 0) return false;
                if (token == 0) token = tokens[i];
                else if (tokens[i] != token) return false;
            }
            return true;
        }

        private static int SnapshotQuality(ParticipantBuffer buffer, int classId, int[] qualities)
        {
            int quality = 0;
            for (int i = 0; i < buffer.Capacity; i++)
            {
                if (!InClass(buffer[i], classId)) continue;
                if (qualities[i] <= 0) return 0;
                if (quality == 0) quality = qualities[i];
                else if (qualities[i] != quality) return 0;
            }
            return quality;
        }
    }
}
