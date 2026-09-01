using System;
using System.Collections.Generic;
using Game.Continuity.Api;
using Game.GameplayReplication.Api;
using Game.Sessions.Api;

namespace Game.Continuity.Runtime
{
    /// <summary>Runtime-only transport association. Never appears in Continuity.Api credentials or snapshots.</summary>
    public readonly struct RuntimeConnectionHandle : IEquatable<RuntimeConnectionHandle>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public RuntimeConnectionHandle(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Connection handle is required.", nameof(value));
            Value = value;
        }
        public bool Equals(RuntimeConnectionHandle other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is RuntimeConnectionHandle other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
    }

    /// <summary>Composition seam that reauthenticates a new transport to the already-existing durable member.</summary>
    public interface IReconnectTransportAdmission
    {
        bool TryBind(PartyMemberSnapshot member, RuntimeConnectionHandle connection);
    }

    /// <summary>Terminal policy is handed back to owning systems; Continuity does not invent removal or AI policy.</summary>
    public interface IContinuityTerminalPolicySink
    {
        void OnExplicitLeave(PartyMemberId memberId);
        void OnRecoveryExpired(PartyMemberId memberId);
    }

    public sealed class ContinuityCoordinator : IContinuityQuery
    {
        private readonly IPartySessionQuery _sessions;
        private readonly ContinuityPolicy _policy;
        private readonly IReconnectTransportAdmission _admission;
        private readonly IGameplayReplicationClientState _replication;
        private readonly IContinuityTerminalPolicySink _terminalPolicy;
        private readonly Dictionary<PartyMemberId, Entry> _entries = new Dictionary<PartyMemberId, Entry>();
        private ulong _eventSequence;

        public event Action<ContinuityEvent> Changed;

        public ContinuityCoordinator(
            IPartySessionQuery sessions,
            ContinuityPolicy policy,
            IReconnectTransportAdmission admission,
            IGameplayReplicationClientState replication,
            IContinuityTerminalPolicySink terminalPolicy = null)
        {
            _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            _policy = policy;
            _admission = admission ?? throw new ArgumentNullException(nameof(admission));
            _replication = replication ?? throw new ArgumentNullException(nameof(replication));
            _terminalPolicy = terminalPolicy;
        }

        public ReconnectCredential IssueCredential(PartyMemberId memberId, string opaqueToken)
        {
            if (!_sessions.TryGetMember(memberId, out _)) throw new ArgumentException("Unknown session member.", nameof(memberId));
            if (string.IsNullOrWhiteSpace(opaqueToken)) throw new ArgumentException("Reconnect token is required.", nameof(opaqueToken));
            GameSessionId sessionId = _sessions.Snapshot().SessionId;
            _entries[memberId] = new Entry(sessionId, memberId, opaqueToken, RecoveryState.Connected, 0, 0);
            return new ReconnectCredential(sessionId, memberId, opaqueToken);
        }

        public bool ObserveUnexpectedLoss(PartyMemberId memberId, double nowSeconds)
        {
            if (!_entries.TryGetValue(memberId, out Entry entry) || !_sessions.TryGetMember(memberId, out _)) return false;
            if (entry.State == RecoveryState.Left || entry.State == RecoveryState.Expired) return false;
            entry.State = RecoveryState.ConnectionInterrupted;
            entry.InterruptedAt = nowSeconds;
            entry.GraceDeadline = nowSeconds + _policy.GraceSeconds;
            _entries[memberId] = entry;
            Publish(ContinuityEventKind.Interrupted, memberId);
            return true;
        }

        public ReconnectResult BeginReconnect(ReconnectRequest request, RuntimeConnectionHandle newConnection, double nowSeconds)
        {
            ReconnectCredential credential = request.Credential;
            if (!newConnection.IsValid) return new ReconnectResult(ReconnectFailureReason.TransportRejected);
            if (!_entries.TryGetValue(credential.MemberId, out Entry entry)) return new ReconnectResult(ReconnectFailureReason.UnknownMember);
            if (entry.SessionId != credential.SessionId) return new ReconnectResult(ReconnectFailureReason.SessionMismatch);
            if (!string.Equals(entry.Token, credential.Token, StringComparison.Ordinal)) return new ReconnectResult(ReconnectFailureReason.CredentialRejected);
            if (entry.State == RecoveryState.Left || entry.State == RecoveryState.Expired) return new ReconnectResult(ReconnectFailureReason.NotRecoverable);
            if (entry.State != RecoveryState.ConnectionInterrupted) return new ReconnectResult(ReconnectFailureReason.NotRecoverable);
            if (nowSeconds > entry.GraceDeadline)
            {
                ExpireEntry(ref entry);
                _entries[entry.MemberId] = entry;
                return new ReconnectResult(ReconnectFailureReason.GraceExpired);
            }
            if (!_sessions.TryGetMember(entry.MemberId, out PartyMemberSnapshot member)) return new ReconnectResult(ReconnectFailureReason.UnknownMember);

            RecoveryPath path = nowSeconds - entry.InterruptedAt <= _policy.FastRepairWindowSeconds
                ? RecoveryPath.FastRepair
                : RecoveryPath.FullResynchronization;
            entry.State = RecoveryState.Reconnecting;
            _entries[entry.MemberId] = entry;
            Publish(ContinuityEventKind.ReconnectStarted, entry.MemberId);

            if (!_admission.TryBind(member, newConnection))
            {
                entry.State = RecoveryState.ConnectionInterrupted;
                _entries[entry.MemberId] = entry;
                return new ReconnectResult(ReconnectFailureReason.TransportRejected);
            }

            GameplayRecoveryMode recoveryMode = path == RecoveryPath.FastRepair
                ? GameplayRecoveryMode.Repair
                : GameplayRecoveryMode.FullSnapshot;
            _replication.RequestRecovery(entry.MemberId, recoveryMode);

            if (path == RecoveryPath.FullResynchronization)
            {
                entry.State = RecoveryState.Resynchronizing;
                _entries[entry.MemberId] = entry;
                Publish(ContinuityEventKind.ResynchronizationStarted, entry.MemberId);
            }

            return new ReconnectResult(ReconnectFailureReason.None, path, member);
        }

        /// <summary>Completes continuity only after gameplay replication reports converged current truth.</summary>
        public bool MarkGameplayReady(PartyMemberId memberId)
        {
            if (!_entries.TryGetValue(memberId, out Entry entry)) return false;
            if (entry.State != RecoveryState.Reconnecting && entry.State != RecoveryState.Resynchronizing) return false;
            if (!_replication.TryGetSynchronization(memberId, out GameplaySynchronizationStatus synchronization)) return false;
            if (!synchronization.GameplayReady || !synchronization.Revision.IsValid) return false;
            entry.State = RecoveryState.Recovered;
            _entries[memberId] = entry;
            Publish(ContinuityEventKind.Recovered, memberId);
            return true;
        }

        public bool ExplicitLeave(PartyMemberId memberId)
        {
            if (!_entries.TryGetValue(memberId, out Entry entry)) return false;
            if (entry.State == RecoveryState.Left || entry.State == RecoveryState.Expired) return false;
            entry.State = RecoveryState.Left;
            entry.Token = string.Empty;
            _entries[memberId] = entry;
            _terminalPolicy?.OnExplicitLeave(memberId);
            Publish(ContinuityEventKind.Left, memberId);
            return true;
        }

        public int ExpireInterrupted(double nowSeconds)
        {
            var keys = new List<PartyMemberId>(_entries.Keys);
            int expired = 0;
            for (int i = 0; i < keys.Count; i++)
            {
                Entry entry = _entries[keys[i]];
                if (entry.State != RecoveryState.ConnectionInterrupted || nowSeconds <= entry.GraceDeadline) continue;
                ExpireEntry(ref entry);
                _entries[keys[i]] = entry;
                expired++;
            }
            return expired;
        }

        public bool TryGetRecovery(PartyMemberId memberId, out RecoverySnapshot recovery)
        {
            if (_entries.TryGetValue(memberId, out Entry entry))
            {
                recovery = new RecoverySnapshot(memberId, entry.State, entry.GraceDeadline);
                return true;
            }
            recovery = default;
            return false;
        }

        private void ExpireEntry(ref Entry entry)
        {
            entry.State = RecoveryState.Expired;
            entry.Token = string.Empty;
            _terminalPolicy?.OnRecoveryExpired(entry.MemberId);
            Publish(ContinuityEventKind.Expired, entry.MemberId);
        }

        private void Publish(ContinuityEventKind kind, PartyMemberId memberId) => Changed?.Invoke(new ContinuityEvent(++_eventSequence, kind, memberId));

        private struct Entry
        {
            public GameSessionId SessionId;
            public PartyMemberId MemberId;
            public string Token;
            public RecoveryState State;
            public double InterruptedAt;
            public double GraceDeadline;

            public Entry(GameSessionId sessionId, PartyMemberId memberId, string token, RecoveryState state, double interruptedAt, double graceDeadline)
            {
                SessionId = sessionId;
                MemberId = memberId;
                Token = token;
                State = state;
                InterruptedAt = interruptedAt;
                GraceDeadline = graceDeadline;
            }
        }
    }
}
