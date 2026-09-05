using System;
using System.Collections.Generic;
using Game.Continuity.Api;
using Game.GameplayReplication.Api;
using Game.Sessions.Api;

namespace Game.Composition.Kentridge.Playable.Validation
{
    /// <summary>
    /// Immutable validation view of one party member. It intentionally carries durable gameplay identity
    /// and semantic lifecycle state only; transient transport identity is not part of this contract.
    /// </summary>
    public readonly struct MultiplayerMemberDiagnostic
    {
        public string MemberId { get; }
        public int Slot { get; }
        public string CharacterId { get; }
        public PartyLeadershipRole LeadershipRole { get; }
        public PartyPresenceState Presence { get; }
        public SessionReadinessState Readiness { get; }
        public bool HasRecovery { get; }
        public RecoveryState RecoveryState { get; }
        public double GraceDeadline { get; }

        public MultiplayerMemberDiagnostic(
            string memberId,
            int slot,
            string characterId,
            PartyLeadershipRole leadershipRole,
            PartyPresenceState presence,
            SessionReadinessState readiness,
            bool hasRecovery,
            RecoveryState recoveryState,
            double graceDeadline)
        {
            MemberId = memberId ?? string.Empty;
            Slot = slot;
            CharacterId = characterId ?? string.Empty;
            LeadershipRole = leadershipRole;
            Presence = presence;
            Readiness = readiness;
            HasRecovery = hasRecovery;
            RecoveryState = recoveryState;
            GraceDeadline = graceDeadline;
        }
    }

    public readonly struct MultiplayerProjectionEntryDiagnostic
    {
        public string Key { get; }
        public string Value { get; }

        public MultiplayerProjectionEntryDiagnostic(string key, string value)
        {
            Key = key ?? string.Empty;
            Value = value ?? string.Empty;
        }
    }

    /// <summary>Copied semantic projection state at the captured gameplay revision.</summary>
    public sealed class MultiplayerProjectionDiagnostic
    {
        private readonly IReadOnlyList<MultiplayerProjectionEntryDiagnostic> _entries;

        public MultiplayerProjectionDiagnostic(
            string projectionId,
            int schemaVersion,
            bool requiredForGameplayReady,
            IReadOnlyList<MultiplayerProjectionEntryDiagnostic> entries)
        {
            ProjectionId = projectionId ?? string.Empty;
            SchemaVersion = schemaVersion;
            RequiredForGameplayReady = requiredForGameplayReady;
            if (entries == null) throw new ArgumentNullException(nameof(entries));

            var copy = new MultiplayerProjectionEntryDiagnostic[entries.Count];
            for (int i = 0; i < entries.Count; i++) copy[i] = entries[i];
            _entries = Array.AsReadOnly(copy);
        }

        public string ProjectionId { get; }
        public int SchemaVersion { get; }
        public bool RequiredForGameplayReady { get; }
        public IReadOnlyList<MultiplayerProjectionEntryDiagnostic> Entries => _entries;
    }

    /// <summary>
    /// Point-in-time semantic multiplayer diagnostic. This is evidence only: it exposes no setters,
    /// recovery commands, socket ids, provider tokens, or transport handles.
    /// </summary>
    public sealed class MultiplayerDiagnosticSnapshot
    {
        private readonly IReadOnlyList<MultiplayerMemberDiagnostic> _members;
        private readonly IReadOnlyList<MultiplayerProjectionDiagnostic> _projections;

        public MultiplayerDiagnosticSnapshot(
            string sessionId,
            ulong gameplayRevision,
            GameplaySynchronizationState synchronizationState,
            bool gameplayReady,
            IReadOnlyList<MultiplayerMemberDiagnostic> members,
            IReadOnlyList<MultiplayerProjectionDiagnostic> projections)
        {
            SessionId = sessionId ?? string.Empty;
            GameplayRevision = gameplayRevision;
            SynchronizationState = synchronizationState;
            GameplayReady = gameplayReady;
            if (members == null) throw new ArgumentNullException(nameof(members));
            if (projections == null) throw new ArgumentNullException(nameof(projections));

            var memberCopy = new MultiplayerMemberDiagnostic[members.Count];
            for (int i = 0; i < members.Count; i++) memberCopy[i] = members[i];
            _members = Array.AsReadOnly(memberCopy);

            var projectionCopy = new MultiplayerProjectionDiagnostic[projections.Count];
            for (int i = 0; i < projections.Count; i++) projectionCopy[i] = projections[i];
            _projections = Array.AsReadOnly(projectionCopy);
        }

        public string SessionId { get; }
        public ulong GameplayRevision { get; }
        public GameplaySynchronizationState SynchronizationState { get; }
        public bool GameplayReady { get; }
        public IReadOnlyList<MultiplayerMemberDiagnostic> Members => _members;
        public IReadOnlyList<MultiplayerProjectionDiagnostic> Projections => _projections;
    }

    /// <summary>
    /// Aggregates public read-only production queries into copied validation evidence. Requested projection
    /// ids are semantic/configuration input; the source cannot discover or command transport internals.
    /// </summary>
    public sealed class MultiplayerDiagnosticSnapshotSource
    {
        private readonly IPartySessionQuery _party;
        private readonly IGameplayReplicationReadState _replication;
        private readonly IContinuityQuery _continuity;
        private readonly GameplayProjectionId[] _projectionIds;

        public MultiplayerDiagnosticSnapshotSource(
            IPartySessionQuery party,
            IGameplayReplicationReadState replication,
            IContinuityQuery continuity,
            IEnumerable<GameplayProjectionId> projectionIds = null)
        {
            _party = party ?? throw new ArgumentNullException(nameof(party));
            _replication = replication ?? throw new ArgumentNullException(nameof(replication));
            _continuity = continuity ?? throw new ArgumentNullException(nameof(continuity));

            var ids = projectionIds == null
                ? new List<GameplayProjectionId>()
                : new List<GameplayProjectionId>(projectionIds);
            ids.Sort();
            for (int i = 1; i < ids.Count; i++)
            {
                if (ids[i - 1] == ids[i])
                    throw new ArgumentException("Projection ids must be unique.", nameof(projectionIds));
            }
            _projectionIds = ids.ToArray();
        }

        public MultiplayerDiagnosticSnapshot Capture()
        {
            PartyRosterSnapshot roster = _party.Snapshot();
            var members = new List<MultiplayerMemberDiagnostic>(roster.Members.Count);
            for (int i = 0; i < roster.Members.Count; i++)
            {
                PartyMemberSnapshot member = roster.Members[i];
                bool hasRecovery = _continuity.TryGetRecovery(member.MemberId, out RecoverySnapshot recovery);
                members.Add(new MultiplayerMemberDiagnostic(
                    member.MemberId.Value,
                    member.Slot.Value,
                    member.CharacterId.Value,
                    member.LeadershipRole,
                    member.Presence,
                    member.Readiness,
                    hasRecovery,
                    hasRecovery ? recovery.State : RecoveryState.Connected,
                    hasRecovery ? recovery.GraceDeadline : 0d));
            }
            members.Sort(CompareMembers);

            var projections = new List<MultiplayerProjectionDiagnostic>(_projectionIds.Length);
            for (int i = 0; i < _projectionIds.Length; i++)
            {
                GameplayProjectionId id = _projectionIds[i];
                if (!_replication.TryGetProjection(id, out GameplayProjectionState state)) continue;

                var entries = new MultiplayerProjectionEntryDiagnostic[state.Entries.Count];
                for (int entryIndex = 0; entryIndex < state.Entries.Count; entryIndex++)
                {
                    GameplayProjectionEntry entry = state.Entries[entryIndex];
                    entries[entryIndex] = new MultiplayerProjectionEntryDiagnostic(entry.Key, entry.Value);
                }
                projections.Add(new MultiplayerProjectionDiagnostic(
                    state.Descriptor.Id.Value,
                    state.Descriptor.SchemaVersion,
                    state.Descriptor.RequiredForGameplayReady,
                    entries));
            }

            return new MultiplayerDiagnosticSnapshot(
                roster.SessionId.Value,
                _replication.Revision.Value,
                _replication.SynchronizationState,
                _replication.GameplayReady,
                members,
                projections);
        }

        private static int CompareMembers(MultiplayerMemberDiagnostic left, MultiplayerMemberDiagnostic right)
        {
            int slot = left.Slot.CompareTo(right.Slot);
            return slot != 0 ? slot : StringComparer.Ordinal.Compare(left.MemberId, right.MemberId);
        }
    }
}
