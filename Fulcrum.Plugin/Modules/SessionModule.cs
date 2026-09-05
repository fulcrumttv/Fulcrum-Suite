using System;
using Fulcrum.Core.Performance;
using Fulcrum.Core.Session;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Modules
{
    /// <summary>
    /// Maintains the shared SessionDatabase and exposes identity data to SimHub.
    /// </summary>
    public sealed class SessionModule
    {
        private const double UpdateHz = 1.0;
        private const int PublishedDriverCount = SessionDatabase.Capacity;

        private readonly PluginManager pluginManager;
        private readonly Type pluginType;
        private readonly SessionInfoReader reader;
        private readonly ScheduledTask updateTask;
        private readonly string[] driverPrefixes;

        private object latestRawData;
        private bool latestGameRunning;

        public SessionModule(
            PluginManager pluginManager,
            Type pluginType,
            UpdateScheduler updateScheduler)
        {
            if (pluginManager == null) throw new ArgumentNullException(nameof(pluginManager));
            if (pluginType == null) throw new ArgumentNullException(nameof(pluginType));
            if (updateScheduler == null) throw new ArgumentNullException(nameof(updateScheduler));

            this.pluginManager = pluginManager;
            this.pluginType = pluginType;

            Database = new SessionDatabase();
            reader = new SessionInfoReader();
            driverPrefixes = new string[PublishedDriverCount];

            RegisterProperties();

            updateTask = updateScheduler.RegisterTask(
                "Session Module",
                UpdateHz,
                UpdateScheduled,
                false);

            Reset();
        }

        public SessionDatabase Database { get; private set; }

        public void SetFrameContext(object rawData, bool gameRunning)
        {
            latestRawData = rawData;
            latestGameRunning = gameRunning;
        }

        public void Reset()
        {
            latestRawData = null;
            latestGameRunning = false;
            reader.Reset(Database);
            Publish();
        }

        private void UpdateScheduled()
        {
            if (!latestGameRunning)
            {
                reader.Reset(Database);
                Publish();
                return;
            }

            // Keep the last valid SessionInfo during short telemetry gaps.
            // SimHub can briefly supply a null frame while the session itself
            // remains active; clearing the database here made names and logos
            // disappear from the Relative.
            if (latestRawData == null)
            {
                Publish();
                return;
            }

            reader.Update(latestRawData, Database);
            Publish();
        }

        private void RegisterProperties()
        {
            Add("Fulcrum.Session.Ready", false, "True when driver identity data is available");
            Add("Fulcrum.Session.DriverCount", 0, "Number of valid drivers read from SessionInfo");
            Add("Fulcrum.Session.StrengthOfField", 0, "Estimated iRacing strength of field from valid driver iRatings");
            Add("Fulcrum.Session.HasSessionData", false, "True when iRacing SessionData is available");
            Add("Fulcrum.Session.HasDriverInfo", false, "True when iRacing DriverInfo is available");
            Add("Fulcrum.Session.Error", string.Empty, "Latest SessionInfo reader error");
            Add("Fulcrum.Session.UpdatedAtUtc", string.Empty, "UTC timestamp of the latest identity update");
            Add("Fulcrum.Session.ExecutionCount", 0L, "Number of SessionInfo reader executions");
            Add("Fulcrum.Session.LastExecutionMs", 0.0, "Latest SessionInfo reader execution time");

            for (int index = 0; index < PublishedDriverCount; index++)
            {
                string prefix = "Fulcrum.Session.Driver." + index.ToString("00") + ".";
                driverPrefixes[index] = prefix;

                Add(prefix + "Valid", false, "True when this CarIndex has identity data");
                Add(prefix + "CarIndex", index, "iRacing CarIndex");
                Add(prefix + "Name", string.Empty, "Driver name");
                Add(prefix + "CarNumber", string.Empty, "Car number");
                Add(prefix + "TeamName", string.Empty, "Team name");
                Add(prefix + "ClassName", string.Empty, "Car class");
                Add(prefix + "Manufacturer", string.Empty, "Car manufacturer");
                Add(prefix + "IRating", 0, "Driver iRating");
                Add(prefix + "License", string.Empty, "Driver license string");
            }
        }

        private void Publish()
        {
            Set("Fulcrum.Session.Ready", Database.ValidDriverCount > 0);
            Set("Fulcrum.Session.DriverCount", Database.ValidDriverCount);
            Set("Fulcrum.Session.StrengthOfField", CalculateStrengthOfField());
            Set("Fulcrum.Session.HasSessionData", reader.HasSessionData);
            Set("Fulcrum.Session.HasDriverInfo", reader.HasDriverInfo);
            Set("Fulcrum.Session.Error", reader.LastError ?? string.Empty);
            Set("Fulcrum.Session.UpdatedAtUtc", reader.LastUpdatedUtc == DateTime.MinValue ? string.Empty : reader.LastUpdatedUtc.ToString("O"));
            Set("Fulcrum.Session.ExecutionCount", updateTask != null ? updateTask.ExecutionCount : 0L);
            Set("Fulcrum.Session.LastExecutionMs", updateTask != null ? updateTask.LastExecutionMilliseconds : 0.0);

            for (int index = 0; index < PublishedDriverCount; index++)
            {
                DriverIdentity identity = Database.Get(index);
                string prefix = driverPrefixes[index];
                bool valid = identity != null;

                Set(prefix + "Valid", valid);
                Set(prefix + "CarIndex", index);
                Set(prefix + "Name", valid ? identity.DriverName : string.Empty);
                Set(prefix + "CarNumber", valid ? identity.CarNumber : string.Empty);
                Set(prefix + "TeamName", valid ? identity.TeamName : string.Empty);
                Set(prefix + "ClassName", valid ? identity.ClassName : string.Empty);
                Set(prefix + "Manufacturer", valid ? identity.Manufacturer : string.Empty);
                Set(prefix + "IRating", valid ? identity.IRating : 0);
                Set(prefix + "License", valid ? identity.License : string.Empty);
            }
        }


        private int CalculateStrengthOfField()
        {
            int count = 0;
            double denominator = 0.0;

            for (int index = 0; index < PublishedDriverCount; index++)
            {
                DriverIdentity identity = Database.Get(index);

                if (identity == null || identity.IRating <= 0)
                {
                    continue;
                }

                denominator += Math.Pow(2.0, -identity.IRating / 1600.0);
                count++;
            }

            if (count == 0 || denominator <= 0.0)
            {
                return 0;
            }

            double strength =
                1600.0 / Math.Log(2.0) *
                Math.Log(count / denominator);

            return strength > 0.0
                ? (int)Math.Round(strength)
                : 0;
        }

        private void Add(string name, object value, string description)
        {
            pluginManager.AddProperty(name, pluginType, value, description);
        }

        private void Set(string name, object value)
        {
            pluginManager.SetPropertyValue(name, pluginType, value);
        }
    }
}
