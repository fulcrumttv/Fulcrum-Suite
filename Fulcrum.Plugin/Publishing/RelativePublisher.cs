using System;
using Fulcrum.Core.Performance;
using Fulcrum.Core.Relative;
using SimHub.Plugins;
using Fulcrum.Plugin.Modules;

namespace Fulcrum.Plugin.Publishing
{
    /// <summary>
    /// Registers and publishes all SimHub properties produced
    /// by the Relative module.
    ///
    /// This class contains no Relative calculation logic.
    /// Its only responsibility is exposing calculated data to SimHub.
    /// </summary>
    internal sealed class RelativePublisher
    {
        private const int PublishedSlotCount = 4;

        private readonly PluginManager pluginManager;
        private readonly Type pluginType;

        private readonly string[] aheadCarIndexProperties;
        private readonly string[] aheadDistanceProperties;
        private readonly string[] aheadLapDifferenceProperties;
        private readonly string[] aheadPositionProperties;
        private readonly string[] aheadRawGapProperties;
        private readonly string[] aheadHasRawGapProperties;
        private readonly string[] aheadFilteredGapProperties;
        private readonly string[] aheadHasFilteredGapProperties;
        private readonly string[] aheadGapProperties;
        private readonly string[] aheadHasGapProperties;

        private readonly string[] behindCarIndexProperties;
        private readonly string[] behindDistanceProperties;
        private readonly string[] behindLapDifferenceProperties;
        private readonly string[] behindPositionProperties;
        private readonly string[] behindRawGapProperties;
        private readonly string[] behindHasRawGapProperties;
        private readonly string[] behindFilteredGapProperties;
        private readonly string[] behindHasFilteredGapProperties;
        private readonly string[] behindGapProperties;
        private readonly string[] behindHasGapProperties;

        public RelativePublisher(
            PluginManager pluginManager,
            Type pluginType)
        {
            if (pluginManager == null)
            {
                throw new ArgumentNullException(
                    nameof(pluginManager));
            }

            if (pluginType == null)
            {
                throw new ArgumentNullException(
                    nameof(pluginType));
            }

            this.pluginManager = pluginManager;
            this.pluginType = pluginType;

            aheadCarIndexProperties =
                new string[PublishedSlotCount];

            aheadDistanceProperties =
                new string[PublishedSlotCount];

            aheadLapDifferenceProperties =
                new string[PublishedSlotCount];

            aheadPositionProperties =
                new string[PublishedSlotCount];

            aheadRawGapProperties =
                new string[PublishedSlotCount];

            aheadHasRawGapProperties =
                new string[PublishedSlotCount];

            aheadFilteredGapProperties =
                new string[PublishedSlotCount];

            aheadHasFilteredGapProperties =
                new string[PublishedSlotCount];

            aheadGapProperties =
                new string[PublishedSlotCount];

            aheadHasGapProperties =
                new string[PublishedSlotCount];

            behindCarIndexProperties =
                new string[PublishedSlotCount];

            behindDistanceProperties =
                new string[PublishedSlotCount];

            behindLapDifferenceProperties =
                new string[PublishedSlotCount];

            behindPositionProperties =
                new string[PublishedSlotCount];

            behindRawGapProperties =
                new string[PublishedSlotCount];

            behindHasRawGapProperties =
                new string[PublishedSlotCount];

            behindFilteredGapProperties =
                new string[PublishedSlotCount];

            behindHasFilteredGapProperties =
                new string[PublishedSlotCount];

            behindGapProperties =
                new string[PublishedSlotCount];

            behindHasGapProperties =
                new string[PublishedSlotCount];

            BuildPropertyNames();
            RegisterProperties();
        }

        public void Publish(
            ParticipantTelemetryReader participantReader,
            ParticipantBuffer participantBuffer,
            RelativeSnapshot relativeSnapshot,
            ScheduledTask updateTask)
        {
            if (participantReader == null)
            {
                throw new ArgumentNullException(
                    nameof(participantReader));
            }

            if (participantBuffer == null)
            {
                throw new ArgumentNullException(
                    nameof(participantBuffer));
            }

            if (relativeSnapshot == null)
            {
                throw new ArgumentNullException(
                    nameof(relativeSnapshot));
            }

            if (updateTask == null)
            {
                throw new ArgumentNullException(
                    nameof(updateTask));
            }

            pluginManager.SetPropertyValue(
                RelativePropertyNames.DirectLookup,
                pluginType,
                participantReader.IsUsingDirectLookup);

            pluginManager.SetPropertyValue(
                RelativePropertyNames.ValidParticipantCount,
                pluginType,
                participantBuffer.ValidParticipantCount);

            pluginManager.SetPropertyValue(
                RelativePropertyNames.PlayerCarIndex,
                pluginType,
                relativeSnapshot.PlayerCarIndex);

            PublishPlayerProperties(
                participantBuffer,
                relativeSnapshot);

            pluginManager.SetPropertyValue(
                RelativePropertyNames.PlayerContinuousTrackPosition,
                pluginType,
                relativeSnapshot.PlayerContinuousTrackPosition);

            pluginManager.SetPropertyValue(
                RelativePropertyNames.AheadCount,
                pluginType,
                relativeSnapshot.AheadCount);

            pluginManager.SetPropertyValue(
                RelativePropertyNames.BehindCount,
                pluginType,
                relativeSnapshot.BehindCount);

            int validRawGapCount = 0;
            int validFilteredGapCount = 0;

            for (int index = 0;
                 index < PublishedSlotCount;
                 index++)
            {
                RelativeEntry aheadEntry =
                    relativeSnapshot.GetAhead(index);

                RelativeEntry behindEntry =
                    relativeSnapshot.GetBehind(index);

                CountValidGaps(
                    aheadEntry,
                    ref validRawGapCount,
                    ref validFilteredGapCount);

                CountValidGaps(
                    behindEntry,
                    ref validRawGapCount,
                    ref validFilteredGapCount);

                PublishEntry(
                    aheadEntry,
                    aheadCarIndexProperties[index],
                    aheadDistanceProperties[index],
                    aheadLapDifferenceProperties[index],
                    aheadPositionProperties[index],
                    aheadRawGapProperties[index],
                    aheadHasRawGapProperties[index],
                    aheadFilteredGapProperties[index],
                    aheadHasFilteredGapProperties[index],
                    aheadGapProperties[index],
                    aheadHasGapProperties[index]);

                PublishEntry(
                    behindEntry,
                    behindCarIndexProperties[index],
                    behindDistanceProperties[index],
                    behindLapDifferenceProperties[index],
                    behindPositionProperties[index],
                    behindRawGapProperties[index],
                    behindHasRawGapProperties[index],
                    behindFilteredGapProperties[index],
                    behindHasFilteredGapProperties[index],
                    behindGapProperties[index],
                    behindHasGapProperties[index]);
            }

            pluginManager.SetPropertyValue(
                RelativePropertyNames.ValidRawGapCount,
                pluginType,
                validRawGapCount);

            pluginManager.SetPropertyValue(
                RelativePropertyNames.ValidFilteredGapCount,
                pluginType,
                validFilteredGapCount);

            pluginManager.SetPropertyValue(
                RelativePropertyNames.GapCalculatorRunning,
                pluginType,
                validRawGapCount > 0);

            pluginManager.SetPropertyValue(
                RelativePropertyNames.GapFilterRunning,
                pluginType,
                validFilteredGapCount > 0);

            pluginManager.SetPropertyValue(
                RelativePropertyNames.ReaderExecutionCount,
                pluginType,
                updateTask.ExecutionCount);

            pluginManager.SetPropertyValue(
                RelativePropertyNames.ReaderLastExecutionMs,
                pluginType,
                updateTask.LastExecutionMilliseconds);

            pluginManager.SetPropertyValue(
                RelativePropertyNames.ReaderPeakExecutionMs,
                pluginType,
                updateTask.PeakExecutionMilliseconds);
        }

        private void BuildPropertyNames()
        {
            for (int index = 0;
                 index < PublishedSlotCount;
                 index++)
            {
                aheadCarIndexProperties[index] =
                    RelativePropertyNames.Ahead(
                        index,
                        "CarIndex");

                aheadDistanceProperties[index] =
                    RelativePropertyNames.Ahead(
                        index,
                        "DistanceLaps");

                aheadLapDifferenceProperties[index] =
                    RelativePropertyNames.Ahead(
                        index,
                        "LapDifference");

                aheadPositionProperties[index] =
                    RelativePropertyNames.Ahead(
                        index,
                        "OverallPosition");

                aheadRawGapProperties[index] =
                    RelativePropertyNames.Ahead(
                        index,
                        "RawGapSeconds");

                aheadHasRawGapProperties[index] =
                    RelativePropertyNames.Ahead(
                        index,
                        "HasRawGap");

                aheadFilteredGapProperties[index] =
                    RelativePropertyNames.Ahead(
                        index,
                        "FilteredGapSeconds");

                aheadHasFilteredGapProperties[index] =
                    RelativePropertyNames.Ahead(
                        index,
                        "HasFilteredGap");

                aheadGapProperties[index] =
                    RelativePropertyNames.Ahead(
                        index,
                        "GapSeconds");

                aheadHasGapProperties[index] =
                    RelativePropertyNames.Ahead(
                        index,
                        "HasGap");

                behindCarIndexProperties[index] =
                    RelativePropertyNames.Behind(
                        index,
                        "CarIndex");

                behindDistanceProperties[index] =
                    RelativePropertyNames.Behind(
                        index,
                        "DistanceLaps");

                behindLapDifferenceProperties[index] =
                    RelativePropertyNames.Behind(
                        index,
                        "LapDifference");

                behindPositionProperties[index] =
                    RelativePropertyNames.Behind(
                        index,
                        "OverallPosition");

                behindRawGapProperties[index] =
                    RelativePropertyNames.Behind(
                        index,
                        "RawGapSeconds");

                behindHasRawGapProperties[index] =
                    RelativePropertyNames.Behind(
                        index,
                        "HasRawGap");

                behindFilteredGapProperties[index] =
                    RelativePropertyNames.Behind(
                        index,
                        "FilteredGapSeconds");

                behindHasFilteredGapProperties[index] =
                    RelativePropertyNames.Behind(
                        index,
                        "HasFilteredGap");

                behindGapProperties[index] =
                    RelativePropertyNames.Behind(
                        index,
                        "GapSeconds");

                behindHasGapProperties[index] =
                    RelativePropertyNames.Behind(
                        index,
                        "HasGap");
            }
        }

        private void RegisterProperties()
        {
            pluginManager.AddProperty(
                RelativePropertyNames.DirectLookup,
                pluginType,
                false,
                "True when participant telemetry uses direct lookup");

            pluginManager.AddProperty(
                RelativePropertyNames.ValidParticipantCount,
                pluginType,
                0,
                "Number of valid participants detected");

            pluginManager.AddProperty(
                RelativePropertyNames.PlayerCarIndex,
                pluginType,
                -1,
                "Player car index");

            pluginManager.AddProperty(
                RelativePropertyNames.PlayerLap,
                pluginType,
                0,
                "Player lap read from participant telemetry");

            pluginManager.AddProperty(
                RelativePropertyNames.PlayerLapDistancePercent,
                pluginType,
                0.0,
                "Player normalized lap distance");

            pluginManager.AddProperty(
                RelativePropertyNames.PlayerContinuousTrackPosition,
                pluginType,
                0.0,
                "Player completed laps plus normalized lap distance");

            RegisterPlayerRowProperties();

            pluginManager.AddProperty(
                RelativePropertyNames.AheadCount,
                pluginType,
                0,
                "Number of available cars ahead");

            pluginManager.AddProperty(
                RelativePropertyNames.BehindCount,
                pluginType,
                0,
                "Number of available cars behind");

            pluginManager.AddProperty(
                RelativePropertyNames.ValidRawGapCount,
                pluginType,
                0,
                "Number of Relative entries with a valid raw gap");

            pluginManager.AddProperty(
                RelativePropertyNames.ValidFilteredGapCount,
                pluginType,
                0,
                "Number of Relative entries with a valid filtered gap");

            pluginManager.AddProperty(
                RelativePropertyNames.GapCalculatorRunning,
                pluginType,
                false,
                "True when the raw gap calculator is producing values");

            pluginManager.AddProperty(
                RelativePropertyNames.GapFilterRunning,
                pluginType,
                false,
                "True when the gap filter is producing final values");

            for (int index = 0;
                 index < PublishedSlotCount;
                 index++)
            {
                RegisterEntryProperties(
                    aheadCarIndexProperties[index],
                    aheadDistanceProperties[index],
                    aheadLapDifferenceProperties[index],
                    aheadPositionProperties[index],
                    aheadRawGapProperties[index],
                    aheadHasRawGapProperties[index],
                    aheadFilteredGapProperties[index],
                    aheadHasFilteredGapProperties[index],
                    aheadGapProperties[index],
                    aheadHasGapProperties[index],
                    "ahead");

                RegisterEntryProperties(
                    behindCarIndexProperties[index],
                    behindDistanceProperties[index],
                    behindLapDifferenceProperties[index],
                    behindPositionProperties[index],
                    behindRawGapProperties[index],
                    behindHasRawGapProperties[index],
                    behindFilteredGapProperties[index],
                    behindHasFilteredGapProperties[index],
                    behindGapProperties[index],
                    behindHasGapProperties[index],
                    "behind");
            }

            pluginManager.AddProperty(
                RelativePropertyNames.ReaderExecutionCount,
                pluginType,
                0L,
                "Number of Relative module executions");

            pluginManager.AddProperty(
                RelativePropertyNames.ReaderLastExecutionMs,
                pluginType,
                0.0,
                "Latest Relative module execution time");

            pluginManager.AddProperty(
                RelativePropertyNames.ReaderPeakExecutionMs,
                pluginType,
                0.0,
                "Peak Relative module execution time");
        }

        private void RegisterPlayerRowProperties()
        {
            pluginManager.AddProperty(
                RelativePropertyNames.PlayerRowCarIndex,
                pluginType,
                -1,
                "Player car index for the Relative player row");

            pluginManager.AddProperty(
                RelativePropertyNames.PlayerRowOverallPosition,
                pluginType,
                0,
                "Player overall race position");

            pluginManager.AddProperty(
                RelativePropertyNames.PlayerRowClassPosition,
                pluginType,
                0,
                "Player class position");

            pluginManager.AddProperty(
                RelativePropertyNames.PlayerRowLap,
                pluginType,
                0,
                "Player current lap");

            pluginManager.AddProperty(
                RelativePropertyNames.PlayerRowLapDistancePercent,
                pluginType,
                0.0,
                "Player normalized lap distance");

            pluginManager.AddProperty(
                RelativePropertyNames.PlayerRowGapSeconds,
                pluginType,
                0.0,
                "Player gap, always zero");

            pluginManager.AddProperty(
                RelativePropertyNames.PlayerRowHasData,
                pluginType,
                false,
                "True when the player Relative row contains valid data");
        }

        private void RegisterEntryProperties(
            string carIndexProperty,
            string distanceProperty,
            string lapDifferenceProperty,
            string positionProperty,
            string rawGapProperty,
            string hasRawGapProperty,
            string filteredGapProperty,
            string hasFilteredGapProperty,
            string gapProperty,
            string hasGapProperty,
            string direction)
        {
            pluginManager.AddProperty(
                carIndexProperty,
                pluginType,
                -1,
                "Car index " + direction + " of the player");

            pluginManager.AddProperty(
                distanceProperty,
                pluginType,
                0.0,
                "Circular lap distance " + direction);

            pluginManager.AddProperty(
                lapDifferenceProperty,
                pluginType,
                0,
                "Completed-lap difference " + direction);

            pluginManager.AddProperty(
                positionProperty,
                pluginType,
                0,
                "Overall position of car " + direction);

            pluginManager.AddProperty(
                rawGapProperty,
                pluginType,
                0.0,
                "Unfiltered estimated time gap in seconds");

            pluginManager.AddProperty(
                hasRawGapProperty,
                pluginType,
                false,
                "True when the raw gap is valid");

            pluginManager.AddProperty(
                filteredGapProperty,
                pluginType,
                0.0,
                "Validated and smoothed time gap in seconds");

            pluginManager.AddProperty(
                hasFilteredGapProperty,
                pluginType,
                false,
                "True when the filtered gap is valid");

            pluginManager.AddProperty(
                gapProperty,
                pluginType,
                0.0,
                "Final Relative gap for overlays");

            pluginManager.AddProperty(
                hasGapProperty,
                pluginType,
                false,
                "True when the final Relative gap is valid");
        }

        private void PublishPlayerProperties(
            ParticipantBuffer participantBuffer,
            RelativeSnapshot relativeSnapshot)
        {
            ParticipantSnapshot playerParticipant;

            if (participantBuffer.TryGetParticipant(
                    relativeSnapshot.PlayerCarIndex,
                    out playerParticipant) &&
                playerParticipant != null &&
                playerParticipant.IsValid)
            {
                pluginManager.SetPropertyValue(
                    RelativePropertyNames.PlayerLap,
                    pluginType,
                    playerParticipant.Lap);

                pluginManager.SetPropertyValue(
                    RelativePropertyNames.PlayerLapDistancePercent,
                    pluginType,
                    (double)playerParticipant.LapDistancePercent);

                pluginManager.SetPropertyValue(
                    RelativePropertyNames.PlayerRowCarIndex,
                    pluginType,
                    playerParticipant.CarIndex);

                pluginManager.SetPropertyValue(
                    RelativePropertyNames.PlayerRowOverallPosition,
                    pluginType,
                    playerParticipant.OverallPosition);

                pluginManager.SetPropertyValue(
                    RelativePropertyNames.PlayerRowClassPosition,
                    pluginType,
                    playerParticipant.ClassPosition);

                pluginManager.SetPropertyValue(
                    RelativePropertyNames.PlayerRowLap,
                    pluginType,
                    playerParticipant.Lap);

                pluginManager.SetPropertyValue(
                    RelativePropertyNames.PlayerRowLapDistancePercent,
                    pluginType,
                    (double)playerParticipant.LapDistancePercent);

                pluginManager.SetPropertyValue(
                    RelativePropertyNames.PlayerRowGapSeconds,
                    pluginType,
                    0.0);

                pluginManager.SetPropertyValue(
                    RelativePropertyNames.PlayerRowHasData,
                    pluginType,
                    true);

                return;
            }

            pluginManager.SetPropertyValue(
                RelativePropertyNames.PlayerLap,
                pluginType,
                0);

            pluginManager.SetPropertyValue(
                RelativePropertyNames.PlayerLapDistancePercent,
                pluginType,
                0.0);

            pluginManager.SetPropertyValue(
                RelativePropertyNames.PlayerRowCarIndex,
                pluginType,
                -1);

            pluginManager.SetPropertyValue(
                RelativePropertyNames.PlayerRowOverallPosition,
                pluginType,
                0);

            pluginManager.SetPropertyValue(
                RelativePropertyNames.PlayerRowClassPosition,
                pluginType,
                0);

            pluginManager.SetPropertyValue(
                RelativePropertyNames.PlayerRowLap,
                pluginType,
                0);

            pluginManager.SetPropertyValue(
                RelativePropertyNames.PlayerRowLapDistancePercent,
                pluginType,
                0.0);

            pluginManager.SetPropertyValue(
                RelativePropertyNames.PlayerRowGapSeconds,
                pluginType,
                0.0);

            pluginManager.SetPropertyValue(
                RelativePropertyNames.PlayerRowHasData,
                pluginType,
                false);
        }

        private void PublishEntry(
            RelativeEntry entry,
            string carIndexProperty,
            string distanceProperty,
            string lapDifferenceProperty,
            string positionProperty,
            string rawGapProperty,
            string hasRawGapProperty,
            string filteredGapProperty,
            string hasFilteredGapProperty,
            string gapProperty,
            string hasGapProperty)
        {
            if (entry != null &&
                entry.IsValid)
            {
                pluginManager.SetPropertyValue(
                    carIndexProperty,
                    pluginType,
                    entry.CarIndex);

                pluginManager.SetPropertyValue(
                    distanceProperty,
                    pluginType,
                    (double)entry.RelativeDistanceLaps);

                pluginManager.SetPropertyValue(
                    lapDifferenceProperty,
                    pluginType,
                    entry.LapDifference);

                pluginManager.SetPropertyValue(
                    positionProperty,
                    pluginType,
                    entry.OverallPosition);

                pluginManager.SetPropertyValue(
                    rawGapProperty,
                    pluginType,
                    entry.HasValidRawGap
                        ? (double)entry.RawGapSeconds
                        : 0.0);

                pluginManager.SetPropertyValue(
                    hasRawGapProperty,
                    pluginType,
                    entry.HasValidRawGap);

                pluginManager.SetPropertyValue(
                    filteredGapProperty,
                    pluginType,
                    entry.HasValidFilteredGap
                        ? (double)entry.FilteredGapSeconds
                        : 0.0);

                pluginManager.SetPropertyValue(
                    hasFilteredGapProperty,
                    pluginType,
                    entry.HasValidFilteredGap);

                pluginManager.SetPropertyValue(
                    gapProperty,
                    pluginType,
                    entry.HasValidFilteredGap
                        ? (double)entry.FilteredGapSeconds
                        : 0.0);

                pluginManager.SetPropertyValue(
                    hasGapProperty,
                    pluginType,
                    entry.HasValidFilteredGap);

                return;
            }

            pluginManager.SetPropertyValue(
                carIndexProperty,
                pluginType,
                -1);

            pluginManager.SetPropertyValue(
                distanceProperty,
                pluginType,
                0.0);

            pluginManager.SetPropertyValue(
                lapDifferenceProperty,
                pluginType,
                0);

            pluginManager.SetPropertyValue(
                positionProperty,
                pluginType,
                0);

            pluginManager.SetPropertyValue(
                rawGapProperty,
                pluginType,
                0.0);

            pluginManager.SetPropertyValue(
                hasRawGapProperty,
                pluginType,
                false);

            pluginManager.SetPropertyValue(
                filteredGapProperty,
                pluginType,
                0.0);

            pluginManager.SetPropertyValue(
                hasFilteredGapProperty,
                pluginType,
                false);

            pluginManager.SetPropertyValue(
                gapProperty,
                pluginType,
                0.0);

            pluginManager.SetPropertyValue(
                hasGapProperty,
                pluginType,
                false);
        }

        private static void CountValidGaps(
            RelativeEntry entry,
            ref int validRawGapCount,
            ref int validFilteredGapCount)
        {
            if (entry == null ||
                !entry.IsValid)
            {
                return;
            }

            if (entry.HasValidRawGap)
            {
                validRawGapCount++;
            }

            if (entry.HasValidFilteredGap)
            {
                validFilteredGapCount++;
            }
        }
    }
}