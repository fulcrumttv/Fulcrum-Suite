using System;
using Fulcrum.Core.Performance;
using Fulcrum.Core.Relative;
using Fulcrum.Core.Session;
using Fulcrum.Core.Standings;
using Fulcrum.Plugin.Publishing;
using SimHub.Plugins;

namespace Fulcrum.Plugin.Modules
{
    public sealed class StandingsModule
    {
        private readonly ParticipantBuffer participantBuffer;
        private readonly SessionDatabase sessionDatabase;
        private readonly StandingsEngine engine;
        private readonly StandingsSnapshot snapshot;
        private readonly StandingsPublisher publisher;
        private readonly ScheduledTask updateTask;

        private bool gameRunning;
        private int playerCarIndex;

        public StandingsModule(
            PluginManager pluginManager,
            Type pluginType,
            UpdateScheduler scheduler,
            ParticipantBuffer participantBuffer,
            SessionDatabase sessionDatabase)
        {
            if (pluginManager == null) throw new ArgumentNullException(nameof(pluginManager));
            if (pluginType == null) throw new ArgumentNullException(nameof(pluginType));
            if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));
            this.participantBuffer = participantBuffer ?? throw new ArgumentNullException(nameof(participantBuffer));
            this.sessionDatabase = sessionDatabase ?? throw new ArgumentNullException(nameof(sessionDatabase));

            engine = new StandingsEngine();
            snapshot = new StandingsSnapshot();
            publisher = new StandingsPublisher(pluginManager, pluginType);
            updateTask = scheduler.RegisterTask(
                "Standings Engine",
                UpdateRates.StandingsHz,
                UpdateScheduled,
                false);
            Reset();
        }

        public StandingsSnapshot Snapshot
        {
            get { return snapshot; }
        }

        public void SetFrameContext(bool isGameRunning, int currentPlayerCarIndex)
        {
            gameRunning = isGameRunning;
            playerCarIndex = currentPlayerCarIndex;
        }

        public void Reset()
        {
            gameRunning = false;
            playerCarIndex = -1;
            snapshot.Reset();
            publisher.Publish(snapshot);
        }

        private void UpdateScheduled()
        {
            if (!gameRunning || playerCarIndex < 0)
            {
                snapshot.Reset();
            }
            else
            {
                engine.Update(
                    participantBuffer,
                    sessionDatabase,
                    playerCarIndex,
                    snapshot);
            }

            publisher.Publish(snapshot);
        }
    }
}
