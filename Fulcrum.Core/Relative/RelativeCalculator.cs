using System;

namespace Fulcrum.Core.Relative
{
    /// <summary>
    /// Calculates the nearest participants physically ahead
    /// and behind the player around the circuit.
    ///
    /// This class performs no collection allocations during updates.
    /// </summary>
    public sealed class RelativeCalculator
    {
        private readonly RelativeLapTracker lapTracker = new RelativeLapTracker();

        public void Reset() { lapTracker.Reset(); }

        public void SetLapColorContext(bool enabled, double sessionTime)
        {
            lapTracker.SetContext(enabled, sessionTime);
        }

        public void Calculate(
            ParticipantBuffer participantBuffer,
            RelativeSnapshot relativeSnapshot)
        {
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

            relativeSnapshot.Reset();

            ParticipantSnapshot player =
                FindPlayerParticipant(
                    participantBuffer);

            if (!IsUsableParticipant(player))
            {
                lapTracker.Update(participantBuffer);
                return;
            }

            lapTracker.Update(participantBuffer);
            double playerContinuousPosition = lapTracker.ContinuousPosition(player);

            relativeSnapshot.SetPlayer(
                player.CarIndex,
                playerContinuousPosition);

            for (int carIndex = 0;
                 carIndex < participantBuffer.Capacity;
                 carIndex++)
            {
                ParticipantSnapshot participant =
                    participantBuffer[carIndex];

                if (!IsUsableParticipant(participant))
                {
                    continue;
                }

                if (participant.IsPlayer ||
                    participant.CarIndex == player.CarIndex)
                {
                    continue;
                }

                float relativeDistance =
                    CalculateCircularDistance(
                        player.LapDistancePercent,
                        participant.LapDistancePercent);

                /*
                 * An exact zero generally means both cars occupy the
                 * same reported telemetry point. We ignore it until
                 * better tie-breaking data is available.
                 */
                if (relativeDistance == 0.0f)
                {
                    continue;
                }

                int lapDifference = lapTracker.LapDifference(player, participant);
                double continuousPosition = lapTracker.ContinuousPosition(participant);

                if (relativeDistance > 0.0f)
                {
                    InsertAhead(
                        relativeSnapshot,
                        participant,
                        relativeDistance,
                        lapDifference,
                        continuousPosition);
                }
                else
                {
                    InsertBehind(
                        relativeSnapshot,
                        participant,
                        relativeDistance,
                        lapDifference,
                        continuousPosition);
                }
            }
        }

        private static ParticipantSnapshot
            FindPlayerParticipant(
                ParticipantBuffer participantBuffer)
        {
            for (int carIndex = 0;
                 carIndex < participantBuffer.Capacity;
                 carIndex++)
            {
                ParticipantSnapshot participant =
                    participantBuffer[carIndex];

                if (participant.IsPlayer)
                {
                    return participant;
                }
            }

            return null;
        }

        private static bool IsUsableParticipant(
            ParticipantSnapshot participant)
        {
            if (participant == null ||
                !participant.IsValid)
            {
                return false;
            }

            return participant.LapDistancePercent >= 0.0f &&
                   participant.LapDistancePercent <= 1.0f;
        }

        /// <summary>
        /// Converts the distance between two normalized track
        /// positions into the shortest signed circular distance.
        ///
        /// Result range:
        /// -0.5 through +0.5 laps.
        /// </summary>
        private static float CalculateCircularDistance(
            float playerLapDistance,
            float participantLapDistance)
        {
            float difference =
                participantLapDistance -
                playerLapDistance;

            if (difference > 0.5f)
            {
                difference -= 1.0f;
            }
            else if (difference < -0.5f)
            {
                difference += 1.0f;
            }

            return difference;
        }

        private static void InsertAhead(
            RelativeSnapshot snapshot,
            ParticipantSnapshot participant,
            float relativeDistance,
            int lapDifference,
            double continuousPosition)
        {
            int insertionIndex = -1;

            for (int index = 0;
                 index < snapshot.Capacity;
                 index++)
            {
                RelativeEntry current =
                    snapshot.GetAhead(index);

                if (!current.IsValid ||
                    relativeDistance <
                    current.RelativeDistanceLaps)
                {
                    insertionIndex = index;

                    break;
                }
            }

            if (insertionIndex < 0)
            {
                return;
            }

            ShiftAheadEntries(
                snapshot,
                insertionIndex);

            RelativeEntry destination =
                snapshot.GetAhead(
                    insertionIndex);

            destination.SetFromParticipant(
                participant,
                relativeDistance,
                lapDifference,
                continuousPosition);

            if (snapshot.AheadCount <
                snapshot.Capacity)
            {
                snapshot.AheadCount++;
            }
        }

        private static void InsertBehind(
            RelativeSnapshot snapshot,
            ParticipantSnapshot participant,
            float relativeDistance,
            int lapDifference,
            double continuousPosition)
        {
            float absoluteDistance =
                Math.Abs(relativeDistance);

            int insertionIndex = -1;

            for (int index = 0;
                 index < snapshot.Capacity;
                 index++)
            {
                RelativeEntry current =
                    snapshot.GetBehind(index);

                if (!current.IsValid ||
                    absoluteDistance <
                    Math.Abs(
                        current.RelativeDistanceLaps))
                {
                    insertionIndex = index;

                    break;
                }
            }

            if (insertionIndex < 0)
            {
                return;
            }

            ShiftBehindEntries(
                snapshot,
                insertionIndex);

            RelativeEntry destination =
                snapshot.GetBehind(
                    insertionIndex);

            destination.SetFromParticipant(
                participant,
                relativeDistance,
                lapDifference,
                continuousPosition);

            if (snapshot.BehindCount <
                snapshot.Capacity)
            {
                snapshot.BehindCount++;
            }
        }

        private static void ShiftAheadEntries(
            RelativeSnapshot snapshot,
            int insertionIndex)
        {
            for (int index = snapshot.Capacity - 1;
                 index > insertionIndex;
                 index--)
            {
                snapshot
                    .GetAhead(index)
                    .CopyFrom(
                        snapshot.GetAhead(index - 1));
            }
        }

        private static void ShiftBehindEntries(
            RelativeSnapshot snapshot,
            int insertionIndex)
        {
            for (int index = snapshot.Capacity - 1;
                 index > insertionIndex;
                 index--)
            {
                snapshot
                    .GetBehind(index)
                    .CopyFrom(
                        snapshot.GetBehind(index - 1));
            }
        }
    }
}
