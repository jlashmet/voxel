using System;
using Game.GameplayReplication.Api;
using Game.Progression.Api;
using Game.ProgressionPresentation.Api;
using Game.Sessions.Api;

namespace Game.ProgressionPresentation.Runtime
{
    /// <summary>
    /// Read-only adapter from the replicated current-state seam to the same Progression snapshot
    /// consumed by the journal presenter. It never owns or mutates progression state.
    /// </summary>
    public sealed class ReplicatedProgressionQuery : IProgressionQuery
    {
        private readonly IGameplayReplicationClientState _replication;
        private readonly PartyMemberId _memberId;

        public ReplicatedProgressionQuery(IGameplayReplicationClientState replication, PartyMemberId memberId)
        {
            _replication = replication ?? throw new ArgumentNullException(nameof(replication));
            _memberId = memberId;
        }

        public ProgressionSnapshot Snapshot()
        {
            if (!_replication.TryGetSynchronization(_memberId, out GameplaySynchronizationStatus synchronization) ||
                !synchronization.GameplayReady)
                throw new InvalidOperationException("Progression presentation requires a gameplay-ready replicated current state.");

            if (!_replication.TryGetCurrent(_memberId, out GameplayProjectionSnapshot<ProgressionPresentationCurrentState> current))
                throw new InvalidOperationException("Replicated progression current state is unavailable.");

            if (current.Revision != synchronization.Revision)
                throw new InvalidOperationException("Replicated progression current state is not coherent with synchronization revision.");

            return current.State.Snapshot;
        }
    }
}
