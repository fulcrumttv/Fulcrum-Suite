using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Fulcrum.Core.Performance
{
    /// <summary>
    /// Controls when Fulcrum modules are updated.
    /// All scheduled tasks share one high-resolution clock.
    /// </summary>
    public sealed class UpdateScheduler
    {
        private readonly Stopwatch clock;
        private readonly List<ScheduledTask> tasks;

        public UpdateScheduler()
        {
            clock =
                new Stopwatch();

            tasks =
                new List<ScheduledTask>(8);

            clock.Start();
        }

        public int TaskCount
        {
            get
            {
                return tasks.Count;
            }
        }

        public ScheduledTask RegisterTask(
            string name,
            double frequencyHz,
            Action updateAction)
        {
            return RegisterTask(
                name,
                frequencyHz,
                updateAction,
                false);
        }

        public ScheduledTask RegisterTask(
            string name,
            double frequencyHz,
            Action updateAction,
            bool runImmediately)
        {
            ScheduledTask task =
                new ScheduledTask(
                    name,
                    frequencyHz,
                    updateAction);

            task.Initialize(
                clock.ElapsedTicks,
                runImmediately);

            tasks.Add(task);

            return task;
        }

        public void Update()
        {
            long currentTicks =
                clock.ElapsedTicks;

            int taskCount =
                tasks.Count;

            for (int index = 0;
                 index < taskCount;
                 index++)
            {
                tasks[index].TryExecute(
                    currentTicks);
            }
        }

        public ScheduledTask GetTask(
            string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            int taskCount =
                tasks.Count;

            for (int index = 0;
                 index < taskCount;
                 index++)
            {
                ScheduledTask task =
                    tasks[index];

                if (string.Equals(
                        task.Name,
                        name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return task;
                }
            }

            return null;
        }

        public void Reset()
        {
            clock.Restart();

            long currentTicks =
                clock.ElapsedTicks;

            int taskCount =
                tasks.Count;

            for (int index = 0;
                 index < taskCount;
                 index++)
            {
                ScheduledTask task =
                    tasks[index];

                task.Initialize(
                    currentTicks,
                    false);

                task.ResetStatistics();
            }
        }

        public void Clear()
        {
            tasks.Clear();

            clock.Restart();
        }
    }
}