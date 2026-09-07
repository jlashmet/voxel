using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Continuity.Api;
using Game.Continuity.Runtime;
using Game.GameplayReplication.Api;
using Game.Sessions.Api;
using Game.Sessions.Runtime;
using VoxelEngine.Net.Api;

namespace Game.Composition.Kentridge.Playable
{
    /// <summary>
    /// Semantic replication status adapter for Continuity. The existing gameplay emitter guarantees
    /// a full current snapshot for every newly authenticated connection, so recovery is complete only
    /// after the authoritative publication revision advances beyond the reconnect request revision.
    /// </summary>
    public sealed class KentridgeContinuityReplicationState : IGameplayReplicationClientState
    {
        private readonly Func<GameplayRevision> _currentRevision;
        private readonly Dictionary<PartyMemberId, GameplayRevision> _requestedAt =
            new Dictionary<PartyMemberId, GameplayRevision>();

        public KentridgeContinuityReplicationState(Func<GameplayRevision> currentRevision)
        {
            _currentRevision = currentRevision ?? throw new ArgumentNullException(nameof(currentRevision));
        }

        public void RequestRecovery(PartyMemberId memberId, GameplayRecoveryMode mode)
        {
            if (!memberId.IsValid) throw new ArgumentException("Member id is required.", nameof(memberId));
            _requestedAt[memberId] = _currentRevision();
        }

        public bool TryGetSynchronization(PartyMemberId memberId, out GameplaySynchronizationStatus status)
        {
            status = default;
            if (!_requestedAt.TryGetValue(memberId, out GameplayRevision requestedAt)) return false;
            GameplayRevision current = _currentRevision();
            bool ready = current.IsValid && current.CompareTo(requestedAt) > 0;
            status = new GameplaySynchronizationStatus(
                ready ? GameplaySynchronizationPhase.GameplayReady : GameplaySynchronizationPhase.Synchronizing,
                current);
            return true;
        }

        public bool TryGetCurrent<TState>(
            PartyMemberId memberId,
            out GameplayProjectionSnapshot<TState> snapshot) where TState : struct
        {
            snapshot = default;
            return false;
        }
    }

    /// <summary>Reconnects Continuity's durable member through the same SessionNetworkAdmissionAdapter used by initial admission.</summary>
    public sealed class KentridgeContinuityTransportAdmission : IReconnectTransportAdmission
    {
        private const string Prefix = "network:";
        private readonly SessionNetworkAdmissionAdapter _network;

        public KentridgeContinuityTransportAdmission(SessionNetworkAdmissionAdapter network)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
        }

        public bool TryBind(PartyMemberSnapshot member, RuntimeConnectionHandle connection)
        {
            if (!member.MemberId.IsValid || !connection.IsValid ||
                !connection.Value.StartsWith(Prefix, StringComparison.Ordinal) ||
                !uint.TryParse(connection.Value.Substring(Prefix.Length), NumberStyles.None,
                    CultureInfo.InvariantCulture, out uint connectionId) || connectionId == 0)
                return false;

            return _network.Authenticate(
                member.MemberId,
                connectionId,
                new NetworkSpawnPosition(0, 0, 0),
                8,
                true);
        }

        public static RuntimeConnectionHandle FromConnectionId(uint connectionId)
        {
            if (connectionId == 0) throw new ArgumentOutOfRangeException(nameof(connectionId));
            return new RuntimeConnectionHandle(Prefix + connectionId.ToString(CultureInfo.InvariantCulture));
        }
    }
}
