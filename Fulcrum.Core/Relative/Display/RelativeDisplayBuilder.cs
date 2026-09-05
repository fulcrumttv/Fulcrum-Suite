using Fulcrum.Core.Session;

namespace Fulcrum.Core.Relative.Display
{
    /// <summary>
    /// Builds the visual representation of the Relative from
    /// participant telemetry, calculated Relative data and
    /// static participant identity information.
    ///
    /// All destination objects are reused during normal updates.
    /// </summary>
    public sealed class RelativeDisplayBuilder
    {
        public void Build(
            ParticipantBuffer participantBuffer,
            RelativeSnapshot relativeSnapshot,
            SessionDatabase sessionDatabase,
            StintTracker stintTracker,
            RelativeDisplaySnapshot displaySnapshot)
        {
            if (participantBuffer == null ||
                relativeSnapshot == null ||
                displaySnapshot == null)
            {
                return;
            }

            displaySnapshot.Reset();

            BuildPlayer(
                participantBuffer,
                relativeSnapshot,
                sessionDatabase,
                stintTracker,
                displaySnapshot.Player);

            for (int index = 0;
                 index < RelativeDisplaySnapshot.SlotCount;
                 index++)
            {
                BuildEntry(
                    participantBuffer,
                    relativeSnapshot.GetAhead(index),
                    sessionDatabase,
                    stintTracker,
                    displaySnapshot.GetAhead(index));

                BuildEntry(
                    participantBuffer,
                    relativeSnapshot.GetBehind(index),
                    sessionDatabase,
                    stintTracker,
                    displaySnapshot.GetBehind(index));
            }

            displaySnapshot.RefreshCounts();
        }

        private static void BuildPlayer(
            ParticipantBuffer participantBuffer,
            RelativeSnapshot relativeSnapshot,
            SessionDatabase sessionDatabase,
            StintTracker stintTracker,
            RelativeDisplayEntry displayEntry)
        {
            ParticipantSnapshot participant;

            if (!participantBuffer.TryGetParticipant(
                    relativeSnapshot.PlayerCarIndex,
                    out participant))
            {
                displayEntry.Reset();

                return;
            }

            displayEntry.SetPlayer(
                participant,
                stintTracker);

            ApplyIdentity(
                sessionDatabase,
                participantBuffer,
                participant.CarIndex,
                displayEntry);
        }

        private static void BuildEntry(
            ParticipantBuffer participantBuffer,
            RelativeEntry relativeEntry,
            SessionDatabase sessionDatabase,
            StintTracker stintTracker,
            RelativeDisplayEntry displayEntry)
        {
            if (relativeEntry == null ||
                !relativeEntry.IsValid)
            {
                displayEntry.Reset();

                return;
            }

            ParticipantSnapshot participant;

            participantBuffer.TryGetParticipant(
                relativeEntry.CarIndex,
                out participant);

            displayEntry.SetRelative(
                relativeEntry,
                participant,
                stintTracker);

            ApplyIdentity(
                sessionDatabase,
                participantBuffer,
                relativeEntry.CarIndex,
                displayEntry);
        }

        private static void ApplyIdentity(
            SessionDatabase sessionDatabase,
            ParticipantBuffer participantBuffer,
            int carIndex,
            RelativeDisplayEntry displayEntry)
        {
            if (sessionDatabase == null)
            {
                return;
            }

            DriverIdentity identity;

            if (!sessionDatabase.TryGet(
                    carIndex,
                    out identity))
            {
                return;
            }

            displayEntry.SetIdentity(
                identity.DriverName,
                identity.CarNumber,
                identity.TeamName,
                identity.ClassName,
                identity.Manufacturer,
                identity.IRating,
                identity.License,
                identity.ClubName,
                identity.FlagText);

            displayEntry.SetResourceData(
                identity.ManufacturerAlias,
                identity.LogoResourceKey,
                identity.CountryAlias,
                identity.FlagResourceKey);

            float carClassEstimatedLapTime = 0.0f;
            ParticipantSnapshot participant;
            if (participantBuffer != null &&
                participantBuffer.TryGetParticipant(
                    carIndex,
                    out participant) &&
                participant != null)
            {
                carClassEstimatedLapTime =
                    participant.CarClassEstimatedLapTime;
            }

            displayEntry.SetDiagnosticData(
                identity.UserId,
                identity.CarId,
                carClassEstimatedLapTime,
                identity.CarPath,
                identity.CarScreenName,
                identity.CarName,
                identity.DriverInfoRaw);
        }
    }
}