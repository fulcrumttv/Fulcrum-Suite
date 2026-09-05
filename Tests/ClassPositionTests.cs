using System;
using Fulcrum.Core.Relative;
using Fulcrum.Core.Session;

// Runs against the compiled production Core, not a translated algorithm.
internal static class ClassPositionTests
{
    private static int checks;
    private static void Eq(int expected, int actual, string label)
    {
        checks++;
        if (expected != actual) throw new Exception(label + ": expected " + expected + ", got " + actual);
    }
    private static void Require(bool value, string label) { Eq(1, value ? 1 : 0, label); }

    private sealed class Fixture
    {
        public readonly ParticipantBuffer B = new ParticipantBuffer();
        public readonly SessionDatabase S = new SessionDatabase();
        public readonly ClassPositionResolver R = new ClassPositionResolver();
        public readonly int[][] Groups;
        public Fixture(params int[] sizes)
        {
            Groups = new int[sizes.Length][];
            int car = 0;
            for (int c = 0; c < sizes.Length; c++)
            {
                Groups[c] = new int[sizes[c]];
                for (int k = 0; k < sizes[c]; k++, car++)
                {
                    Groups[c][k] = car;
                    Add(car, 100 + c);
                    B[car].IsValid = true; B[car].Lap = 3; B[car].LapCompleted = 2;
                    B[car].LapDistancePercent = (float)(.2 + car * .003);
                    B[car].TrackSurface = 3;
                }
            }
            R.Reset();
        }
        public void Add(int car, int cls)
        {
            S.SetDriver(car, "Driver " + car, car.ToString(), "", "Class " + cls);
            S.GetWritable(car).SetClassIdentity(cls, false); B[car].ClassId = cls;
        }
        public void Set(int[] group, int[] native, int[] overall)
        {
            for (int k = 0; k < group.Length; k++)
            { B[group[k]].ClassPosition = native[k]; B[group[k]].OverallPosition = overall[k]; }
        }
        public void Expect(int[] group, int[] expected, string label)
        { for (int k = 0; k < group.Length; k++) Eq(expected[k], B[group[k]].ClassPosition, label); }
        public void Invariants()
        {
            foreach (int[] group in Groups)
            {
                int unknown = 0;
                bool[] used = new bool[group.Length + 1];
                foreach (int i in group)
                {
                    Eq(group.Length, B[i].ClassSize, "complete registered class count");
                    int pos = B[i].ClassPosition;
                    Require(pos >= 0 && pos <= group.Length, "bounded class position");
                    if (pos == 0) unknown++;
                    else { Require(!used[pos], "unique class position"); used[pos] = true; }
                }
                Require(unknown == 0 || unknown == group.Length, "whole class known or unavailable");
            }
        }
    }

    private static bool Complete(int[] order, int max)
    {
        for (int i = 0; i < order.Length; i++)
        {
            if (order[i] < 1 || order[i] > max) return false;
            for (int j = 0; j < i; j++) if (order[i] == order[j]) return false;
        }
        return true;
    }

    private static int[] FromOverall(int[] overall)
    {
        int[] result = new int[overall.Length];
        for (int i = 0; i < overall.Length; i++)
        {
            result[i] = 1;
            for (int j = 0; j < overall.Length; j++) if (overall[j] < overall[i]) result[i]++;
        }
        return result;
    }

    private static int[] Partial(int[] primary, int[] fallback)
    {
        int[] output = new int[primary.Length];
        bool[] used = new bool[primary.Length + 1];
        int known = 0, missing = 0;
        for (int i = 0; i < primary.Length; i++)
        {
            int rank = primary[i];
            if (rank >= 1 && rank <= primary.Length)
            {
                if (used[rank]) return null;
                used[rank] = true; output[i] = rank; known++;
            }
            else missing++;
        }
        if (known == 0 || missing == 0) return null;
        if (missing > 1)
        {
            if (fallback == null) return null;
            for (int i = 0; i < primary.Length; i++)
            {
                if (output[i] != 0 || fallback[i] < 1) continue;
                for (int j = 0; j < i; j++)
                    if (output[j] == 0 && fallback[j] == fallback[i]) return null;
            }
            for (int i = 0; i < primary.Length; i++)
                if (output[i] == 0 && fallback[i] < 1) return null;
        }
        for (int rank = 1; rank <= primary.Length; rank++)
        {
            if (used[rank]) continue;
            int best = -1;
            for (int i = 0; i < primary.Length; i++)
            {
                if (output[i] != 0) continue;
                if (best < 0 || missing == 1 || fallback[i] < fallback[best]) best = i;
            }
            if (best < 0) return null;
            output[best] = rank; missing--;
        }
        return output;
    }

    private static void ExhaustiveSources()
    {
        var f = new Fixture(3);
        int[] group = f.Groups[0], baseline = { 2, 3, 1 };
        foreach (bool cached in new bool[] { false, true })
        for (int test = 0; test < 15625; test++)
        {
            int[] native = new int[3], overall = new int[3];
            int q = test;
            for (int k = 0; k < 6; k++, q /= 5)
                if (k < 3) native[k] = q % 5; else overall[k - 3] = q % 5;
            f.R.Reset();
            if (cached) { f.Set(group, baseline, baseline); f.R.Update(f.B, f.S, true, 3); }
            f.Set(group, native, overall); f.R.Update(f.B, f.S, true, 4);
            bool validNative = Complete(native, 3), validOverall = Complete(overall, 64);
            int[] current = validNative ? native : validOverall ? FromOverall(overall) : Partial(native, cached ? baseline : null);
            int[] expectedOrder = current ?? (cached ? baseline : new int[3]);
            bool available = cached || current != null;
            for (int k = 0; k < 3; k++)
            {
                int expected = expectedOrder[k];
                Eq(expected, f.B[k].ClassPosition, "complete-source selection");
                Eq(available ? 1 : 0, f.B[k].PositionGainLossAvailable ? 1 : 0, "grid availability");
                if (available) Eq((cached ? baseline[k] : expected) - expected, f.B[k].PositionGainLoss, "gain uses stable reference");
            }
            f.Invariants();
        }
    }

    private static void LifecycleAndSizes()
    {
        foreach (int[] sizes in new int[][] { new int[] { 14, 12, 14 }, new int[] { 1, 7, 23 }, new int[] { 64 }, new int[] { 1 } })
        {
            var f = new Fixture(sizes);
            foreach (int[] group in f.Groups)
                for (int k = 0; k < group.Length; k++)
                { f.B[group[k]].ClassPosition = k + 1; f.B[group[k]].OverallPosition = group[k] + 1; }
            f.R.Update(f.B, f.S, true, 3);
            for (int tick = 0; tick < 960; tick++)
            {
                int offset = 0;
                foreach (int[] group in f.Groups)
                {
                    for (int k = 0; k < group.Length; k++)
                    {
                        int rank = (k + tick / 120) % group.Length + 1;
                        ParticipantSnapshot p = f.B[group[k]];
                        p.ClassPosition = k == tick % group.Length ? 0 : rank;
                        p.OverallPosition = offset + rank;
                        p.Lap = 2 + tick / 240; p.LapCompleted = p.Lap - 1;
                        int mode = (tick + k) % 4;
                        p.IsOnPitRoad = mode == 1; p.TrackSurface = mode == 0 ? 3 : mode == 1 ? 1 : -1;
                        p.IsValid = mode < 2; p.LapDistancePercent = mode < 2 ? .2f : -1f;
                    }
                    offset += group.Length;
                }
                // State remains Racing through caution laps; flags must not
                // recapture a starting grid or remove pits/towed competitors.
                object raw = new { Telemetry = new { SessionState = 4, SessionTime = (double)tick,
                    SessionFlags = tick < 720 ? 0x4000 : 0x200 } };
                f.R.Update(f.B, f.S, true, 4, raw); f.Invariants();
                foreach (int[] group in f.Groups)
                for (int k = 0; k < group.Length; k++)
                {
                    int rank = (k + tick / 120) % group.Length + 1;
                    Eq(rank, f.B[group[k]].ClassPosition, "pits/tow/garage rank under prolonged caution");
                    Eq(k + 1 - rank, f.B[group[k]].PositionGainLoss, "prolonged caution keeps original grid");
                }
            }
        }
    }

    private static void CacheMembershipAndReset()
    {
        var f = new Fixture(3);
        int[] abc = { 0, 1, 2 }, acd = { 0, 2, 3 }, abd = { 0, 1, 3 };
        int[] good = { 1, 2, 3 }, zero = { 0, 0, 0 };
        f.Set(abc, good, good); f.R.Update(f.B, f.S, true, 3);
        f.S.RemoveDriver(1); f.B[1].ClassPosition = f.B[1].OverallPosition = 0; f.Add(3, 100);
        f.Set(acd, good, good); f.R.Update(f.B, f.S, true, 3);
        f.S.RemoveDriver(2); f.B[2].ClassPosition = f.B[2].OverallPosition = 0; f.Add(1, 100);
        f.Set(abd, zero, zero); f.R.Update(f.B, f.S, true, 4);
        f.Expect(abd, zero, "cannot merge historical class caches");
        f.Set(abd, good, good); f.R.Update(f.B, f.S, true, 4);
        foreach (int i in abd) Require(f.B[i].PositionGainLossAvailable, "new complete roster captures its own late reference");
        f.Set(abd, zero, zero); f.R.Update(f.B, f.S, true, 4);
        f.Expect(abd, good, "same complete membership can use its cache");
        f.R.Reset(); f.Set(abd, zero, zero); f.R.Update(f.B, f.S, true, 4);
        f.Expect(abd, zero, "session reset clears class cache");
    }

    private static void GridAndNonCompetitors()
    {
        foreach (int state in new int[] { 1, 3 })
        {
            var f = new Fixture(3);
            object raw = new { SessionData = new { QualifyResultsInfo = new { Results = new object[] {
                new { CarIdx = 0, Position = 0 }, new { CarIdx = 1, Position = 1 }, new { CarIdx = 2, Position = 2 } } } } };
            f.R.Update(f.B, f.S, true, state, raw);
            f.Expect(f.Groups[0], new int[] { 1, 2, 3 }, "qualifying class grid");
            f.Set(f.Groups[0], new int[] { 2, 1, 3 }, new int[] { 2, 1, 3 });
            f.R.Update(f.B, f.S, true, 4, raw);
            Eq(-1, f.B[0].PositionGainLoss, "green cannot overwrite original grid");
            Eq(1, f.B[1].PositionGainLoss, "early gain retained");
        }
        var pace = new Fixture(3); pace.Add(63, 100);
        pace.S.GetWritable(63).SetClassIdentity(100, true);
        pace.Set(pace.Groups[0], new int[] { 0, 0, 0 }, new int[] { 2, 3, 4 });
        pace.B[63].ClassPosition = pace.B[63].OverallPosition = 1;
        pace.R.Update(pace.B, pace.S, true, 4);
        pace.Expect(pace.Groups[0], new int[] { 1, 2, 3 }, "exclude noncompetitor from fallback");
        Eq(0, pace.B[63].ClassPosition, "noncompetitor not ranked");
        Eq(3, pace.B[0].ClassSize, "noncompetitor not counted");
    }

    private static void AllSessionsAndPartialAiRanks()
    {
        foreach (int state in new int[] { 1, 2, 3, 4, 5, 6 })
        {
            var f = new Fixture(3); int[] group = f.Groups[0];
            f.Set(group, new int[] { 1, 2, 3 }, new int[] { 1, 2, 3 });
            f.R.Update(f.B, f.S, false, state);
            foreach (int i in group) Require(f.B[i].PositionGainLossAvailable, "non-race active session has +/- baseline");
            f.Set(group, new int[] { 2, 1, 3 }, new int[] { 2, 1, 3 });
            f.R.Update(f.B, f.S, false, state);
            Eq(-1, f.B[group[0]].PositionGainLoss, "offline/AI loss updates without lap crossing");
            Eq(1, f.B[group[1]].PositionGainLoss, "offline/AI gain updates without lap crossing");
        }

        foreach (int invalid in new int[] { -1, 0, 4, 40, 999 })
        {
            var f = new Fixture(3); int[] group = f.Groups[0];
            f.Set(group, new int[] { 1, 2, invalid }, new int[] { 0, 0, 0 });
            f.R.Update(f.B, f.S, false, 4);
            f.Expect(group, new int[] { 1, 2, 3 }, "one invalid AI rank uses sole free class slot");
            f.Invariants();
        }

        var cached = new Fixture(3); int[] g = cached.Groups[0];
        cached.Set(g, new int[] { 3, 2, 1 }, new int[] { 3, 2, 1 });
        cached.R.Update(cached.B, cached.S, false, 4);
        cached.Set(g, new int[] { 0, 2, 0 }, new int[] { 0, 0, 0 });
        cached.R.Update(cached.B, cached.S, false, 4);
        cached.Expect(g, new int[] { 3, 2, 1 }, "multiple missing AI ranks use coherent cache order");
        cached.Set(g, new int[] { 1, 1, 2 }, new int[] { 0, 0, 0 });
        cached.R.Update(cached.B, cached.S, false, 4);
        cached.Expect(g, new int[] { 3, 2, 1 }, "duplicate positive AI ranks reject partial merge");

        var unknown = new Fixture(3); int[] ug = unknown.Groups[0];
        unknown.Set(ug, new int[] { 0, 2, 0 }, new int[] { 0, 0, 0 });
        unknown.R.Update(unknown.B, unknown.S, false, 4);
        unknown.Expect(ug, new int[] { 0, 0, 0 }, "multiple unknown ranks without evidence remain unavailable");

        var late = new Fixture(3); int[] lg = late.Groups[0];
        late.Set(lg, new int[] { 3, 1, 2 }, new int[] { 3, 1, 2 });
        late.R.Update(late.B, late.S, true, 4);
        foreach (int i in lg) Require(late.B[i].PositionGainLossAvailable, "late race attach captures coherent baseline");
        late.Set(lg, new int[] { 2, 1, 3 }, new int[] { 2, 1, 3 });
        late.R.Update(late.B, late.S, true, 4);
        Eq(1, late.B[lg[0]].PositionGainLoss, "late race baseline remains stable");
        Eq(-1, late.B[lg[2]].PositionGainLoss, "late race loss remains stable");

        object[] rows = new object[] {
            new { CarIdx = 0, ClassPosition = 2, Position = 3 },
            new { CarIdx = 1, ClassPosition = 0, Position = 1 },
            new { CarIdx = 2, ClassPosition = 1, Position = 2 }
        };
        object raw = new {
            Telemetry = new { SessionNum = 2 },
            SessionData = new { SessionInfo = new { Sessions = new object[] {
                new { SessionNum = 2, SessionType = "Practice", ResultsPositions = rows }
            } } }
        };
        int[] cls = new int[64], overall = new int[64];
        Require(RelativeSessionReader.ReadSessionResults(raw, cls, overall), "current non-race results accepted");
        Eq(3, cls[0], "results ClassPosition converted from zero-based");
        var resultFixture = new Fixture(3); int[] rg = resultFixture.Groups[0];
        resultFixture.Set(rg, new int[] { 0, 0, 0 }, new int[] { 0, 0, 0 });
        resultFixture.R.Update(resultFixture.B, resultFixture.S, false, 4, raw);
        resultFixture.Expect(rg, new int[] { 3, 1, 2 }, "non-race current results restore complete class");
    }

    private static object StartingGridRaw(int sessionNumber)
    {
        return new {
            Telemetry = new { SessionNum = sessionNumber },
            SessionData = new { SessionInfo = new { CurrentSessionNum = 2, Sessions = new object[] {
                new { SessionNum = 2, SessionType = "Race", QualifyPositions = new object[] {
                    new { CarIdx = 0, ClassPosition = 0 },
                    new { CarIdx = 1, ClassPosition = 1 },
                    new { CarIdx = 2, ClassPosition = 2 },
                    new { CarIdx = 3, ClassPosition = 2 },
                    new { CarIdx = 4, ClassPosition = 0 },
                    new { CarIdx = 5, ClassPosition = 1 }
                } }
            } } }
        };
    }

    private static void StartGridRecovery()
    {
        object raw = StartingGridRaw(2);
        int[] order = new int[64];
        Require(RelativeSessionReader.ReadStartingClassOrder(raw, order), "read current race QualifyPositions");
        int[] expectedOrder = { 1, 2, 3, 3, 1, 2 };
        for (int i = 0; i < expectedOrder.Length; i++)
            Eq(expectedOrder[i], order[i], "zero-based historical class grid converted once");

        object global = new {
            Telemetry = new { SessionNum = 2 },
            SessionData = new {
                SessionInfo = new { Sessions = new object[] { new { SessionNum = 2, SessionType = "Race" } } },
                QualifyResultsInfo = new { Results = new object[] {
                    new { CarIdx = 0, ClassPosition = 1 }, new { CarIdx = 1, ClassPosition = 2 },
                    new { CarIdx = 2, ClassPosition = 0 }
                } }
            }
        };
        Require(RelativeSessionReader.ReadStartingClassOrder(global, order), "global qualifying ClassPosition fallback");
        int[] expectedGlobal = { 2, 3, 1 };
        for (int i = 0; i < 3; i++) Eq(expectedGlobal[i], order[i], "global qualifying order remains per class");

        object prior = new {
            Telemetry = new { SessionNum = 2 },
            SessionData = new { SessionInfo = new { Sessions = new object[] {
                new { SessionNum = 1, SessionType = "Lone Qualify", ResultsPositions = new object[] {
                    new { CarIdx = 0, ClassPosition = 2 }, new { CarIdx = 1, ClassPosition = 0 },
                    new { CarIdx = 2, ClassPosition = 1 }
                } },
                new { SessionNum = 2, SessionType = "Race", ResultsPositions = new object[0] }
            } } }
        };
        Require(RelativeSessionReader.ReadStartingClassOrder(prior, order), "preceding qualifying-session fallback");
        int[] expectedPrior = { 3, 1, 2 };
        for (int i = 0; i < 3; i++) Eq(expectedPrior[i], order[i], "preceding qualifying order converted once");

        object currentInfo = new {
            Telemetry = new { SessionNum = 2 },
            CurrentSessionInfo = new { SessionNum = 2, QualifyPositions = new object[] {
                new { CarIdx = 0, ClassPosition = 0 }, new { CarIdx = 1, ClassPosition = 1 },
                new { CarIdx = 2, ClassPosition = 2 }
            } }
        };
        Require(RelativeSessionReader.ReadStartingClassOrder(currentInfo, order), "matching CurrentSessionInfo fallback");
        for (int i = 0; i < 3; i++) Eq(i + 1, order[i], "CurrentSessionInfo class order");

        // A newly constructed resolver represents SimHub restarting while the
        // existing iRacing race remains in progress.
        var restart = new Fixture(3, 3); int[] a = restart.Groups[0], b = restart.Groups[1];
        restart.Set(a, new int[] { 2, 1, 3 }, new int[] { 2, 1, 3 });
        restart.Set(b, new int[] { 1, 3, 2 }, new int[] { 4, 6, 5 });
        restart.R.Update(restart.B, restart.S, true, 4, raw);
        int[] gainA = { -1, 1, 0 }, gainB = { 2, -2, 0 };
        for (int i = 0; i < 3; i++)
        {
            Eq(gainA[i], restart.B[a[i]].PositionGainLoss, "mid-race restart class A gain");
            Eq(gainB[i], restart.B[b[i]].PositionGainLoss, "mid-race restart class B gain");
        }

        // If metadata is momentarily absent, the live reference is explicitly
        // provisional and is upgraded when the historical grid arrives.
        var delayed = new Fixture(3); int[] g = delayed.Groups[0];
        delayed.Set(g, new int[] { 2, 1, 3 }, new int[] { 2, 1, 3 });
        delayed.R.Update(delayed.B, delayed.S, true, 4, null);
        for (int i = 0; i < 3; i++) Eq(0, delayed.B[g[i]].PositionGainLoss, "provisional baseline starts at zero");
        delayed.R.Update(delayed.B, delayed.S, true, 4, raw);
        for (int i = 0; i < 3; i++) Eq(gainA[i], delayed.B[g[i]].PositionGainLoss, "late start metadata upgrades provisional baseline");
        delayed.Set(g, new int[] { 3, 1, 2 }, new int[] { 3, 1, 2 });
        foreach (int flags in new int[] { 0x4000, 0x8000, 0x200, 0x4000 })
        {
            object caution = new { Telemetry = new { SessionNum = 2, SessionFlags = flags } };
            delayed.R.Update(delayed.B, delayed.S, true, 4, caution);
            int[] expectedGain = { -2, 1, 1 };
            for (int i = 0; i < 3; i++) Eq(expectedGain[i], delayed.B[g[i]].PositionGainLoss,
                "pits/tow/extended caution cannot recapture historical grid");
        }

        var slow = new Fixture(3); int[] sg = slow.Groups[0];
        slow.Set(sg, new int[] { 2, 1, 3 }, new int[] { 2, 1, 3 });
        for (int frame = 0; frame < 130; frame++) slow.R.Update(slow.B, slow.S, true, 4, null);
        for (int frame = 0; frame < 61; frame++) slow.R.Update(slow.B, slow.S, true, 4, raw);
        for (int i = 0; i < 3; i++) Eq(gainA[i], slow.B[sg[i]].PositionGainLoss,
            "throttled long-delay polling still recovers historical grid");

        // A complete order actually observed before green has priority over a
        // conflicting metadata order (for example a grid penalty).
        var observed = new Fixture(3); int[] og = observed.Groups[0];
        observed.Set(og, new int[] { 2, 1, 3 }, new int[] { 2, 1, 3 });
        observed.R.Update(observed.B, observed.S, true, 3, raw);
        observed.Set(og, new int[] { 1, 2, 3 }, new int[] { 1, 2, 3 });
        observed.R.Update(observed.B, observed.S, true, 4, raw);
        int[] observedGain = { 1, -1, 0 };
        for (int i = 0; i < 3; i++) Eq(observedGain[i], observed.B[og[i]].PositionGainLoss,
            "observed formation order outranks historical metadata");

        Require(!RelativeSessionReader.ReadStartingClassOrder(StartingGridRaw(9), order),
            "stale session historical grid rejected");
        for (int i = 0; i < order.Length; i++) Eq(0, order[i], "failed historical read clears destination");

        object duplicate = new {
            Telemetry = new { SessionNum = 2 },
            SessionData = new { SessionInfo = new { Sessions = new object[] {
                new { SessionNum = 2, QualifyPositions = new object[] {
                    new { CarIdx = 0, ClassPosition = 0 }, new { CarIdx = 0, ClassPosition = 1 }
                } }
            } } }
        };
        Require(!RelativeSessionReader.ReadStartingClassOrder(duplicate, order), "duplicate CarIdx historical rows rejected");
        for (int i = 0; i < order.Length; i++) Eq(0, order[i], "duplicate historical read clears destination");
    }

    public static int Run()
    {
        ExhaustiveSources(); LifecycleAndSizes(); CacheMembershipAndReset(); GridAndNonCompetitors();
        AllSessionsAndPartialAiRanks(); StartGridRecovery();
        Console.WriteLine("Class positions: PASS (" + checks + " assertions)");
        return checks;
    }
}
