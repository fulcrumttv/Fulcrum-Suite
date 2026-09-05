using System;

namespace Fulcrum.Core.Relative.Display
{
    /// <summary>
    /// Reusable display representation of the Relative.
    ///
    /// The snapshot retains up to eight cars ahead and eight behind internally.
    /// The table publisher selects an adaptive nine-row window around the player.
    /// </summary>
    public sealed class RelativeDisplaySnapshot
    {
        public const int SlotCount = 8;

        private readonly RelativeDisplayEntry[] ahead;
        private readonly RelativeDisplayEntry[] behind;

        public RelativeDisplaySnapshot()
        {
            ahead =
                new RelativeDisplayEntry[SlotCount];

            behind =
                new RelativeDisplayEntry[SlotCount];

            for (int index = 0;
                 index < SlotCount;
                 index++)
            {
                ahead[index] =
                    new RelativeDisplayEntry();

                behind[index] =
                    new RelativeDisplayEntry();
            }

            Player =
                new RelativeDisplayEntry();

            Reset();
        }

        public RelativeDisplayEntry Player
        {
            get;
            private set;
        }

        public int AheadCount
        {
            get;
            private set;
        }

        public int BehindCount
        {
            get;
            private set;
        }

        public RelativeDisplayEntry GetAhead(
            int index)
        {
            ValidateIndex(index);

            return ahead[index];
        }

        public RelativeDisplayEntry GetBehind(
            int index)
        {
            ValidateIndex(index);

            return behind[index];
        }

        public void RefreshCounts()
        {
            int aheadCount = 0;
            int behindCount = 0;

            for (int index = 0;
                 index < SlotCount;
                 index++)
            {
                if (ahead[index].HasData)
                {
                    aheadCount++;
                }

                if (behind[index].HasData)
                {
                    behindCount++;
                }
            }

            AheadCount = aheadCount;
            BehindCount = behindCount;
        }

        public void Reset()
        {
            Player.Reset();

            for (int index = 0;
                 index < SlotCount;
                 index++)
            {
                ahead[index].Reset();
                behind[index].Reset();
            }

            AheadCount = 0;
            BehindCount = 0;
        }

        private static void ValidateIndex(
            int index)
        {
            if (index < 0 ||
                index >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index));
            }
        }
    }
}