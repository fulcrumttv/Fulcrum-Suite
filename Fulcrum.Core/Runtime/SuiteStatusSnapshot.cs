using System;

namespace Fulcrum.Core.Runtime
{
    public sealed class SuiteStatusSnapshot
    {
        public bool Ready { get; set; }
        public string Health { get; set; }
        public string Summary { get; set; }
        public int ReadyModuleCount { get; set; }
        public int TotalModuleCount { get; set; }
        public string PrimaryAlert { get; set; }
        public int PrimaryAlertPriority { get; set; }
        public string PrimaryAction { get; set; }
        public string MissingModules { get; set; }
        public string CoreStatus { get; set; }

        public string RelativeStatus { get; set; }
        public string RadarStatus { get; set; }
        public string FuelStatus { get; set; }
        public string DamageStatus { get; set; }
        public string DeltaStatus { get; set; }
        public string SpotterStatus { get; set; }
        public string PitWindowStatus { get; set; }
        public string StrategyStatus { get; set; }
        public string StandingsStatus { get; set; }

        public bool RelativeReady { get; set; }
        public bool RadarReady { get; set; }
        public bool FuelReady { get; set; }
        public bool DamageReady { get; set; }
        public bool DeltaReady { get; set; }
        public bool SpotterReady { get; set; }
        public bool PitWindowReady { get; set; }
        public bool StrategyReady { get; set; }
        public bool StandingsReady { get; set; }

        public DateTime UpdatedAtUtc { get; set; }

        public SuiteStatusSnapshot()
        {
            Reset();
        }

        public void Reset()
        {
            Ready = false;
            Health = "Offline";
            Summary = "Waiting for game";
            ReadyModuleCount = 0;
            TotalModuleCount = 9;
            PrimaryAlert = "None";
            PrimaryAlertPriority = 0;
            PrimaryAction = "Wait";
            MissingModules = "Relative,Radar,Fuel,Damage,Delta,Spotter,PitWindow,Strategy,Standings";
            CoreStatus = "Waiting for game";

            RelativeStatus = "Waiting";
            RadarStatus = "Waiting";
            FuelStatus = "Waiting";
            DamageStatus = "Waiting";
            DeltaStatus = "Waiting";
            SpotterStatus = "Waiting";
            PitWindowStatus = "Waiting";
            StrategyStatus = "Waiting";
            StandingsStatus = "Waiting";

            RelativeReady = false;
            RadarReady = false;
            FuelReady = false;
            DamageReady = false;
            DeltaReady = false;
            SpotterReady = false;
            PitWindowReady = false;
            StrategyReady = false;
            StandingsReady = false;

            UpdatedAtUtc = DateTime.MinValue;
        }
    }
}
