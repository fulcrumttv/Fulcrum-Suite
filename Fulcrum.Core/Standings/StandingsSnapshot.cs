using System;

namespace Fulcrum.Core.Standings
{
    public sealed class StandingsSnapshot
    {
        public const int PublishedRowCount = 20;
        private readonly StandingsEntry[] rows;

        public StandingsSnapshot()
        {
            rows = new StandingsEntry[PublishedRowCount];
            for (int index = 0; index < rows.Length; index++)
            {
                rows[index] = new StandingsEntry();
            }
            Reset();
        }

        public bool Ready { get; internal set; }
        public int ParticipantCount { get; internal set; }
        public int PublishedCount { get; internal set; }
        public int PlayerRow { get; internal set; }
        public int LeaderCarIndex { get; internal set; }
        public string LeaderName { get; internal set; }
        public string Error { get; internal set; }

        public StandingsEntry GetRow(int index)
        {
            if (index < 0 || index >= rows.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return rows[index];
        }

        public void Reset()
        {
            Ready = false;
            ParticipantCount = 0;
            PublishedCount = 0;
            PlayerRow = 0;
            LeaderCarIndex = -1;
            LeaderName = string.Empty;
            Error = string.Empty;
            for (int index = 0; index < rows.Length; index++)
            {
                rows[index].Reset();
            }
        }
    }
}
