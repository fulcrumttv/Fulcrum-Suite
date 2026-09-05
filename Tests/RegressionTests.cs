using System;
using Fulcrum.Core.Relative;
using Fulcrum.Core.Relative.Display;
using Fulcrum.Core.Session;

internal static class RegressionTests
{
    private static int checks;
    private static void Equal(int expected, int actual, string message)
    {
        checks++;
        if (expected != actual) throw new Exception(message + ": expected " + expected + ", got " + actual);
    }
    private static void Set(ParticipantBuffer b, int i, int lap, double pct)
    {
        ParticipantSnapshot p = b[i];
        p.IsValid = true; p.Lap = lap; p.LapCompleted = lap - 1;
        p.LapDistancePercent = (float)pct; p.TrackSurface = 3;
    }
    private static void CrossingSequence(bool early, bool firstLap)
    {
        ParticipantBuffer b = new ParticipantBuffer();
        RelativeLapTracker tracker = new RelativeLapTracker();
        tracker.Reset();
        int frames = firstLap ? 1500 : 450;
        for (int tick = 0; tick < frames; tick++)
        {
            double progress = (firstLap ? -0.15 : 2.80) + tick * 0.001;
            for (int i = 0; i < 3; i++)
            {
                double v = progress + (i - 1) * 0.018;
                double raw = v + (early ? 0.003 : -0.003);
                Set(b, i, (int)Math.Floor(raw) + 1, v - Math.Floor(v));
            }
            tracker.SetContext(true, tick * 0.05);
            tracker.Update(b);
            Equal(0, tracker.LapDifference(b[1], b[0]), "same-lap behind crossing");
            Equal(0, tracker.LapDifference(b[1], b[2]), "same-lap ahead crossing");
        }
    }
    private static void LappingAndReset()
    {
        ParticipantBuffer b = new ParticipantBuffer();
        RelativeLapTracker tracker = new RelativeLapTracker(); tracker.Reset();
        Set(b, 0, 5, 0.10); Set(b, 1, 6, 0.08);
        b[0].ClassId = 100; b[1].ClassId = 200;
        tracker.SetContext(true, 400); tracker.Update(b);
        Equal(1, tracker.LapDifference(b[0], b[1]), "other-class lapper behind");
        Equal(-1, tracker.LapDifference(b[1], b[0]), "other-class slower ahead");
        tracker.Reset(); Set(b, 0, 1, 0.50); Set(b, 1, 1, 0.51);
        tracker.SetContext(true, 0); tracker.Update(b);
        Equal(0, tracker.LapDifference(b[0], b[1]), "restart has neutral first lap");
        tracker.SetContext(false, 0); tracker.Update(b);
        Equal(0, tracker.LapDifference(b[0], b[1]), "practice has neutral lap colors");
    }
    private static void StationaryFinishZone()
    {
        foreach (double pct in new double[] { 0, .001, .01, .029, .971, .99, .999, 1 })
        foreach (int player in new int[] { 0, 17, 63 })
        foreach (int reason in new int[] { 0, 1, 2 })
        {
            ParticipantBuffer b = new ParticipantBuffer();
            RelativeLapTracker tracker = new RelativeLapTracker(); tracker.Reset();
            int other = (player + 1) % 64;
            double start = 1 + pct + .96;
            Set(b, player, 2, reason == 0 ? pct : .20);
            Set(b, other, (int)Math.Floor(start) + 1, start % 1);
            b[player].ClassId = 100; b[other].ClassId = 200;
            tracker.SetContext(true, 0); tracker.Update(b);
            if (reason == 2)
            {
                b[player].IsValid = false;
                tracker.SetContext(true, .1); tracker.Update(b);
            }
            for (int tick = 0; tick <= 2200; tick++)
            {
                double v = start + tick * .0015;
                Set(b, player, 2, pct);
                b[player].TrackSurface = 1; b[player].IsOnPitRoad = true;
                Set(b, other, (int)Math.Floor(v) + 1, v % 1);
                tracker.SetContext(true, 1 + tick * .05); tracker.Update(b);
                if (tick < 60) continue;
                double offset = (double)b[other].LapDistancePercent - b[player].LapDistancePercent;
                int expected = b[other].Lap - b[player].Lap;
                if (offset > .5) expected++;
                else if (offset < -.5) expected--;
                Equal(expected, tracker.LapDifference(b[player], b[other]), "stationary red: before/after several lappings");
                Equal(-expected, tracker.LapDifference(b[other], b[player]), "stationary rival blue: every class and CarIdx");
            }
        }
        ParticipantBuffer stable = new ParticipantBuffer();
        RelativeLapTracker anchor = new RelativeLapTracker(); anchor.Reset();
        Set(stable, 0, 2, .01); Set(stable, 1, 3, .20);
        for (int n = 0; n < 50; n++) { anchor.SetContext(true, 0); anchor.Update(stable); }
        Equal(0, anchor.LapDifference(stable[0], stable[1]), "frozen time is not stable evidence");
        anchor.SetContext(true, 2); anchor.Update(stable);
        Equal(0, anchor.LapDifference(stable[0], stable[1]), "require three distinct timestamps");
        anchor.SetContext(true, 2.1); anchor.Update(stable);
        Equal(1, anchor.LapDifference(stable[0], stable[1]), "stationary anchor recovered");
        anchor.SetContext(false, 3); anchor.Update(stable);
        Equal(0, anchor.LapDifference(stable[0], stable[1]), "formation/practice remains neutral");
    }

    private static void MulticlassAndDisplay()
    {
        ParticipantBuffer b = new ParticipantBuffer();
        SessionDatabase session = new SessionDatabase();
        ClassPositionResolver resolver = new ClassPositionResolver(); resolver.Reset();
        int[] sizes = { 14, 12, 14 };
        int car = 0;
        for (int c = 0; c < sizes.Length; c++)
        {
            for (int pos = 1; pos <= sizes[c]; pos++, car++)
            {
                Set(b, car, 3, 0.2 + car * 0.005);
                b[car].OverallPosition = car + 1;
                b[car].ClassPosition = 0;
                b[car].IsOnPitRoad = true;
                b[car].TrackSurface = 1;
                session.SetDriver(car, "Driver " + car, car.ToString(), "", "Class " + c);
                session.GetWritable(car).SetClassIdentity(100 + c, false);
            }
        }
        resolver.Update(b, session, true, 3);
        Equal(1, b[0].ClassPosition, "first class P1");
        Equal(1, b[14].ClassPosition, "second class P1");
        Equal(1, b[26].ClassPosition, "third class P1");
        Equal(14, b[39].ClassPosition, "overall P40 is class P14");
        Equal(14, b[39].ClassSize, "full class roster includes pit cars");
        b[39].ClassPosition = 40;
        resolver.Update(b, session, true, 4);
        Equal(14, b[39].ClassPosition, "invalid P40 cannot bypass class bound");
        Equal(0, b[39].PositionGainLoss, "class-based gain/loss");

        b[39].IsPlayer = true;
        RelativeCalculator calculator = new RelativeCalculator();
        RelativeSnapshot snapshot = new RelativeSnapshot();
        calculator.SetLapColorContext(true, 200);
        calculator.Calculate(b, snapshot);
        RelativeDisplaySnapshot display = new RelativeDisplaySnapshot();
        new RelativeDisplayBuilder().Build(b, snapshot, session, new StintTracker(), display);
        Equal(14, display.Player.ClassPosition, "class rank reaches player display");
        Equal(14, display.Player.ClassSize, "class count reaches player display");
        Equal(0, display.Player.PositionGainLoss, "class gain reaches player display");
    }
    private static int Main()
    {
        try
        {
            CrossingSequence(false, false); CrossingSequence(true, false);
            CrossingSequence(false, true); CrossingSequence(true, true);
            LappingAndReset(); StationaryFinishZone(); MulticlassAndDisplay();
            checks += ClassPositionTests.Run();
            Console.WriteLine("Fulcrum Core regression tests: PASS (" + checks + " assertions)");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("REGRESSION FAILED: " + e);
            return 1;
        }
    }
}
