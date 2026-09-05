namespace Fulcrum.Plugin.Modules
{
    /// <summary>
    /// Centralizes all SimHub property names used by the Relative module.
    /// </summary>
    internal static class RelativePropertyNames
    {
        private const string Root =
            "Fulcrum.Relative.";

        private const string PlayerRoot =
            Root + "Player.";

        public const string DirectLookup =
            Root + "DirectLookup";

        public const string ValidParticipantCount =
            Root + "ValidParticipantCount";

        // Legacy player properties kept for compatibility.
        public const string PlayerCarIndex =
            Root + "PlayerCarIndex";

        public const string PlayerLap =
            Root + "PlayerLap";

        public const string PlayerLapDistancePercent =
            Root + "PlayerLapDistancePercent";

        public const string PlayerContinuousTrackPosition =
            Root + "PlayerContinuousTrackPosition";

        // Canonical player-row properties.
        public const string PlayerRowCarIndex =
            PlayerRoot + "CarIndex";

        public const string PlayerRowOverallPosition =
            PlayerRoot + "OverallPosition";

        public const string PlayerRowClassPosition =
            PlayerRoot + "ClassPosition";

        public const string PlayerRowLap =
            PlayerRoot + "Lap";

        public const string PlayerRowLapDistancePercent =
            PlayerRoot + "LapDistancePercent";

        public const string PlayerRowGapSeconds =
            PlayerRoot + "GapSeconds";

        public const string PlayerRowHasData =
            PlayerRoot + "HasData";

        public const string AheadCount =
            Root + "AheadCount";

        public const string BehindCount =
            Root + "BehindCount";

        public const string DisplayAheadCount =
            Root + "Display.AheadCount";

        public const string DisplayBehindCount =
            Root + "Display.BehindCount";

        public const string ValidRawGapCount =
            Root + "ValidRawGapCount";

        public const string ValidFilteredGapCount =
            Root + "ValidFilteredGapCount";

        public const string GapCalculatorRunning =
            Root + "GapCalculatorRunning";

        public const string GapFilterRunning =
            Root + "GapFilterRunning";

        public const string ReaderExecutionCount =
            Root + "ReaderExecutionCount";

        public const string ReaderLastExecutionMs =
            Root + "ReaderLastExecutionMs";

        public const string ReaderPeakExecutionMs =
            Root + "ReaderPeakExecutionMs";

        public static string Player(
            string propertyName)
        {
            return
                PlayerRoot +
                propertyName;
        }

        public static string Ahead(
            int zeroBasedIndex,
            string propertyName)
        {
            return
                Root +
                "Ahead" +
                FormatSlotNumber(zeroBasedIndex) +
                "." +
                propertyName;
        }

        public static string Behind(
            int zeroBasedIndex,
            string propertyName)
        {
            return
                Root +
                "Behind" +
                FormatSlotNumber(zeroBasedIndex) +
                "." +
                propertyName;
        }

        private static string FormatSlotNumber(
            int zeroBasedIndex)
        {
            return
                (zeroBasedIndex + 1).ToString("00");
        }
    }
}