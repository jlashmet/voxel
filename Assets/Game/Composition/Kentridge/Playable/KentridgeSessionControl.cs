using System;
using Game.SessionPresentation.Api;
using Game.Sessions.Api;
using Game.Sessions.Runtime;
using VoxelEngine.Net.Runtime.Client;
using VoxelEngine.Net.Runtime.Server;

namespace Game.Composition.Kentridge.Playable
{
    /// <summary>
    /// Kentridge-owned semantic session control carried inside Net's existing bounded Sessions EVENT payload.
    /// The payload never contains a durable member id: authority resolves identity from the authenticated
    /// transport connection before applying a command.
    /// </summary>
    public static class KentridgeSessionControlCodec
    {
        private const byte Version = 1;
        private const byte LeaveCommand = 2; // Admission join uses 0/1 in this byte; 2 is intentionally disjoint.
        public const int LeavePayloadBytes = 2;

        public static bool TryEncodeLeave(Span<byte> destination, out int written)
        {
            written = 0;
            if (destination.Length < LeavePayloadBytes) return false;
            destination[0] = Version;
            destination[1] = LeaveCommand;
            written = LeavePayloadBytes;
            return true;
        }

        public static bool IsLeave(ReadOnlySpan<byte> payload) =>
            payload.Length == LeavePayloadBytes && payload[0] == Version && payload[1] == LeaveCommand;
    }

    /// <summary>
    /// Authority-side Sessions control wrapper. Non-control payloads continue through the ordinary
    /// admission consumer. Explicit Leave removes durable membership before closing transport; an
    /// ordinary transport close never passes this path and therefore remains reconnectable.
    /// </summary>
    public sealed class KentridgeAuthoritativeSessionControl : IAuthoritativeSessionAdmissionConsumer
    {
        private readonly IAuthoritativeSessionAdmissionConsumer _admission;
        private readonly PartySession _session;
        private readonly PartySessionApplication _application;
        private AuthoritativeServerSession _server;

        public KentridgeAuthoritativeSessionControl(
            IAuthoritativeSessionAdmissionConsumer admission,
            PartySession session,
            PartySessionApplication application)
        {
            _admission = admission ?? throw new ArgumentNullException(nameof(admission));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _application = application ?? throw new ArgumentNullException(nameof(application));
        }

        public void BindAuthority(AuthoritativeServerSession server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (_server != null) throw new InvalidOperationException("Authority is already bound.");
            _server = server;
        }

        public void HandleSessionAdmission(uint connectionId, ReadOnlySpan<byte> payload)
        {
            if (!KentridgeSessionControlCodec.IsLeave(payload))
            {
                _admission.HandleSessionAdmission(connectionId, payload);
                return;
            }

            if (_server == null || connectionId == 0 ||
                !_session.TryResolveConnection(
                    SessionNetworkAdmissionAdapter.FromConnectionId(connectionId), out PartyMemberId memberId))
                return;

            PartySessionCommandResult leave = _application.Leave(memberId);
            if (leave.Accepted)
                _server.Disconnect(connectionId);
        }
    }

    /// <summary>
    /// Client-side Application intent router. Kentridge auto-readies an authenticated member and only
    /// the authority leader may Start, so the only valid remote UI command is explicit Leave.
    /// Successful return means the reliable EVENT command was queued/flushed, not that the client
    /// manufactured authority state locally.
    /// </summary>
    public sealed class KentridgeRemoteSessionIntentRouter : ISessionPresentationIntentRouter
    {
        private readonly PartyMemberId _localMemberId;
        private readonly Func<ClientNetworkRuntime> _activeClient;

        public KentridgeRemoteSessionIntentRouter(
            PartyMemberId localMemberId,
            Func<ClientNetworkRuntime> activeClient)
        {
            if (!localMemberId.IsValid) throw new ArgumentException("Local member id is required.", nameof(localMemberId));
            _localMemberId = localMemberId;
            _activeClient = activeClient ?? throw new ArgumentNullException(nameof(activeClient));
        }

        public PartySessionCommandResult Request(SessionPresentationIntent intent)
        {
            if (intent.MemberId != _localMemberId)
                return PartySessionCommandResult.Reject(PartySessionCommandFailure.UnknownMember);
            if (intent.Kind != SessionPresentationIntentKind.Leave)
                return PartySessionCommandResult.Reject(PartySessionCommandFailure.InvalidRequest);

            ClientNetworkRuntime client = _activeClient();
            if (client == null || !client.IsConnected)
                return PartySessionCommandResult.Reject(PartySessionCommandFailure.InvalidRequest);

            Span<byte> payload = stackalloc byte[KentridgeSessionControlCodec.LeavePayloadBytes];
            if (!KentridgeSessionControlCodec.TryEncodeLeave(payload, out int written) ||
                !client.TrySendSessionAdmission(payload.Slice(0, written)))
                return PartySessionCommandResult.Reject(PartySessionCommandFailure.InvalidRequest);

            client.FlushSends();
            return PartySessionCommandResult.Accept();
        }
    }
}
