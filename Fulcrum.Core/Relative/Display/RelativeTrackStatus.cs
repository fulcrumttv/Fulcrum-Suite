namespace Fulcrum.Core.Relative.Display
{
    /// <summary>
    /// Translates the raw iRacing CarIdxTrackSurface value into stable
    /// display fields that dashboards can consume without custom formulas.
    /// </summary>
    public static class RelativeTrackStatus
    {
        public const int NotInWorld = -1;
        public const int OffTrack = 0;
        public const int InPitStall = 1;
        public const int ApproachingPits = 2;
        public const int OnTrack = 3;

        public static string GetName(int trackSurface)
        {
            switch (trackSurface)
            {
                case NotInWorld:
                    return "NotInWorld";
                case OffTrack:
                    return "OffTrack";
                case InPitStall:
                    return "InPitStall";
                case ApproachingPits:
                    return "ApproachingPits";
                case OnTrack:
                    return "OnTrack";
                default:
                    return "Unknown";
            }
        }

        public static bool IsInPits(int trackSurface)
        {
            return trackSurface == InPitStall ||
                   trackSurface == ApproachingPits;
        }
    }
}
