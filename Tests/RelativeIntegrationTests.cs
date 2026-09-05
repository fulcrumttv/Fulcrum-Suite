using System;
using System.Collections.Generic;
using System.Reflection;
using Fulcrum.Core.Performance;
using Fulcrum.Core.Relative;
using Fulcrum.Core.Session;
using Fulcrum.Plugin.Modules;
using Fulcrum.Plugin.Settings;
using SimHub.Plugins;

// Compiles the ACTUAL RelativeModule, its actual three publishers and Core.
// The only substitute is SimHub's property registry; no race logic is mocked.
public static class RelativeIntegrationTests
{
    public sealed class RawFrame
    {
        public Dictionary<string, object> Telemetry { get; set; }
        public object SessionData { get; set; }
        public object CurrentSessionInfo { get; set; }
    }
    private enum StateValue { ParadeLaps = 3, Racing = 4 }
    private static int checks;
    private static Dictionary<string, object> D(params object[] values)
    {
        var d = new Dictionary<string, object>();
        for (int i = 0; i < values.Length; i += 2) d[(string)values[i]] = values[i + 1];
        return d;
    }
    private static void Equal(object expected, object actual, string message)
    {
        checks++;
        if (!object.Equals(expected, actual)) throw new Exception(message + ": expected " + expected + ", actual " + actual);
    }
    private sealed class Harness
    {
        public readonly RawFrame Frame;
        public readonly PluginManager Props = new PluginManager();
        public readonly SessionDatabase Session = new SessionDatabase();
        public readonly RelativeModule Module;
        public readonly int[] Lap = new int[64], Completed = new int[64], Surface = new int[64];
        public readonly int[] Position = new int[64], ClassPosition = new int[64], Class = new int[64];
        public readonly float[] Pct = new float[64];
        public readonly bool[] Pit = new bool[64];
        private readonly MethodInfo update = typeof(RelativeModule).GetMethod("UpdateScheduled", BindingFlags.Instance | BindingFlags.NonPublic);

        public Harness(string type = "Race", int number = 2, int state = 3)
        {
            var drivers = new List<object>();
            var qualifying = new List<object>();
            for (int i = 0; i < 64; i++)
            {
                Lap[i] = Completed[i] = Surface[i] = Class[i] = -1; Pct[i] = -1;
                if (i >= 6) continue;
                Class[i] = 100 + i / 3;
                drivers.Add(D("CarIdx", i, "UserName", "Driver " + i, "CarNumber", (i + 10).ToString(),
                    "CarClassID", Class[i], "CarClassShortName", "Class " + Class[i], "CarIsPaceCar", 0, "IsSpectator", 0));
                qualifying.Add(D("CarIdx", i, "Position", i));
                Car(i, 0, .90 + i * .003);
            }
            Frame = new RawFrame {
                Telemetry = D("SessionState", state, "SessionNum", number, "SessionTime", 0.0,
                    "PlayerCarIdx", 0, "CarIdxLap", Lap, "CarIdxLapCompleted", Completed,
                    "CarIdxLapDistPct", Pct, "CarIdxTrackSurface", Surface, "CarIdxOnPitRoad", Pit,
                    "CarIdxPosition", Position, "CarIdxClassPosition", ClassPosition, "CarIdxClass", Class),
                SessionData = D("DriverInfo", D("Drivers", drivers), "QualifyResultsInfo", D("Results", qualifying),
                    "SessionInfo", D("Sessions", new object[] { D("SessionNum", number, "SessionType", type) })),
                CurrentSessionInfo = D("SessionType", type)
            };
            var reader = new SessionInfoReader();
            reader.Update(Frame, Session);
            Equal(6, Session.ValidDriverCount, "actual SessionInfoReader loads six drivers");
            Equal(101, Session.Get(3).ClassId, "actual SessionInfoReader loads class identity");
            var settings = new RelativeOverlaySettings { ShowGainLoss = true };
            Module = new RelativeModule(Props, typeof(RelativeIntegrationTests), new UpdateScheduler(), Session, settings);
        }
        public void Car(int i, int lap, double pct, bool pit = false)
        {
            Lap[i] = lap; Completed[i] = lap - 1; Pct[i] = (float)pct;
            Surface[i] = pit ? 1 : 3; Pit[i] = pit;
        }
        public void Step(object state, double time, string type = "Race", int number = 2)
        {
            Frame.Telemetry["SessionState"] = state;
            Frame.Telemetry["SessionTime"] = time;
            Frame.Telemetry["SessionNum"] = number;
            Frame.CurrentSessionInfo = D("SessionType", type);
            Module.SetFrameContext(Frame, true, 0, "");
            update.Invoke(Module, null);
        }
        public object Row(int car, string field)
        {
            for (int row = 1; row <= 9; row++)
            {
                string prefix = "Fulcrum.Relative.Table.Row" + row.ToString("00") + ".";
                if ((bool)Props.Get(prefix + "Visible") && (int)Props.Get(prefix + "CarIndex") == car)
                    return Props.Get(prefix + field);
            }
            throw new Exception("Car not published: " + car);
        }
        public void Positions()
        {
            for (int i = 0; i < 6; i++) { Position[i] = i + 1; ClassPosition[i] = i % 3 + 1; }
        }
        public ParticipantBuffer Participants
        {
            get { return (ParticipantBuffer)typeof(RelativeModule).GetField("participantBuffer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Module); }
        }
        public void SetStartingGrid()
        {
            var data = (Dictionary<string, object>)Frame.SessionData;
            var info = (Dictionary<string, object>)data["SessionInfo"];
            var sessions = (object[])info["Sessions"];
            var current = (Dictionary<string, object>)sessions[0];
            current["QualifyPositions"] = new object[] {
                D("CarIdx", 0, "ClassPosition", 0), D("CarIdx", 1, "ClassPosition", 1),
                D("CarIdx", 2, "ClassPosition", 2), D("CarIdx", 3, "ClassPosition", 2),
                D("CarIdx", 4, "ClassPosition", 0), D("CarIdx", 5, "ClassPosition", 1)
            };
            data.Remove("QualifyResultsInfo");
        }
    }

    private static void NestedContext()
    {
        var h = new Harness();
        Equal(null, h.Frame.GetType().GetProperty("SessionState"), "old root lookup cannot read SessionState");
        h.Step(StateValue.ParadeLaps, 0);
        Equal(3, h.Props.Get("Fulcrum.Relative.Context.SessionState"), "boxed enum nested state");
        h.Step("Racing", 1);
        Equal(true, h.Props.Get("Fulcrum.Relative.Context.LapColorsEnabled"), "named nested state enables colors");
        h.Frame.CurrentSessionInfo = null;
        Equal("Race", RelativeSessionReader.SessionType(h.Frame, ""), "SessionData fallback selects current session");
        var dictionaryFrame = D("Telemetry", D("SessionState", 4), "SessionState", 1);
        Equal(4, RelativeSessionReader.State(dictionaryFrame), "nested dictionary takes precedence over root");
    }

    private static void PracticeGridGreenAndGain()
    {
        var h = new Harness("Practice", 0, 4);
        for (int i = 0; i < 6; i++) h.Car(i, 8 + i, .7 + i * .002);
        h.Step(4, 500, "Practice", 0);
        h.Step(4, 501, "Practice", 0);
        for (int i = 0; i < 6; i++) h.Car(i, 0, .90 + i * .003, i % 2 == 0);
        h.Step(1, 0);
        for (int i = 0; i < 6; i++) h.Car(i, 0, .91 + i * .003);
        h.Step(3, 1);
        for (int i = 0; i < 6; i++)
        {
            Equal(false, h.Row(i, "IsOutLap"), "grid/formation is not OUT");
            Equal(0, h.Row(i, "StintLap"), "formation has not begun a timed stint");
            Equal(i % 3 + 1, h.Row(i, "Position"), "pre-green grid comes from class qualifying order");
        }
        h.Positions();
        h.Step(4, 2);
        for (int i = 0; i < 6; i++)
        {
            Equal("L1", h.Row(i, "StatusStintText"), "all drivers start on L1 before their line crossing");
            Equal(true, h.Row(i, "PositionGainLossAvailable"), "zero pre-green telemetry did not freeze an empty grid");
            Equal(0, h.Row(i, "PositionGainLoss"), "unchanged grid position has zero gain");
        }
        h.ClassPosition[0] = 2; h.ClassPosition[1] = 1;
        h.Position[0] = 2; h.Position[1] = 1;
        h.Step(4, 3);
        Equal(-1, h.Row(0, "PositionGainLoss"), "player loses one within class");
        Equal(1, h.Row(1, "PositionGainLoss"), "rival gains one within class");
    }

    private static void StandingStartAndFirstLap()
    {
        var h = new Harness();
        h.Step(1, 0); // standing start, no parade-state sample required
        h.Positions();
        for (int tick = 0; tick < 1200; tick++)
        {
            double p = -.10 + tick * .001;
            for (int i = 0; i < 6; i++)
            {
                double v = p + i * .003;
                h.Car(i, (int)Math.Floor(v) + 1, v - Math.Floor(v));
                h.Completed[i] = -1; // opponents' completed counter may be unavailable
            }
            h.Step(4, 1 + tick * .05);
            for (int i = 0; i < 6; i++)
            {
                Equal(false, h.Row(i, "IsOutLap"), "standing start never becomes OUT");
                Equal(0, h.Row(i, "LapDifference"), "staggered first-lap crossing stays neutral");
                int expected = p + i * .003 < 1 ? 1 : 2;
                Equal(expected, h.Row(i, "StintLap"), "one numbering policy for every driver");
            }
        }
    }

    private static void NativeGridAtGreen()
    {
        var h = new Harness();
        ((Dictionary<string, object>)h.Frame.SessionData).Remove("QualifyResultsInfo");
        h.Step(3, 0);
        h.Positions();
        h.Step(4, 1);
        Equal(true, h.Row(0, "PositionGainLossAvailable"), "capture native grid becoming available at green");
        h.ClassPosition[0] = 2; h.ClassPosition[1] = 1;
        h.Step(4, 2);
        Equal(-1, h.Row(0, "PositionGainLoss"), "native starting grid survives later position changes");
    }

    private static void OfflineAiGainLoss()
    {
        var h = new Harness("Offline Testing", 0, 4); h.Positions();
        h.Step(4, 100, "Offline Testing", 0);
        for (int i = 0; i < 6; i++)
        {
            Equal(true, h.Row(i, "PositionGainLossAvailable"), "offline AI baseline is available for every driver");
            Equal(0, h.Row(i, "PositionGainLoss"), "offline AI baseline starts at zero");
        }
        // Lap telemetry deliberately stays unchanged: +/- must react to class
        // classification updates, not wait for a finish-line crossing.
        h.ClassPosition[0] = 2; h.ClassPosition[1] = 1;
        h.Position[0] = 2; h.Position[1] = 1;
        h.Step(4, 101, "Offline Testing", 0);
        Equal(-1, h.Row(0, "PositionGainLoss"), "offline AI loss updates immediately");
        Equal(1, h.Row(1, "PositionGainLoss"), "offline AI gain updates immediately");

        // SimHub can reuse SessionNum while changing the selected session type.
        // That is a new baseline, not a continuation of the offline race.
        ((Dictionary<string, object>)h.Frame.SessionData).Remove("QualifyResultsInfo");
        h.Step(4, 102, "Race", 0);
        Equal(0, h.Row(0, "PositionGainLoss"), "race/non-race mode change resets only the class reference");
        Equal(0, h.Row(1, "PositionGainLoss"), "new class mode starts evenly");
    }

    private static void StartGridRecoveryThroughModule()
    {
        // Fresh Harness/Module instances simulate restarting SimHub without
        // restarting the active iRacing race.
        var h = new Harness("Race", 2, 4); h.SetStartingGrid(); h.Positions();
        h.ClassPosition[0] = 2; h.ClassPosition[1] = 1;
        h.Position[0] = 2; h.Position[1] = 1;
        h.ClassPosition[3] = 1; h.ClassPosition[4] = 3; h.ClassPosition[5] = 2;
        h.Position[3] = 4; h.Position[4] = 6; h.Position[5] = 5;
        h.Step(4, 500);
        Equal(-1, h.Row(0, "PositionGainLoss"), "module restart restores original class-A start");
        Equal(1, h.Row(1, "PositionGainLoss"), "module restart publishes class-A gain");
        Equal(2, h.Row(3, "PositionGainLoss"), "module restart restores independent class-B start");
        Equal(-2, h.Row(4, "PositionGainLoss"), "module restart publishes independent class-B loss");

        var delayed = new Harness("Race", 2, 4);
        ((Dictionary<string, object>)delayed.Frame.SessionData).Remove("QualifyResultsInfo");
        delayed.Positions(); delayed.ClassPosition[0] = 2; delayed.ClassPosition[1] = 1;
        delayed.Position[0] = 2; delayed.Position[1] = 1;
        delayed.Step(4, 600);
        Equal(0, delayed.Row(0, "PositionGainLoss"), "module provisional start begins at zero");
        delayed.SetStartingGrid(); delayed.Step(4, 601);
        Equal(-1, delayed.Row(0, "PositionGainLoss"), "module upgrades when start metadata arrives late");
        delayed.ClassPosition[0] = 3; delayed.ClassPosition[1] = 1; delayed.ClassPosition[2] = 2;
        delayed.Position[0] = 3; delayed.Position[1] = 1; delayed.Position[2] = 2;
        var delayedData = (Dictionary<string, object>)delayed.Frame.SessionData;
        var delayedInfo = (Dictionary<string, object>)delayedData["SessionInfo"];
        ((Dictionary<string, object>)((object[])delayedInfo["Sessions"])[0]).Remove("QualifyPositions");
        double cautionTime = 602;
        foreach (int flags in new int[] { 0x4000, 0x8000, 0x200, 0x4000 })
        {
            delayed.Frame.Telemetry["SessionFlags"] = flags;
            delayed.Step(4, cautionTime++);
            Equal(-2, delayed.Row(0, "PositionGainLoss"), "extended yellow cannot recapture upgraded start");
        }

        var observed = new Harness("Race", 2, 3); observed.SetStartingGrid(); observed.Positions();
        observed.ClassPosition[0] = 2; observed.ClassPosition[1] = 1;
        observed.Position[0] = 2; observed.Position[1] = 1;
        observed.Step(3, 0);
        observed.Positions(); observed.Step(4, 1);
        Equal(1, observed.Row(0, "PositionGainLoss"), "observed formation grid survives conflicting history");
        Equal(-1, observed.Row(1, "PositionGainLoss"), "observed formation classmate reference survives green");
    }

    private static void LappersAcrossClasses()
    {
        var h = new Harness(); h.Positions();
        for (int i = 0; i < 6; i++) h.Car(i, 4, .50 + i * .003);
        h.Car(3, 5, .49); // different class, will lap the player from behind
        h.Car(4, 3, .51); // different class, player is approaching to lap it
        h.Step(4, 400);
        Equal(true, h.Props.Get("Fulcrum.Relative.Context.LapColorsEnabled"), "race phase reaches actual module");
        Equal(true, h.Row(3, "IsAheadByLap"), "red lapper published across classes");
        Equal(true, h.Row(4, "IsLappedByPlayer"), "blue backmarker published across classes");
        Equal(false, h.Row(3, "IsSameClass"), "lapper genuinely belongs to another class");
        h.Car(3, 5, .505); h.Step(4, 401);
        Equal(true, h.Row(3, "IsAheadByLap"), "red remains when lapper moves ahead");
    }

    private static void RealPitExitAndStints()
    {
        var h = new Harness(); h.Positions();
        for (int i = 0; i < 6; i++) h.Car(i, 4, .90 + i * .003);
        h.Step(4, 300);
        h.Car(0, 4, .92, true); h.Car(1, 4, .925, true); h.Step(4, 301);
        Equal("PIT", h.Row(0, "Status"), "real pit entry");
        h.Car(0, 4, .94); h.Car(1, 4, .945); h.Step(4, 302);
        Equal("OUT", h.Row(0, "StatusStintText"), "player real pit exit is OUT");
        Equal("OUT", h.Row(1, "StatusStintText"), "opponent real pit exit is OUT");
        h.Car(0, 4, .995); h.Car(1, 4, .997); h.Step(4, 303);
        h.Car(0, 5, .005); h.Car(1, 5, .007); h.Step(4, 304);
        Equal("L1", h.Row(0, "StatusStintText"), "first timed lap after OUT is L1");
        Equal("L1", h.Row(1, "StatusStintText"), "same post-pit numbering for opponent");
        for (int t = 1; t <= 101; t++)
        {
            double p = .005 + t * .01;
            h.Car(0, p >= 1 ? 6 : 5, p % 1);
            h.Car(1, p + .002 >= 1 ? 6 : 5, (p + .002) % 1);
            h.Step(4, 304 + t * .1);
        }
        Equal("L2", h.Row(0, "StatusStintText"), "next complete stint lap is L2");
        Equal("L2", h.Row(1, "StatusStintText"), "opponent advances to L2 identically");
    }

    private static void HighlightsDuringLongPitStop()
    {
        foreach (double pct in new double[] { .01, .99 })
        foreach (bool dropout in new bool[] { false, true })
        {
            var h = new Harness(); h.Positions();
            for (int i = 0; i < 6; i++) h.Car(i, 2, .2 + i * .003);
            h.Step(4, 300); // trusted track sample before entering/teleporting to pits
            if (dropout)
            {
                h.Car(0, -1, -1);
                h.Step(4, 300.1);
            }
            // Two same/different-class lappers, two same/different-class
            // backmarkers, and one same-lap car surround the stationary player.
            h.Car(0, 2, pct, true);
            h.Car(2, 1, pct + .003); h.Car(4, 1, pct + .006);
            h.Car(5, 2, pct + .004);
            // Avoid an exactly identical physical sample: the production
            // relative intentionally omits an exact zero-distance tie.
            double start = 1 + pct + .98037;
            for (int tick = 0; tick <= 1500; tick++)
            {
                double v = start + tick * .002;
                h.Car(1, (int)Math.Floor(v) + 1, v % 1);
                h.Car(3, (int)Math.Floor(v - .003) + 1, (v - .003) % 1);
                h.Step(4, 301 + tick * .1);
                if (tick < 30) continue;
                Equal("PIT", h.Row(0, "Status"), "player remains in pits throughout multiple lappings");
                Equal(true, h.Row(1, "IsAheadByLap"), "same-class lapper stays red before/after passing pits");
                Equal(true, h.Row(3, "IsAheadByLap"), "other-class lapper stays red before/after passing pits");
                Equal(true, h.Row(2, "IsLappedByPlayer"), "same-class blue recovers inside guard zone");
                Equal(true, h.Row(4, "IsLappedByPlayer"), "other-class blue recovers inside guard zone");
                Equal(0, h.Row(5, "LapDifference"), "same-lap car stays neutral during the pit stop");
                Equal(false, h.Row(3, "IsSameClass"), "cross-class fixture remains genuinely multiclasse");
            }
        }
    }

    private static void CoherentClassPositionsAcrossLifecycle()
    {
        var h = new Harness(); h.Positions(); h.Step(3, 0);
        for (int i = 0; i < 6; i++) h.Car(i, 2, .30 + i * .003);
        h.ClassPosition[0] = 0; h.Position[0] = 6; h.Position[1] = 1; h.Position[2] = 2;
        h.Car(0, 2, .30, true); h.Step(4, 1);
        Equal(3, h.Row(0, "Position"), "pit car uses whole-class overall fallback");
        Equal(1, h.Row(1, "Position"), "classmate rank recalculated too, no duplicate P3");
        Equal(2, h.Row(2, "Position"), "third classmate belongs to same snapshot");
        Equal(-2, h.Row(0, "PositionGainLoss"), "class-based loss reaches publisher");
        for (int i = 0; i < 3; i++) h.ClassPosition[i] = h.Position[i] = 0;
        h.Step(4, 2);
        Equal(3, h.Row(0, "Position"), "missing telemetry retains coherent class cache");

        var results = new List<object>();
        for (int i = 0; i < 6; i++) results.Add(D("CarIdx", i, "ClassPosition", i % 3, "Position", i + 1));
        ((Dictionary<string, object>)h.Frame.SessionData)["SessionInfo"] = D("Sessions", new object[] {
            D("SessionNum", 1, "SessionType", "Lone Qualify", "ResultsPositions", new object[] { D("CarIdx", 0, "ClassPosition", 0, "Position", 1) }),
            D("SessionNum", 2, "SessionType", "Race", "ResultsPositions", results)
        });
        // Real module/reader/publisher route under four extended caution laps.
        for (int tick = 0; tick < 960; tick++)
        {
            int[] expected = new int[3];
            for (int i = 0; i < 3; i++)
            {
                expected[i] = (i + tick / 120) % 3 + 1;
                var row = (Dictionary<string, object>)results[i];
                row["ClassPosition"] = expected[i] - 1; row["Position"] = expected[i];
                h.ClassPosition[i] = h.Position[i] = 0;
            }
            h.Frame.Telemetry["SessionFlags"] = tick < 480 ? 0x4000 : tick < 720 ? 0x8000 : 0x200;
            // Loss of geometry in the garage clears the visible relative, but
            // must NOT remove that car from the registered class or freeze its
            // official classification when current results keep arriving.
            bool garage = tick >= 240 && tick < 480;
            h.Car(0, garage ? -1 : 2 + tick / 240, garage ? -1 : .30, !garage && tick < 720);
            h.Step(4, 10 + tick);
            for (int i = 0; i < 3; i++)
            {
                EqParticipant(h, i, expected[i], i + 1 - expected[i]);
                if (!garage)
                {
                    Equal(expected[i], h.Row(i, "Position"), "current-session result reaches visible class position");
                    Equal(i + 1 - expected[i], h.Row(i, "PositionGainLoss"), "yellow flag never recaptures class grid");
                }
            }
        }
        h.ClassPosition[0] = 3; h.ClassPosition[1] = 2; h.ClassPosition[2] = 1;
        h.Step(4, 1000);
        Equal(3, h.Row(0, "Position"), "valid live telemetry wins again on recovery");
        for (int i = 0; i < 3; i++) h.ClassPosition[i] = h.Position[i] = 0;
        h.Step(4, 0, "Race", 3);
        for (int i = 0; i < 3; i++)
        {
            Equal(0, h.Row(i, "Position"), "new session cannot reuse old class cache or old results");
            Equal(false, h.Row(i, "PositionGainLossAvailable"), "new session has no fabricated gain");
        }
    }

    private static void EqParticipant(Harness h, int car, int rank, int gain)
    {
        Equal(3, h.Participants[car].ClassSize, "pits/garage never change actual class population");
        Equal(rank, h.Participants[car].ClassPosition, "class ranking preserved in real module");
        Equal(gain, h.Participants[car].PositionGainLoss, "original grid preserved across lifecycle");
    }

    public static int Main()
    {
        try
        {
            NestedContext(); PracticeGridGreenAndGain(); NativeGridAtGreen(); StandingStartAndFirstLap();
            OfflineAiGainLoss(); StartGridRecoveryThroughModule();
            LappersAcrossClasses(); RealPitExitAndStints(); HighlightsDuringLongPitStop();
            CoherentClassPositionsAcrossLifecycle();
            Console.WriteLine("Relative source pipeline: PASS (" + checks + " assertions)");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("RELATIVE PIPELINE FAILED: " + error);
            return 1;
        }
    }
}
