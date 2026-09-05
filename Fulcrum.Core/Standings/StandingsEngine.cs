using Fulcrum.Core.Relative;
using Fulcrum.Core.Relative.Display;
using Fulcrum.Core.Session;

namespace Fulcrum.Core.Standings
{
    public sealed class StandingsEngine
    {
        private readonly int[] sortedCarIndexes;

        public StandingsEngine()
        {
            sortedCarIndexes = new int[ParticipantBuffer.DefaultCapacity];
        }

        public void Update(
            ParticipantBuffer participantBuffer,
            SessionDatabase sessionDatabase,
            int playerCarIndex,
            StandingsSnapshot snapshot)
        {
            if (snapshot == null) return;
            snapshot.Reset();

            if (participantBuffer == null)
            {
                snapshot.Error = "Participant buffer unavailable";
                return;
            }

            int participantCount = BuildSortedIndex(participantBuffer);
            snapshot.ParticipantCount = participantCount;
            if (participantCount == 0)
            {
                snapshot.Error = "No standings participants";
                return;
            }

            ParticipantSnapshot leader = participantBuffer[sortedCarIndexes[0]];
            DriverIdentity playerIdentity = null;
            if (sessionDatabase != null)
            {
                sessionDatabase.TryGet(playerCarIndex, out playerIdentity);
            }

            int publishCount = participantCount > StandingsSnapshot.PublishedRowCount
                ? StandingsSnapshot.PublishedRowCount
                : participantCount;

            for (int rowIndex = 0; rowIndex < publishCount; rowIndex++)
            {
                int carIndex = sortedCarIndexes[rowIndex];
                ParticipantSnapshot participant = participantBuffer[carIndex];
                DriverIdentity identity = null;
                if (sessionDatabase != null)
                {
                    sessionDatabase.TryGet(carIndex, out identity);
                }

                int lapDifference = participant.LapCompleted - leader.LapCompleted;
                bool hasGap = lapDifference == 0 &&
                              IsUsableEstimatedTime(leader.EstimatedTime) &&
                              IsUsableEstimatedTime(participant.EstimatedTime);
                float gap = hasGap ? participant.EstimatedTime - leader.EstimatedTime : 0.0f;
                if (gap < 0.0f)
                {
                    hasGap = false;
                    gap = 0.0f;
                }
                bool isPlayer = carIndex == playerCarIndex || participant.IsPlayer;
                bool isSameClass = IsSameClass(playerIdentity, identity);
                int trackSurface = participant.TrackSurface;

                StandingsEntry row = snapshot.GetRow(rowIndex);
                row.SetTelemetry(
                    carIndex,
                    isPlayer,
                    isSameClass,
                    participant.OverallPosition,
                    participant.ClassPosition,
                    participant.Lap,
                    participant.LapCompleted,
                    lapDifference,
                    gap,
                    hasGap,
                    participant.LastLapTime,
                    participant.BestLapTime,
                    trackSurface,
                    RelativeTrackStatus.GetName(trackSurface),
                    RelativeTrackStatus.IsInPits(trackSurface));

                if (identity != null)
                {
                    row.SetIdentity(
                        identity.DriverName,
                        identity.CarNumber,
                        identity.TeamName,
                        identity.ClassName,
                        identity.Manufacturer,
                        identity.IRating,
                        identity.License);
                }

                if (isPlayer) snapshot.PlayerRow = rowIndex + 1;
            }

            snapshot.PublishedCount = publishCount;
            snapshot.LeaderCarIndex = leader.CarIndex;
            DriverIdentity leaderIdentity;
            if (sessionDatabase != null &&
                sessionDatabase.TryGet(leader.CarIndex, out leaderIdentity))
            {
                snapshot.LeaderName = leaderIdentity.DriverName;
            }
            snapshot.Ready = true;
        }

        private int BuildSortedIndex(ParticipantBuffer participantBuffer)
        {
            int count = 0;
            for (int carIndex = 0; carIndex < participantBuffer.Capacity; carIndex++)
            {
                ParticipantSnapshot participant = participantBuffer[carIndex];
                if (!IsEligible(participant)) continue;

                int insertAt = count;
                while (insertAt > 0 &&
                       Compare(participant, participantBuffer[sortedCarIndexes[insertAt - 1]]) < 0)
                {
                    sortedCarIndexes[insertAt] = sortedCarIndexes[insertAt - 1];
                    insertAt--;
                }
                sortedCarIndexes[insertAt] = carIndex;
                count++;
            }
            return count;
        }

        private static bool IsEligible(ParticipantSnapshot participant)
        {
            return participant != null &&
                   participant.IsValid &&
                   (participant.OverallPosition > 0 ||
                    participant.ClassPosition > 0 ||
                    participant.IsPlayer);
        }

        private static int Compare(ParticipantSnapshot first, ParticipantSnapshot second)
        {
            int firstPosition = first.OverallPosition > 0 ? first.OverallPosition : int.MaxValue;
            int secondPosition = second.OverallPosition > 0 ? second.OverallPosition : int.MaxValue;
            if (firstPosition != secondPosition)
            {
                return firstPosition < secondPosition ? -1 : 1;
            }

            double firstProgress = first.LapCompleted + first.LapDistancePercent;
            double secondProgress = second.LapCompleted + second.LapDistancePercent;
            if (firstProgress == secondProgress)
            {
                return first.CarIndex.CompareTo(second.CarIndex);
            }
            return firstProgress > secondProgress ? -1 : 1;
        }

        private static bool IsSameClass(DriverIdentity player, DriverIdentity participant)
        {
            if (player == null || participant == null ||
                string.IsNullOrEmpty(player.ClassName) ||
                string.IsNullOrEmpty(participant.ClassName))
            {
                return false;
            }
            return string.Equals(
                player.ClassName,
                participant.ClassName,
                System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUsableEstimatedTime(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0.0f;
        }
    }
}
