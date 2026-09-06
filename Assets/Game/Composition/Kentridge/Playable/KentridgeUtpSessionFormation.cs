using System;
using System.Collections.Generic;
using System.Text;
using Game.Characters.Api;
using Game.Sessions.Api;
using Game.Sessions.Runtime;
using Unity.Networking.Transport;
using VoxelEngine.Net.Api;
using VoxelEngine.Net.Runtime.Client;
using VoxelEngine.Net.Runtime.Protocol;
using VoxelEngine.Net.Runtime.Server;

namespace Game.Composition.Kentridge.Playable
{
    /// <summary>
    /// Kentridge's production Sessions payload carried inside Net's bounded C/S_SessionAdmission EVENT frame.
    /// Transport connection ids never appear in the payload; Net supplies sender attribution separately.
    /// </summary>
    public static class KentridgeSessionAdmissionCodec
    {
        private const byte Version = 1;
        private const byte ReplySuccess = 1;
        private const int MaxFieldBytes = 256;

        public static bool TryEncodeJoin(in JoinRequest request, Span<byte> destination, out int written)
        {
            written = 0;
            if (!request.SessionId.IsValid || string.IsNullOrWhiteSpace(request.ApplicantKey) ||
                string.IsNullOrWhiteSpace(request.ProtocolVersion) || string.IsNullOrWhiteSpace(request.ContentCompatibilityKey))
                return false;
            int offset = 0;
            if (!TryWriteByte(destination, ref offset, Version) ||
                !TryWriteByte(destination, ref offset, request.IsJoinInProgress ? (byte)1 : (byte)0) ||
                !TryWriteString(destination, ref offset, request.SessionId.Value) ||
                !TryWriteString(destination, ref offset, request.ApplicantKey) ||
                !TryWriteString(destination, ref offset, request.ProtocolVersion) ||
                !TryWriteString(destination, ref offset, request.ContentCompatibilityKey))
                return false;
            written = offset;
            return written <= SessionAdmissionPacket.MaxPayloadBytes;
        }

        public static bool TryDecodeJoin(ReadOnlySpan<byte> payload, out JoinRequest request)
        {
            request = default;
            int offset = 0;
            if (!TryReadByte(payload, ref offset, out byte version) || version != Version ||
                !TryReadByte(payload, ref offset, out byte flags) || flags > 1 ||
                !TryReadString(payload, ref offset, out string session) ||
                !TryReadString(payload, ref offset, out string applicant) ||
                !TryReadString(payload, ref offset, out string protocol) ||
                !TryReadString(payload, ref offset, out string content) || offset != payload.Length)
                return false;
            try
            {
                request = new JoinRequest(new GameSessionId(session), applicant, protocol, content, flags != 0);
                return true;
            }
            catch (ArgumentException) { return false; }
        }

        public static bool TryEncodeReply(SessionFormationResult result, ushort networkPlayerId,
            Span<byte> destination, out int written)
        {
            written = 0;
            int offset = 0;
            if (!TryWriteByte(destination, ref offset, Version) ||
                !TryWriteByte(destination, ref offset, result.Succeeded ? ReplySuccess : (byte)0) ||
                !TryWriteByte(destination, ref offset, (byte)result.Failure))
                return false;
            if (result.Succeeded)
            {
                if (networkPlayerId == 0 || !result.SessionId.IsValid || !result.LocalMemberId.IsValid ||
                    !TryWriteUInt16(destination, ref offset, networkPlayerId) ||
                    !TryWriteString(destination, ref offset, result.SessionId.Value) ||
                    !TryWriteString(destination, ref offset, result.LocalMemberId.Value))
                    return false;
            }
            else if (!TryWriteString(destination, ref offset, SanitizeDetail(result.Detail)))
            {
                return false;
            }
            written = offset;
            return written <= SessionAdmissionPacket.MaxPayloadBytes;
        }

        public static bool TryDecodeReply(ReadOnlySpan<byte> payload, out SessionFormationResult result,
            out ushort networkPlayerId)
        {
            result = default;
            networkPlayerId = 0;
            int offset = 0;
            if (!TryReadByte(payload, ref offset, out byte version) || version != Version ||
                !TryReadByte(payload, ref offset, out byte success) || success > 1 ||
                !TryReadByte(payload, ref offset, out byte rawFailure) ||
                rawFailure > (byte)SessionFormationFailure.Rejected)
                return false;
            SessionFormationFailure failure = (SessionFormationFailure)rawFailure;
            try
            {
                if (success == ReplySuccess)
                {
                    if (failure != SessionFormationFailure.None ||
                        !TryReadUInt16(payload, ref offset, out networkPlayerId) || networkPlayerId == 0 ||
                        !TryReadString(payload, ref offset, out string session) ||
                        !TryReadString(payload, ref offset, out string member) || offset != payload.Length)
                        return false;
                    result = SessionFormationResult.Success(new GameSessionId(session), new PartyMemberId(member));
                    return true;
                }
                if (failure == SessionFormationFailure.None ||
                    !TryReadString(payload, ref offset, out string detail) || offset != payload.Length)
                    return false;
                result = SessionFormationResult.Reject(failure, detail);
                return true;
            }
            catch (ArgumentException) { return false; }
        }

        private static SessionFormationFailure Map(JoinFailureReason failure)
        {
            switch (failure)
            {
                case JoinFailureReason.ProtocolVersionMismatch: return SessionFormationFailure.ProtocolMismatch;
                case JoinFailureReason.ContentMismatch: return SessionFormationFailure.ContentMismatch;
                case JoinFailureReason.SessionFull: return SessionFormationFailure.SessionFull;
                case JoinFailureReason.SessionMismatch: return SessionFormationFailure.SessionUnavailable;
                case JoinFailureReason.InvalidRequest: return SessionFormationFailure.InvalidRequest;
                default: return SessionFormationFailure.Rejected;
            }
        }

        internal static SessionFormationResult Reject(JoinFailureReason failure) =>
            SessionFormationResult.Reject(Map(failure), failure.ToString());

        private static string SanitizeDetail(string detail)
        {
            if (string.IsNullOrWhiteSpace(detail)) return "Rejected";
            string trimmed = detail.Trim();
            return trimmed.Length <= 96 ? trimmed : trimmed.Substring(0, 96);
        }

        private static bool TryWriteString(Span<byte> destination, ref int offset, string value)
        {
            if (value == null) return false;
            int count = Encoding.UTF8.GetByteCount(value);
            if (count < 1 || count > MaxFieldBytes || destination.Length - offset < count + 2) return false;
            destination[offset++] = (byte)count;
            destination[offset++] = (byte)(count >> 8);
            Encoding.UTF8.GetBytes(value.AsSpan(), destination.Slice(offset, count));
            offset += count;
            return true;
        }

        private static bool TryReadString(ReadOnlySpan<byte> source, ref int offset, out string value)
        {
            value = string.Empty;
            if (source.Length - offset < 2) return false;
            int count = source[offset] | (source[offset + 1] << 8);
            offset += 2;
            if (count < 1 || count > MaxFieldBytes || source.Length - offset < count) return false;
            try { value = Encoding.UTF8.GetString(source.Slice(offset, count)); }
            catch (DecoderFallbackException) { return false; }
            offset += count;
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool TryWriteByte(Span<byte> destination, ref int offset, byte value)
        {
            if (offset >= destination.Length) return false;
            destination[offset++] = value;
            return true;
        }

        private static bool TryReadByte(ReadOnlySpan<byte> source, ref int offset, out byte value)
        {
            value = 0;
            if (offset >= source.Length) return false;
            value = source[offset++];
            return true;
        }

        private static bool TryWriteUInt16(Span<byte> destination, ref int offset, ushort value)
        {
            if (destination.Length - offset < 2) return false;
            destination[offset++] = (byte)value;
            destination[offset++] = (byte)(value >> 8);
            return true;
        }

        private static bool TryReadUInt16(ReadOnlySpan<byte> source, ref int offset, out ushort value)
        {
            value = 0;
            if (source.Length - offset < 2) return false;
            value = (ushort)(source[offset] | (source[offset + 1] << 8));
            offset += 2;
            return true;
        }
    }

    /// <summary>
    /// Authority-side Kentridge composition of Sessions policy onto the canonical Net authority.
    /// Requests are consumed only from AuthoritativeServerSession's fixed-tick admission drain.
    /// </summary>
    public sealed class KentridgeAuthoritativeSessionAdmission : IAuthoritativeSessionAdmissionConsumer, IDisposable
    {
        private readonly PartySession _session;
        private readonly Dictionary<string, PartyMemberId> _admittedApplicants =
            new Dictionary<string, PartyMemberId>(StringComparer.Ordinal);
        private AuthoritativeServerSession _server;
        private SessionNetworkAdmissionAdapter _network;
        private bool _disposed;

        public KentridgeAuthoritativeSessionAdmission(PartySession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public PartySession Session => _session;

        public void BindAuthority(AuthoritativeServerSession server)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KentridgeAuthoritativeSessionAdmission));
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (_server != null) throw new InvalidOperationException("Authority is already bound.");
            _server = server;
            _network = new SessionNetworkAdmissionAdapter(_session, server);
            _server.ConnectionClosed += OnConnectionClosed;
        }

        public void HandleSessionAdmission(uint connectionId, ReadOnlySpan<byte> payload)
        {
            if (_disposed || _server == null || _network == null || connectionId == 0) return;
            Span<byte> reply = stackalloc byte[SessionAdmissionPacket.MaxPayloadBytes];
            SessionFormationResult result;
            ushort networkPlayerId = 0;

            if (!KentridgeSessionAdmissionCodec.TryDecodeJoin(payload, out JoinRequest request))
            {
                result = SessionFormationResult.Reject(SessionFormationFailure.InvalidRequest, "Invalid admission payload");
            }
            else
            {
                PartyMemberId memberId = default;
                PartyMemberSnapshot member = default;
                bool newJoin = false;
                if (_admittedApplicants.TryGetValue(request.ApplicantKey, out PartyMemberId prior) &&
                    _session.TryGetMember(prior, out member))
                {
                    memberId = prior;
                }
                else
                {
                    _admittedApplicants.Remove(request.ApplicantKey);
                    JoinResult join = _session.Join(request);
                    if (!join.Accepted)
                    {
                        result = KentridgeSessionAdmissionCodec.Reject(join.FailureReason);
                        Send(connectionId, result, 0, reply);
                        return;
                    }
                    member = join.Member;
                    memberId = member.MemberId;
                    _admittedApplicants[request.ApplicantKey] = memberId;
                    newJoin = true;
                    if (!member.CharacterId.IsValid)
                    {
                        var characterId = new CharacterId("kentridge-player-" + (member.Slot.Value + 1));
                        if (!_session.BindCharacter(memberId, characterId))
                        {
                            _session.Remove(memberId);
                            _admittedApplicants.Remove(request.ApplicantKey);
                            result = SessionFormationResult.Reject(SessionFormationFailure.Rejected, "Character binding rejected");
                            Send(connectionId, result, 0, reply);
                            return;
                        }
                        _session.TryGetMember(memberId, out member);
                    }
                }

                networkPlayerId = checked((ushort)(member.Slot.Value + 1));
                if (!_network.Authenticate(memberId, connectionId, new NetworkSpawnPosition(0, 0, 0), 8, true))
                {
                    if (newJoin)
                    {
                        _session.Remove(memberId);
                        _admittedApplicants.Remove(request.ApplicantKey);
                    }
                    result = SessionFormationResult.Reject(SessionFormationFailure.Rejected, "Network admission rejected");
                }
                else
                {
                    result = SessionFormationResult.Success(request.SessionId, memberId);
                }
            }
            Send(connectionId, result, networkPlayerId, reply);
        }

        private void Send(uint connectionId, SessionFormationResult result, ushort networkPlayerId, Span<byte> buffer)
        {
            if (KentridgeSessionAdmissionCodec.TryEncodeReply(result, networkPlayerId, buffer, out int written))
                _server.TrySendSessionAdmissionReply(connectionId, buffer.Slice(0, written));
        }

        private void OnConnectionClosed(uint connectionId)
        {
            if (!_disposed && _network != null) _network.Disconnect(connectionId);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_server != null) _server.ConnectionClosed -= OnConnectionClosed;
            _server = null;
            _network = null;
            _admittedApplicants.Clear();
        }
    }

    /// <summary>
    /// Production nonblocking UTP formation provider used by Application. It owns one local client
    /// connection while an attempt is pending; accepted transport remains alive for gameplay composition.
    /// The optional authority pump is used only by the host process to advance the canonical fixed tick.
    /// </summary>
    public sealed class KentridgeUtpSessionFormationService : IAsyncSessionFormationService, IDisposable
    {
        private readonly Func<IServerSessionAdmissionHandler, ClientNetworkRuntime> _clientFactory;
        private readonly Func<NetworkEndpoint> _endpoint;
        private readonly Action<HostSessionRequest> _prepareHostAuthority;
        private readonly Action _pumpAuthorityFixedTick;
        private Operation _pending;
        private bool _disposed;

        public KentridgeUtpSessionFormationService(
            Func<IServerSessionAdmissionHandler, ClientNetworkRuntime> clientFactory,
            Func<NetworkEndpoint> endpoint,
            Action<HostSessionRequest> prepareHostAuthority = null,
            Action pumpAuthorityFixedTick = null)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            _prepareHostAuthority = prepareHostAuthority;
            _pumpAuthorityFixedTick = pumpAuthorityFixedTick;
        }

        public ClientNetworkRuntime ActiveClient { get; private set; }
        public ushort ActiveNetworkPlayerId { get; private set; }

        public ISessionFormationOperation BeginHost(HostSessionRequest request)
        {
            ThrowIfDisposed();
            if (_prepareHostAuthority == null)
                throw new InvalidOperationException("This provider is not configured to host authority.");
            _prepareHostAuthority(request);
            var join = new JoinRequest(request.SessionId, request.LocalApplicantKey,
                request.Configuration.ProtocolVersion, request.Configuration.ContentCompatibilityKey, false);
            return Begin(join);
        }

        public ISessionFormationOperation BeginJoin(JoinSessionRequest request)
        {
            ThrowIfDisposed();
            return Begin(request.Admission);
        }

        public SessionFormationResult Host(HostSessionRequest request) =>
            SessionFormationResult.Reject(SessionFormationFailure.ProviderUnavailable, "Asynchronous provider requires BeginHost");

        public SessionFormationResult Join(JoinSessionRequest request) =>
            SessionFormationResult.Reject(SessionFormationFailure.ProviderUnavailable, "Asynchronous provider requires BeginJoin");

        private ISessionFormationOperation Begin(JoinRequest request)
        {
            if (_pending != null) throw new InvalidOperationException("A session admission attempt is already active.");
            Span<byte> encoded = stackalloc byte[SessionAdmissionPacket.MaxPayloadBytes];
            if (!KentridgeSessionAdmissionCodec.TryEncodeJoin(in request, encoded, out int written))
                throw new ArgumentException("Admission request exceeds the production EVENT payload contract.", nameof(request));
            var inbox = new ReplyInbox();
            ClientNetworkRuntime client = _clientFactory(inbox)
                ?? throw new InvalidOperationException("Client factory returned no production network runtime.");
            var operation = new Operation(this, client, inbox, encoded.Slice(0, written).ToArray(), request.SessionId,
                _endpoint, _pumpAuthorityFixedTick);
            _pending = operation;
            return operation;
        }

        private void Complete(Operation operation, ClientNetworkRuntime client, ushort networkPlayerId)
        {
            if (!ReferenceEquals(_pending, operation)) return;
            _pending = null;
            ActiveClient?.Dispose();
            ActiveClient = client;
            ActiveNetworkPlayerId = networkPlayerId;
        }

        private void Abandon(Operation operation)
        {
            if (ReferenceEquals(_pending, operation)) _pending = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _pending?.Cancel();
            _pending = null;
            ActiveClient?.Dispose();
            ActiveClient = null;
            ActiveNetworkPlayerId = 0;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KentridgeUtpSessionFormationService));
        }

        private sealed class ReplyInbox : IServerSessionAdmissionHandler
        {
            private byte[] _reply;
            public bool TryEnqueueSessionAdmissionReply(ReadOnlySpan<byte> payload)
            {
                if (_reply != null || payload.Length < 1 || payload.Length > SessionAdmissionPacket.MaxPayloadBytes) return false;
                _reply = payload.ToArray();
                return true;
            }
            public bool TryTake(out byte[] reply)
            {
                reply = _reply;
                _reply = null;
                return reply != null;
            }
        }

        private sealed class Operation : ISessionFormationOperation
        {
            private readonly KentridgeUtpSessionFormationService _owner;
            private ClientNetworkRuntime _client;
            private readonly ReplyInbox _inbox;
            private readonly byte[] _request;
            private readonly GameSessionId _expectedSession;
            private readonly Func<NetworkEndpoint> _endpoint;
            private readonly Action _pumpAuthority;
            private bool _sent;
            private bool _terminal;
            private bool _cancelled;
            private SessionFormationResult _result;

            public Operation(KentridgeUtpSessionFormationService owner, ClientNetworkRuntime client, ReplyInbox inbox,
                byte[] request, GameSessionId expectedSession, Func<NetworkEndpoint> endpoint, Action pumpAuthority)
            {
                _owner = owner;
                _client = client;
                _inbox = inbox;
                _request = request;
                _expectedSession = expectedSession;
                _endpoint = endpoint;
                _pumpAuthority = pumpAuthority;
                NetworkEndpoint target = _endpoint();
                if (target.Port == 0 || !_client.Connect(target))
                {
                    _terminal = true;
                    _result = SessionFormationResult.Reject(SessionFormationFailure.ProviderUnavailable, "Unable to connect to session authority");
                }
            }

            public bool TryGetResult(out SessionFormationResult result)
            {
                result = default;
                if (_cancelled) return false;
                if (_terminal)
                {
                    result = _result;
                    return true;
                }
                _pumpAuthority?.Invoke();
                _client.PumpTransport();
                if (_client.IsConnected && !_sent)
                {
                    if (!_client.TrySendSessionAdmission(_request))
                    {
                        FinishRejected(SessionFormationFailure.ProviderUnavailable, "Unable to send session admission");
                    }
                    else
                    {
                        _client.FlushSends();
                        _sent = true;
                    }
                }
                _pumpAuthority?.Invoke();
                _client.PumpTransport();
                if (!_inbox.TryTake(out byte[] reply)) return false;
                if (!KentridgeSessionAdmissionCodec.TryDecodeReply(reply, out SessionFormationResult decoded, out ushort networkPlayerId) ||
                    (decoded.Succeeded && decoded.SessionId != _expectedSession))
                {
                    FinishRejected(SessionFormationFailure.Rejected, "Invalid authority admission reply");
                }
                else
                {
                    _result = decoded;
                    _terminal = true;
                    if (decoded.Succeeded)
                    {
                        _owner.Complete(this, _client, networkPlayerId);
                        _client = null;
                    }
                }
                result = _result;
                return true;
            }

            public void Cancel()
            {
                if (_cancelled) return;
                _cancelled = true;
                _owner.Abandon(this);
                if (_client != null)
                {
                    if (_client.IsConnected) _client.Disconnect();
                    _client.Dispose();
                    _client = null;
                }
            }

            private void FinishRejected(SessionFormationFailure failure, string detail)
            {
                _result = SessionFormationResult.Reject(failure, detail);
                _terminal = true;
                _owner.Abandon(this);
                if (_client != null)
                {
                    if (_client.IsConnected) _client.Disconnect();
                    _client.Dispose();
                    _client = null;
                }
            }
        }
    }
}
