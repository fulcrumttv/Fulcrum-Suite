using System;

namespace Fulcrum.Core.Telemetry
{
    public static class SessionStateInterpreter
    {
        private const long Checkered = 0x00000001L;
        private const long White = 0x00000002L;
        private const long Green = 0x00000004L;
        private const long Yellow = 0x00000008L;
        private const long Red = 0x00000010L;
        private const long Blue = 0x00000020L;
        private const long YellowWaving = 0x00000100L;
        private const long Debris = 0x00000040L;
        private const long OneLapToGreen = 0x00000200L;
        private const long Caution = 0x00004000L;
        private const long CautionWaving = 0x00008000L;
        private const long Black = 0x00010000L;
        private const long Disqualify = 0x00020000L;
        private const long Furled = 0x00080000L;
        private const long Repair = 0x00100000L;
        private const long StartReady = 0x20000000L;
        private const long StartSet = 0x40000000L;
        private const long StartGo = 0x80000000L;

        public static string GetSessionStateName(int value)
        {
            switch (value)
            {
                case 1: return "GetInCar";
                case 2: return "Warmup";
                case 3: return "ParadeLaps";
                case 4: return "Racing";
                case 5: return "Checkered";
                case 6: return "CoolDown";
                default: return "Invalid";
            }
        }

        public static string GetPrimaryFlag(long flags)
        {
            if (Has(flags, Disqualify)) return "Disqualified";
            if (Has(flags, Repair)) return "Repair";
            if (Has(flags, Black)) return "Black";
            if (Has(flags, Red)) return "Red";
            if (Has(flags, Checkered)) return "Checkered";
            if (Has(flags, CautionWaving) || Has(flags, YellowWaving)) return "YellowWaving";
            if (Has(flags, Caution) || Has(flags, Yellow)) return "Yellow";
            if (Has(flags, White)) return "White";
            if (Has(flags, Blue)) return "Blue";
            if (Has(flags, Green) || Has(flags, StartGo)) return "Green";
            if (Has(flags, StartSet)) return "StartSet";
            if (Has(flags, StartReady)) return "StartReady";
            return "None";
        }

        public static bool HasGreen(long flags) { return Has(flags, Green) || Has(flags, StartGo); }
        public static bool HasYellowLocal(long flags) { return Has(flags, Yellow); }
        public static bool HasYellowWaving(long flags) { return Has(flags, YellowWaving); }
        public static bool HasCaution(long flags) { return Has(flags, Caution); }
        public static bool HasCautionWaving(long flags) { return Has(flags, CautionWaving); }
        public static bool HasDebris(long flags) { return Has(flags, Debris); }
        public static bool HasYellow(long flags) { return Has(flags, Yellow) || Has(flags, YellowWaving) || Has(flags, Caution) || Has(flags, CautionWaving); }
        public static bool HasRed(long flags) { return Has(flags, Red); }
        public static bool HasBlue(long flags) { return Has(flags, Blue); }
        public static bool HasWhite(long flags) { return Has(flags, White); }
        public static bool HasCheckered(long flags) { return Has(flags, Checkered); }
        public static bool HasBlack(long flags) { return Has(flags, Black); }
        public static bool HasRepair(long flags) { return Has(flags, Repair); }
        public static bool HasFurledBlack(long flags) { return Has(flags, Furled); }
        public static bool HasStartReady(long flags) { return Has(flags, StartReady); }
        public static bool HasStartSet(long flags) { return Has(flags, StartSet); }
        public static bool HasStartGo(long flags) { return Has(flags, StartGo); }
        public static bool HasDisqualify(long flags) { return Has(flags, Disqualify); }
        public static bool HasOneLapToGreen(long flags) { return Has(flags, OneLapToGreen); }
        public static bool IsRacing(int sessionState) { return sessionState == 4; }
        public static bool IsFinished(int sessionState, long flags) { return sessionState >= 5 || HasCheckered(flags); }

        private static bool Has(long flags, long mask)
        {
            return (flags & mask) != 0;
        }
    }
}
