using System;

namespace Fulcrum.Plugin.Settings
{
    public enum TimingReferenceMode
    {
        PersonalBest = 0,
        ClassBest = 1
    }

    [Serializable]
    public sealed class TimingReferenceSettings
    {
        // Preserve current Fulcrum behavior as the default.
        public TimingReferenceMode ReferenceMode { get; set; } = TimingReferenceMode.ClassBest;

        public void Normalize()
        {
            if (ReferenceMode != TimingReferenceMode.PersonalBest &&
                ReferenceMode != TimingReferenceMode.ClassBest)
            {
                ReferenceMode = TimingReferenceMode.ClassBest;
            }
        }
    }
}
