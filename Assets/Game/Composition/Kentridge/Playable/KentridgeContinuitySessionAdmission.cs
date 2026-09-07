using System;
using System.Collections.Generic;
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

        public void HandleSessionAdmission(uint connectionId, ReadOnlySpan<byte> payload)
        {
            if (_disposed || _server == null || _continuity == null || connectionId == 0) return;

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

        private static double DefaultNowSeconds() => Environment.TickCount64 * 0.001d;

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
