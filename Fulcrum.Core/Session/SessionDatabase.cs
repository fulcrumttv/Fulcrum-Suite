using System;

namespace Fulcrum.Core.Session
{
    /// <summary>
    /// Fixed-capacity lookup database for participant identity data.
    ///
    /// Entries are indexed directly by iRacing CarIndex so lookups
    /// require no searching and produce no allocations.
    /// </summary>
    public sealed class SessionDatabase
    {
        public const int Capacity = 64;

        private readonly DriverIdentity[] drivers;

        public SessionDatabase()
        {
            drivers =
                new DriverIdentity[Capacity];

            for (int index = 0;
                 index < Capacity;
                 index++)
            {
                drivers[index] =
                    new DriverIdentity();
            }

            Reset();
        }

        public int ValidDriverCount
        {
            get;
            private set;
        }

        public DriverIdentity Get(
            int carIndex)
        {
            if (!IsValidIndex(carIndex))
            {
                return null;
            }

            DriverIdentity identity =
                drivers[carIndex];

            if (!identity.IsValid)
            {
                return null;
            }

            return identity;
        }

        public bool TryGet(
            int carIndex,
            out DriverIdentity identity)
        {
            identity = Get(carIndex);

            return identity != null;
        }

        public DriverIdentity GetWritable(
            int carIndex)
        {
            if (!IsValidIndex(carIndex))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(carIndex));
            }

            return drivers[carIndex];
        }

        public void SetDriver(
            int carIndex,
            string driverName,
            string carNumber,
            string teamName,
            string className)
        {
            if (!IsValidIndex(carIndex))
            {
                return;
            }

            bool wasValid =
                drivers[carIndex].IsValid;

            drivers[carIndex].Set(
                carIndex,
                driverName,
                carNumber,
                teamName,
                className);

            if (!wasValid &&
                drivers[carIndex].IsValid)
            {
                ValidDriverCount++;
            }
        }

        public void RemoveDriver(
            int carIndex)
        {
            if (!IsValidIndex(carIndex))
            {
                return;
            }

            if (drivers[carIndex].IsValid)
            {
                ValidDriverCount--;
            }

            drivers[carIndex].Reset();
        }

        public void RefreshValidDriverCount()
        {
            int count = 0;

            for (int index = 0;
                 index < Capacity;
                 index++)
            {
                if (drivers[index].IsValid)
                {
                    count++;
                }
            }

            ValidDriverCount = count;
        }

        public void Reset()
        {
            for (int index = 0;
                 index < Capacity;
                 index++)
            {
                drivers[index].Reset();
            }

            ValidDriverCount = 0;
        }

        private static bool IsValidIndex(
            int carIndex)
        {
            return
                carIndex >= 0 &&
                carIndex < Capacity;
        }
    }
}