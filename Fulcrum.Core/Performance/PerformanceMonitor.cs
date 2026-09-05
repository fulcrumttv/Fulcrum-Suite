using System.Diagnostics;

namespace Fulcrum.Core.Performance
{
    /// <summary>
    /// Measures the execution cost of the Fulcrum plugin.
    /// It does not control scheduling.
    /// </summary>
    public sealed class PerformanceMonitor
    {
        private long updateStartTimestamp;
        private long measurementWindowStartTimestamp;

        private long windowUpdateCount;
        private long windowTotalExecutionTicks;

        public PerformanceMonitor()
        {
            Reset();
        }

        public double LastUpdateMilliseconds
        {
            get;
            private set;
        }

        public double AverageUpdateMilliseconds
        {
            get;
            private set;
        }

        public double PeakUpdateMilliseconds
        {
            get;
            private set;
        }

        public double UpdatesPerSecond
        {
            get;
            private set;
        }

        public long TotalUpdateCount
        {
            get;
            private set;
        }

        public void BeginUpdate()
        {
            updateStartTimestamp =
                Stopwatch.GetTimestamp();
        }

        public void EndUpdate()
        {
            long updateEndTimestamp =
                Stopwatch.GetTimestamp();

            long elapsedTicks =
                updateEndTimestamp -
                updateStartTimestamp;

            if (elapsedTicks < 0)
            {
                elapsedTicks = 0;
            }

            LastUpdateMilliseconds =
                ConvertTicksToMilliseconds(
                    elapsedTicks);

            if (LastUpdateMilliseconds >
                PeakUpdateMilliseconds)
            {
                PeakUpdateMilliseconds =
                    LastUpdateMilliseconds;
            }

            TotalUpdateCount++;
            windowUpdateCount++;

            windowTotalExecutionTicks +=
                elapsedTicks;

            UpdateMeasurementWindow(
                updateEndTimestamp);
        }

        public void Reset()
        {
            long currentTimestamp =
                Stopwatch.GetTimestamp();

            updateStartTimestamp =
                currentTimestamp;

            measurementWindowStartTimestamp =
                currentTimestamp;

            windowUpdateCount = 0;
            windowTotalExecutionTicks = 0;

            LastUpdateMilliseconds = 0.0;
            AverageUpdateMilliseconds = 0.0;
            PeakUpdateMilliseconds = 0.0;
            UpdatesPerSecond = 0.0;

            TotalUpdateCount = 0;
        }

        public void ResetPeak()
        {
            PeakUpdateMilliseconds =
                LastUpdateMilliseconds;
        }

        private void UpdateMeasurementWindow(
            long currentTimestamp)
        {
            long windowElapsedTicks =
                currentTimestamp -
                measurementWindowStartTimestamp;

            if (windowElapsedTicks <
                Stopwatch.Frequency)
            {
                return;
            }

            if (windowUpdateCount > 0)
            {
                long averageTicks =
                    windowTotalExecutionTicks /
                    windowUpdateCount;

                AverageUpdateMilliseconds =
                    ConvertTicksToMilliseconds(
                        averageTicks);

                UpdatesPerSecond =
                    windowUpdateCount *
                    (double)Stopwatch.Frequency /
                    windowElapsedTicks;
            }
            else
            {
                AverageUpdateMilliseconds =
                    0.0;

                UpdatesPerSecond =
                    0.0;
            }

            measurementWindowStartTimestamp =
                currentTimestamp;

            windowUpdateCount = 0;
            windowTotalExecutionTicks = 0;
        }

        private static double
            ConvertTicksToMilliseconds(
                long ticks)
        {
            return ticks *
                   1000.0 /
                   Stopwatch.Frequency;
        }
    }
}