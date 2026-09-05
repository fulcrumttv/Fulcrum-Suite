namespace Fulcrum.Core.Session
{
    /// <summary>
    /// Stores static identity information for one participant.
    ///
    /// The telemetry system identifies participants by CarIndex.
    /// This class associates that index with human-readable data
    /// obtained from iRacing SessionInfo.
    /// </summary>
    public sealed class DriverIdentity
    {
        public bool IsValid
        {
            get;
            private set;
        }

        public int CarIndex
        {
            get;
            private set;
        }

        public string DriverName
        {
            get;
            private set;
        }

        public string CarNumber
        {
            get;
            private set;
        }

        public string TeamName
        {
            get;
            private set;
        }

        public string ClassName
        {
            get;
            private set;
        }

        public string Manufacturer
        {
            get;
            private set;
        }

        public int IRating
        {
            get;
            private set;
        }

        public string License
        {
            get;
            private set;
        }

        public string ClubName
        {
            get;
            private set;
        }

        public int UserId { get; private set; }
        public int CarId { get; private set; }
        public int ClassId { get; private set; }
        public bool IsNonCompetitor { get; private set; }

        public void SetClassIdentity(int classId, bool isNonCompetitor)
        {
            ClassId = classId;
            IsNonCompetitor = isNonCompetitor;
        }
        public float CarClassEstimatedLapTime { get; private set; }
        public string CarPath { get; private set; }
        public string CarScreenName { get; private set; }
        public string CarName { get; private set; }
        public string DriverInfoRaw { get; private set; }

        public string ManufacturerAlias { get; private set; }
        public string LogoResourceKey { get; private set; }
        public string CountryAlias { get; private set; }
        public string FlagResourceKey { get; private set; }

        public string FlagText
        {
            get;
            private set;
        }

        public DriverIdentity()
        {
            Reset();
        }

        public void Reset()
        {
            IsValid = false;
            CarIndex = -1;

            DriverName = string.Empty;
            CarNumber = string.Empty;
            TeamName = string.Empty;
            ClassName = string.Empty;
            Manufacturer = string.Empty;
            License = string.Empty;
            ClubName = string.Empty;
            FlagText = string.Empty;
            UserId = 0;
            CarId = 0;
            ClassId = -1;
            IsNonCompetitor = false;
            CarClassEstimatedLapTime = 0.0f;
            CarPath = string.Empty;
            CarScreenName = string.Empty;
            CarName = string.Empty;
            DriverInfoRaw = string.Empty;
            ManufacturerAlias = string.Empty;
            LogoResourceKey = string.Empty;
            CountryAlias = string.Empty;
            FlagResourceKey = string.Empty;

            IRating = 0;
        }

        public void Set(
            int carIndex,
            string driverName,
            string carNumber,
            string teamName,
            string className)
        {
            Reset();

            if (carIndex < 0)
            {
                return;
            }

            IsValid = true;
            CarIndex = carIndex;

            DriverName =
                driverName ?? string.Empty;

            CarNumber =
                carNumber ?? string.Empty;

            TeamName =
                teamName ?? string.Empty;

            ClassName =
                className ?? string.Empty;
        }

        public void SetExtendedData(
            string manufacturer,
            int iRating,
            string license,
            string clubName,
            string flagText)
        {
            Manufacturer =
                manufacturer ?? string.Empty;

            IRating =
                iRating > 0
                    ? iRating
                    : 0;

            License =
                license ?? string.Empty;

            ClubName =
                clubName ?? string.Empty;

            FlagText =
                flagText ?? string.Empty;
        }
        public void SetDiagnosticData(
            int userId,
            int carId,
            string carPath,
            string carScreenName,
            string carName,
            string driverInfoRaw,
            float carClassEstimatedLapTime)
        {
            UserId = userId > 0 ? userId : 0;
            CarId = carId > 0 ? carId : 0;
            CarClassEstimatedLapTime =
                carClassEstimatedLapTime > 0.0f
                    ? carClassEstimatedLapTime
                    : 0.0f;
            CarPath = carPath ?? string.Empty;
            CarScreenName = carScreenName ?? string.Empty;
            CarName = carName ?? string.Empty;
            DriverInfoRaw = driverInfoRaw ?? string.Empty;
        }

        public void SetResourceData(
            string manufacturerAlias,
            string logoResourceKey,
            string countryAlias,
            string flagResourceKey)
        {
            ManufacturerAlias = manufacturerAlias ?? string.Empty;
            LogoResourceKey = logoResourceKey ?? string.Empty;
            CountryAlias = countryAlias ?? string.Empty;
            FlagResourceKey = flagResourceKey ?? string.Empty;
        }

    }
}
