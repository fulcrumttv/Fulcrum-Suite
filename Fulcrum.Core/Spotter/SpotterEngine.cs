using System;
using Fulcrum.Core.Damage;
using Fulcrum.Core.Intelligence;
using Fulcrum.Core.Radar;
using Fulcrum.Core.Telemetry;

namespace Fulcrum.Core.Spotter
{
    /// <summary>
    /// Converts Radar, flags, damage and race intelligence into concise
    /// spotter callouts. The engine suppresses repeated messages and emits
    /// a CLEAR event when side-by-side traffic ends.
    /// </summary>
    public sealed class SpotterEngine
    {
        private const double RepeatCooldownSeconds = 4.0;

        private bool previousRadarActive;
        private bool previousBlueFlag;
        private bool previousYellowFlag;
        private bool previousMeatballFlag;
        private bool previousFasterClassApproaching;
        private string lastEventCode;
        private DateTime lastEventUtc;
        private int eventSequence;

        public SpotterEngine()
        {
            ResetHistory();
        }

        public void Reset(SpotterSnapshot output)
        {
            ResetHistory();
            if (output != null)
            {
                output.Reset();
            }
        }

        public void Update(
            TelemetrySnapshot telemetry,
            RadarSnapshot radar,
            RaceIntelligenceSnapshot intelligence,
            DamageSnapshot damage,
            SpotterSnapshot output)
        {
            if (output == null)
            {
                return;
            }

            int existingSequence = eventSequence;
            output.Reset();
            output.EventSequence = existingSequence;
            output.UpdatedAtUtc = DateTime.UtcNow;

            if (telemetry == null || radar == null ||
                intelligence == null || damage == null ||
                !telemetry.GameRunning)
            {
                output.State = "Unavailable";
                return;
            }

            output.Ready = true;
            output.HasCarLeft = radar.HasCarLeft;
            output.HasCarRight = radar.HasCarRight;
            output.HasCarsBothSides = radar.HasCarsBothSides;
            output.IsClear = !radar.IsActive;
            output.BlueFlag = SessionStateInterpreter.HasBlue(telemetry.SessionFlags);
            output.YellowFlag = SessionStateInterpreter.HasYellow(telemetry.SessionFlags);
            output.MeatballFlag = damage.HasMeatballFlag;
            output.FasterClassApproaching = intelligence.FasterClassApproaching;
            output.DefenseRequired = intelligence.DefenseRequired;
            output.SuggestedAction = intelligence.SuggestedAction;

            Candidate candidate = SelectCandidate(telemetry, radar, intelligence, damage);
            ApplyCurrentState(candidate, output);

            bool shouldEmit = ShouldEmit(candidate, radar.IsActive, output);
            if (shouldEmit)
            {
                eventSequence++;
                lastEventCode = candidate.Code;
                lastEventUtc = DateTime.UtcNow;
                output.EventName = candidate.Code;
                output.EventSequence = eventSequence;
                output.HasActiveCallout = true;
            }
            else
            {
                output.EventName = "None";
                output.EventSequence = eventSequence;
                output.HasActiveCallout = false;
            }

            previousRadarActive = radar.IsActive;
            previousBlueFlag = output.BlueFlag;
            previousYellowFlag = output.YellowFlag;
            previousMeatballFlag = output.MeatballFlag;
            previousFasterClassApproaching = output.FasterClassApproaching;
        }

        private Candidate SelectCandidate(
            TelemetrySnapshot telemetry,
            RadarSnapshot radar,
            RaceIntelligenceSnapshot intelligence,
            DamageSnapshot damage)
        {
            if (damage.IsDisqualified)
            {
                return new Candidate("DISQUALIFIED", "DISQUALIFIED", 100, true, "Disqualified");
            }

            if (SessionStateInterpreter.HasRed(telemetry.SessionFlags))
            {
                return new Candidate("RED_FLAG", "RED FLAG", 95, true, "RedFlag");
            }

            if (damage.HasMeatballFlag)
            {
                return new Candidate("MEATBALL", "REPAIR FLAG", 90, true, "RepairFlag");
            }

            if (damage.HasBlackFlag)
            {
                return new Candidate("BLACK_FLAG", "BLACK FLAG", 88, true, "BlackFlag");
            }

            if (SessionStateInterpreter.HasYellow(telemetry.SessionFlags))
            {
                return new Candidate("YELLOW_FLAG", "YELLOW FLAG", 82, true, "YellowFlag");
            }

            if (radar.HasCarsBothSides)
            {
                return new Candidate("THREE_WIDE", "THREE WIDE", 80, true, "ThreeWide");
            }

            if (radar.HasTwoCarsLeft)
            {
                return new Candidate("TWO_LEFT", "TWO LEFT", 76, true, "TwoCarsLeft");
            }

            if (radar.HasTwoCarsRight)
            {
                return new Candidate("TWO_RIGHT", "TWO RIGHT", 76, true, "TwoCarsRight");
            }

            if (radar.HasCarLeft)
            {
                return new Candidate("CAR_LEFT", "CAR LEFT", 70, true, "CarLeft");
            }

            if (radar.HasCarRight)
            {
                return new Candidate("CAR_RIGHT", "CAR RIGHT", 70, true, "CarRight");
            }

            if (previousRadarActive && !radar.IsActive)
            {
                return new Candidate("CLEAR", "CLEAR", 68, false, "Clear");
            }

            if (SessionStateInterpreter.HasBlue(telemetry.SessionFlags))
            {
                return new Candidate("BLUE_FLAG", "BLUE FLAG", 55, false, "BlueFlag");
            }

            if (intelligence.FasterClassApproaching)
            {
                return new Candidate("FASTER_CLASS", "FASTER CLASS APPROACHING", 50, false, "FasterClass");
            }

            if (intelligence.DefenseRequired)
            {
                return new Candidate("DEFEND", "CAR CLOSING BEHIND", 35, false, "Defense");
            }

            return new Candidate("NONE", string.Empty, 0, false, "Clear");
        }

        private bool ShouldEmit(Candidate candidate, bool radarActive, SpotterSnapshot output)
        {
            if (candidate.Code == "NONE")
            {
                return false;
            }

            if (candidate.Code == "CLEAR")
            {
                return previousRadarActive && !radarActive;
            }

            if (candidate.Code == "BLUE_FLAG")
            {
                return output.BlueFlag && !previousBlueFlag;
            }

            if (candidate.Code == "YELLOW_FLAG")
            {
                return output.YellowFlag && !previousYellowFlag;
            }

            if (candidate.Code == "MEATBALL")
            {
                return output.MeatballFlag && !previousMeatballFlag;
            }

            if (candidate.Code == "FASTER_CLASS")
            {
                return output.FasterClassApproaching && !previousFasterClassApproaching;
            }

            if (!string.Equals(candidate.Code, lastEventCode, StringComparison.Ordinal))
            {
                return true;
            }

            return (DateTime.UtcNow - lastEventUtc).TotalSeconds >= RepeatCooldownSeconds;
        }

        private static void ApplyCurrentState(Candidate candidate, SpotterSnapshot output)
        {
            output.State = candidate.State;
            output.Callout = candidate.Text;
            output.CalloutCode = candidate.Code;
            output.Priority = candidate.Priority;
            output.IsUrgent = candidate.IsUrgent;
        }

        private void ResetHistory()
        {
            previousRadarActive = false;
            previousBlueFlag = false;
            previousYellowFlag = false;
            previousMeatballFlag = false;
            previousFasterClassApproaching = false;
            lastEventCode = string.Empty;
            lastEventUtc = DateTime.MinValue;
            eventSequence = 0;
        }

        private sealed class Candidate
        {
            public Candidate(string code, string text, int priority, bool isUrgent, string state)
            {
                Code = code;
                Text = text;
                Priority = priority;
                IsUrgent = isUrgent;
                State = state;
            }

            public string Code { get; private set; }
            public string Text { get; private set; }
            public int Priority { get; private set; }
            public bool IsUrgent { get; private set; }
            public string State { get; private set; }
        }
    }
}
