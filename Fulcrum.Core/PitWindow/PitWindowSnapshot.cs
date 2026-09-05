using System;

namespace Fulcrum.Core.PitWindow
{
    public sealed class PitWindowSnapshot
    {
        public bool Ready { get; set; }
        public bool HasWindow { get; set; }
        public string Status { get; set; }
        public string Summary { get; set; }
        public string Recommendation { get; set; }

        public int CurrentLap { get; set; }
        public int OpenLap { get; set; }
        public int CloseLap { get; set; }
        public bool IsOpen { get; set; }
        public bool MustPitThisLap { get; set; }
        public bool CanReachWindow { get; set; }
        public int LapsUntilOpen { get; set; }
        public int LapsUntilClose { get; set; }
        public string WindowText { get; set; }
        public string CountdownText { get; set; }

        public bool IsOnPitRoad { get; set; }
        public bool JustEnteredPits { get; set; }
        public bool JustExitedPits { get; set; }
        public int PitStopCount { get; set; }
        public int LastPitEntryLap { get; set; }
        public int LastPitExitLap { get; set; }
        public int CurrentStintLap { get; set; }

        public double FuelLapsRemaining { get; set; }
        public double FullTankStintLaps { get; set; }
        public double EstimatedSessionLapsRemaining { get; set; }
        public int EstimatedStopsRemaining { get; set; }
        public double RecommendedFuelToAddLiters { get; set; }
        public double MaximumFuelToAddLiters { get; set; }
        public bool CanFinishWithoutStop { get; set; }

        public string EventName { get; set; }
        public int EventSequence { get; set; }
        public DateTime UpdatedAtUtc { get; set; }

        public PitWindowSnapshot()
        {
            Reset();
        }

        public void Reset()
        {
            Ready = false;
            HasWindow = false;
            Status = "Unavailable";
            Summary = "Waiting for fuel and session data";
            Recommendation = "Monitor";
            CurrentLap = 0;
            OpenLap = 0;
            CloseLap = 0;
            IsOpen = false;
            MustPitThisLap = false;
            CanReachWindow = false;
            LapsUntilOpen = 0;
            LapsUntilClose = 0;
            WindowText = "--";
            CountdownText = "--";
            IsOnPitRoad = false;
            JustEnteredPits = false;
            JustExitedPits = false;
            PitStopCount = 0;
            LastPitEntryLap = -1;
            LastPitExitLap = -1;
            CurrentStintLap = 0;
            FuelLapsRemaining = 0.0;
            FullTankStintLaps = 0.0;
            EstimatedSessionLapsRemaining = 0.0;
            EstimatedStopsRemaining = 0;
            RecommendedFuelToAddLiters = 0.0;
            MaximumFuelToAddLiters = 0.0;
            CanFinishWithoutStop = false;
            EventName = "None";
            EventSequence = 0;
            UpdatedAtUtc = DateTime.MinValue;
        }
    }
}
