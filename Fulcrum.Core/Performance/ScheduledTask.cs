using System;
using System.Diagnostics;

namespace Fulcrum.Core.Performance
{
    /// <summary>
    /// Represents a task executed by the Fulcrum update scheduler
    /// at a controlled frequency.
    /// </summary>
    public sealed class ScheduledTask
    {
        private readonly Action updateAction;
        private readonly long intervalTicks;

        private long nextRunTicks;

        public ScheduledTask(
            string name,
            double frequencyHz,
            Action updateAction)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "A scheduled task must have a name.",
                    nameof(name));
            }

            if (frequencyHz <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(frequencyHz),
                    "Frequency must be greater than zero.");
            }

            if (updateAction == null)
            {
                throw new ArgumentNullException(
                    nameof(updateAction));
            }

            Name = name;
            FrequencyHz = frequencyHz;

            this.updateAction = updateAction;

            intervalTicks =
                Math.Max(
                    1L,
                    (long)(
                        Stopwatch.Frequency /
                        frequencyHz));

            Enabled = true;
            LastErrorMessage = string.Empty;
        }

        public string Name
        {
            get;
            private set;
        }

        public double FrequencyHz
        {
            get;
            private set;
        }

        public bool Enabled
        {
            get;
            set;
        }

        public long ExecutionCount
        {
            get;
            private set;
        }

        public long ErrorCount
        {
            get;
            private set;
        }

        public double LastExecutionMilliseconds
        {
            get;
            private set;
        }

        public double PeakExecutionMilliseconds
        {
            get;
            private set;
        }

        public string LastErrorMessage
        {
            get;
            private set;
        }

        internal void Initialize(
            long currentSchedulerTicks,
            bool runImmediately)
        {
            if (runImmediately)
            {
                nextRunTicks =
                    currentSchedulerTicks;
            }
            else
            {
                nextRunTicks =
                    currentSchedulerTicks +
                    intervalTicks;
            }
        }

        internal bool TryExecute(
            long currentSchedulerTicks)
        {
            if (!Enabled)
            {
                return false;
            }

            if (currentSchedulerTicks <
                nextRunTicks)
            {
                return false;
            }

            AdvanceNextRunTime(
                currentSchedulerTicks);

            long executionStart =
                Stopwatch.GetTimestamp();

            try
            {
                updateAction();

                ExecutionCount++;

                LastErrorMessage =
                    string.Empty;
            }
            catch (Exception exception)
            {
                ErrorCount++;

                LastErrorMessage =
                    exception.Message ??
                    exception.GetType().Name;
            }
            finally
            {
                long executionEnd =
                    Stopwatch.GetTimestamp();

                long elapsedTicks =
                    executionEnd -
                    executionStart;

                double elapsedMilliseconds =
                    elapsedTicks *
                    1000.0 /
                    Stopwatch.Frequency;

                LastExecutionMilliseconds =
                    elapsedMilliseconds;

                if (elapsedMilliseconds >
                    PeakExecutionMilliseconds)
                {
                    PeakExecutionMilliseconds =
                        elapsedMilliseconds;
                }
            }

            return true;
        }

        internal void ResetStatistics()
        {
            ExecutionCount = 0;
            ErrorCount = 0;

            LastExecutionMilliseconds =
                0.0;

            PeakExecutionMilliseconds =
                0.0;

            LastErrorMessage =
                string.Empty;
        }

        private void AdvanceNextRunTime(
            long currentSchedulerTicks)
        {
            long timeBehind =
                currentSchedulerTicks -
                nextRunTicks;

            long intervalsPassed =
                (timeBehind /
                 intervalTicks) +
                1L;

            nextRunTicks +=
                intervalsPassed *
                intervalTicks;
        }
    }
}