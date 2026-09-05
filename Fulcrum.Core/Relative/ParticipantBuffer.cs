using System;

namespace Fulcrum.Core.Relative
{
    /// <summary>
    /// Fixed, reusable storage for all participants in a session.
    ///
    /// The buffer creates its participant objects once and reuses
    /// them throughout the lifetime of the plugin.
    /// </summary>
    public sealed class ParticipantBuffer
    {
        /// <summary>
        /// Maximum number of iRacing car indexes currently supported.
        ///
        /// iRacing participant telemetry arrays commonly expose
        /// 64 car slots, indexed from 0 through 63.
        /// </summary>
        public const int DefaultCapacity = 64;

        private readonly ParticipantSnapshot[] participants;

        public ParticipantBuffer()
            : this(DefaultCapacity)
        {
        }

        public ParticipantBuffer(
            int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(capacity),
                    "Participant capacity must be greater than zero.");
            }

            participants =
                new ParticipantSnapshot[capacity];

            for (int index = 0;
                 index < participants.Length;
                 index++)
            {
                participants[index] =
                    new ParticipantSnapshot(index);
            }
        }

        /// <summary>
        /// Total number of reusable participant slots.
        /// </summary>
        public int Capacity
        {
            get
            {
                return participants.Length;
            }
        }

        /// <summary>
        /// Number of participant slots containing valid data
        /// during the latest telemetry update.
        /// </summary>
        public int ValidParticipantCount
        {
            get;
            private set;
        }

        /// <summary>
        /// Returns a reusable participant slot by car index.
        /// </summary>
        public ParticipantSnapshot this[int carIndex]
        {
            get
            {
                if (carIndex < 0 ||
                    carIndex >= participants.Length)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(carIndex));
                }

                return participants[carIndex];
            }
        }

        /// <summary>
        /// Attempts to retrieve a participant without throwing
        /// when the supplied car index is invalid.
        /// </summary>
        public bool TryGetParticipant(
            int carIndex,
            out ParticipantSnapshot participant)
        {
            if (carIndex < 0 ||
                carIndex >= participants.Length)
            {
                participant = null;

                return false;
            }

            participant =
                participants[carIndex];

            return true;
        }

        /// <summary>
        /// Clears all participant data while preserving the existing
        /// objects and backing array.
        /// </summary>
        public void Reset()
        {
            ValidParticipantCount = 0;

            for (int index = 0;
                 index < participants.Length;
                 index++)
            {
                participants[index].Reset();
            }
        }

        /// <summary>
        /// Recalculates how many participant slots contain usable data.
        ///
        /// This method performs no allocations.
        /// </summary>
        public void RefreshValidParticipantCount()
        {
            int validCount = 0;

            for (int index = 0;
                 index < participants.Length;
                 index++)
            {
                if (participants[index].IsValid)
                {
                    validCount++;
                }
            }

            ValidParticipantCount =
                validCount;
        }
    }
}