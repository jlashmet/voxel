using System;
using System.Collections.Generic;
using System.Diagnostics;
using Game.Continuity.Api;
using Game.Continuity.Runtime;
using Game.GameplayReplication.Api;
using Game.Sessions.Api;
using Game.Sessions.Runtime;
using VoxelEngine.Net.Api;
using VoxelEngine.Net.Runtime.Protocol;
using VoxelEngine.Net.Runtime.Server;

namespace Game.Composition.Kentridge.Playable
{
    /// <summary>
    /// Production Continuity wrapper around ordinary Kentridge admission. Initial joins delegate to
    /// the existing admission consumer unchanged. A known applicant whose durable member is interrupted
    /// must reconnect through Continuity before a new transport can bind that member.
    /// </summary>
    public sealed class KentridgeContinuitySessionAdmission :
        IAuthoritativeSessionAdmissionConsumer,
        IContinuityTerminalPolicySink,
        IDisposable
    {
        private static readonly ContinuityPolicy DefaultPolicy = new ContinuityPolicy(60d, 10d);

        private readonly PartySession _session;
        private readonly KentridgeAuthoritativeSessionAdmission _inner;
        private readonly Func<GameplayRevision> _gameplayRevision;
        private readonly Func<double> _nowSeconds;
        private readonly Dictionary<string, PartyMemberId> _memberByApplicant =
            new Dictionary<string, PartyMemberId>(StringComparer.Ordinal);
        private readonly Dictionary<string, ReconnectCredential> _credentialByApplicant =
            new Dictionary<string, ReconnectCredential>(StringComparer.Ordinal);
        private readonly Dictionary<PartyMemberId, string> _applicantByMember =
            new Dictionary<PartyMemberId, string>();
        private readonly Dictionary<uint, PartyMemberId> _memberByConnection =
            new Dictionary<uint, PartyMemberId>();

        private AuthoritativeServerSession _server;
        private SessionNetworkAdmissionAdapter _network;
        private KentridgeContinuityReplicationState _replication;
        private ContinuityCoordinator _continuity;
        private bool _disposed;

        public KentridgeContinuitySessionAdmission(
            PartySession session,
            KentridgeAuthoritativeSessionAdmission inner,
            Func<GameplayRevision> gameplayRevision,
            Func<double> nowSeconds = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _gameplayRevision = gameplayRevision ?? throw new ArgumentNullException(nameof(gameplayRevision));
            _nowSeconds = nowSeconds ?? DefaultNowSeconds;
        }

        public IContinuityQuery Continuity => _continuity;

        public void BindAuthority(AuthoritativeServerSession server)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KentridgeContinuitySessionAdmission));
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (_server != null) throw new InvalidOperationException("Authority is already bound.");

            _server = server;
            _inner.BindAuthority(server);
            _network = new SessionNetworkAdmissionAdapter(_session, server);
            _replication = new KentridgeContinuityReplicationState(_gameplayRevision);
            _continuity = new ContinuityCoordinator(
                _session,
                DefaultPolicy,
                new KentridgeContinuityTransportAdmission(_network),
                _replication,
                this);
            _server.ConnectionClosed += OnConnectionClosed;
            _session.Changed += OnSessionChanged;
        }

        /// <summary>
        /// Rebuilds Continuity's transient reconnect credentials from a just-restored durable Sessions
        /// roster. Credentials are intentionally minted fresh in the new authority process. Members
        /// restored without a live connection begin in the ordinary interrupted state so a new UTP
        /// connection must authenticate and pass current-state recovery before becoming ready.
        /// </summary>
        public void RefreshRestoredRoster()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KentridgeContinuitySessionAdmission));
            if (_continuity == null) throw new InvalidOperationException("Authority must be bound before restoring continuity.");

            PartySessionStateCapture state = _session.CaptureState();
            var validApplicants = new HashSet<string>(StringComparer.Ordinal);
            var validMembers = new HashSet<PartyMemberId>();
            for (int i = 0; i < state.Members.Count; i++)
            {
                PartyMemberStateCapture member = state.Members[i];
                validApplicants.Add(member.ApplicantKey);
                validMembers.Add(member.MemberId);
                _memberByApplicant[member.ApplicantKey] = member.MemberId;
                _applicantByMember[member.MemberId] = member.ApplicantKey;
                if (!_credentialByApplicant.ContainsKey(member.ApplicantKey))
                {
                    _credentialByApplicant[member.ApplicantKey] = _continuity.IssueCredential(
                        member.MemberId,
                        Guid.NewGuid().ToString("N"));
                }

                if (_session.TryGetMember(member.MemberId, out PartyMemberSnapshot current) &&
                    current.Presence == PartyPresenceState.Disconnected &&
                    _continuity.TryGetRecovery(member.MemberId, out RecoverySnapshot recovery) &&
                    recovery.State == RecoveryState.Connected)
                {
                    _continuity.ObserveUnexpectedLoss(member.MemberId, _nowSeconds());
                }
            }

            RemoveStaleApplicants(validApplicants, validMembers);
        }

        public void HandleSessionAdmission(uint connectionId, ReadOnlySpan<byte> payload)
        {
            if (_disposed || _server == null || _continuity == null || connectionId == 0) return;

            // A fresh authority may have restored durable Sessions state immediately before this
            // packet. Refresh here so reconnect admission cannot race the first authority update tick.
            RefreshRestoredRoster();

            if (KentridgeSessionAdmissionCodec.TryDecodeJoin(payload, out JoinRequest request) &&
                _memberByApplicant.TryGetValue(request.ApplicantKey, out PartyMemberId prior) &&
                _credentialByApplicant.TryGetValue(request.ApplicantKey, out ReconnectCredential credential) &&
                _session.TryGetMember(prior, out PartyMemberSnapshot existing) &&
                existing.Presence == PartyPresenceState.Disconnected)
            {
                ReconnectResult reconnect = _continuity.BeginReconnect(
                    new ReconnectRequest(credential),
                    KentridgeContinuityTransportAdmission.FromConnectionId(connectionId),
                    _nowSeconds());
                if (!reconnect.Accepted)
                {
                    Send(connectionId,
                        SessionFormationResult.Reject(SessionFormationFailure.Rejected,
                            "Continuity reconnect rejected: " + reconnect.FailureReason),
                        0);
                    return;
                }

                _memberByConnection[connectionId] = prior;
                ushort networkPlayerId = checked((ushort)(existing.Slot.Value + 1));
                Send(connectionId, SessionFormationResult.Success(request.SessionId, prior), networkPlayerId);
                return;
            }

            _inner.HandleSessionAdmission(connectionId, payload);
            if (!KentridgeSessionAdmissionCodec.TryDecodeJoin(payload, out JoinRequest admittedRequest)) return;
            TransportConnectionHandle handle = SessionNetworkAdmissionAdapter.FromConnectionId(connectionId);
            if (!_session.TryResolveConnection(handle, out PartyMemberId memberId) ||
                !_session.TryGetMember(memberId, out _))
                return;

            TrackInitialAdmission(admittedRequest.ApplicantKey, memberId, connectionId);
        }

        public void TickContinuity()
        {
            if (_disposed || _continuity == null) return;
            _continuity.ExpireInterrupted(_nowSeconds());
            var members = new List<PartyMemberId>(_applicantByMember.Keys);
            for (int i = 0; i < members.Count; i++)
                _continuity.MarkGameplayReady(members[i]);
        }

        public void OnExplicitLeave(PartyMemberId memberId)
        {
            _session.Remove(memberId);
        }

        public void OnRecoveryExpired(PartyMemberId memberId)
        {
            _session.Remove(memberId);
        }

        private void TrackInitialAdmission(string applicant, PartyMemberId memberId, uint connectionId)
        {
            _memberByApplicant[applicant] = memberId;
            _applicantByMember[memberId] = applicant;
            _memberByConnection[connectionId] = memberId;
            if (_credentialByApplicant.ContainsKey(applicant)) return;
            _credentialByApplicant[applicant] = _continuity.IssueCredential(
                memberId,
                Guid.NewGuid().ToString("N"));
        }

        private void RemoveStaleApplicants(HashSet<string> validApplicants, HashSet<PartyMemberId> validMembers)
        {
            var applicants = new List<string>(_memberByApplicant.Keys);
            for (int i = 0; i < applicants.Count; i++)
            {
                string applicant = applicants[i];
                if (validApplicants.Contains(applicant)) continue;
                _memberByApplicant.Remove(applicant);
                _credentialByApplicant.Remove(applicant);
            }

            var members = new List<PartyMemberId>(_applicantByMember.Keys);
            for (int i = 0; i < members.Count; i++)
            {
                if (!validMembers.Contains(members[i])) _applicantByMember.Remove(members[i]);
            }
        }

        private void OnConnectionClosed(uint connectionId)
        {
            if (_disposed || _continuity == null ||
                !_memberByConnection.TryGetValue(connectionId, out PartyMemberId memberId))
                return;
            _memberByConnection.Remove(connectionId);
            _continuity.ObserveUnexpectedLoss(memberId, _nowSeconds());
        }

        private void OnSessionChanged(SessionLifecycleEvent evt)
        {
            if (_disposed || evt.Kind != SessionLifecycleEventKind.MemberRemoved || _continuity == null) return;
            _continuity.ExplicitLeave(evt.MemberId);
            if (!_applicantByMember.TryGetValue(evt.MemberId, out string applicant)) return;
            _applicantByMember.Remove(evt.MemberId);
            _memberByApplicant.Remove(applicant);
            _credentialByApplicant.Remove(applicant);
        }

        private void Send(uint connectionId, SessionFormationResult result, ushort networkPlayerId)
        {
            Span<byte> reply = stackalloc byte[SessionAdmissionPacket.MaxPayloadBytes];
            if (KentridgeSessionAdmissionCodec.TryEncodeReply(result, networkPlayerId, reply, out int written))
                _server.TrySendSessionAdmissionReply(connectionId, reply.Slice(0, written));
        }

        private static double DefaultNowSeconds() =>
            Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_server != null) _server.ConnectionClosed -= OnConnectionClosed;
            _session.Changed -= OnSessionChanged;
            _inner.Dispose();
            _server = null;
            _network = null;
            _replication = null;
            _continuity = null;
            _memberByConnection.Clear();
            _memberByApplicant.Clear();
            _applicantByMember.Clear();
            _credentialByApplicant.Clear();
        }
    }
}
