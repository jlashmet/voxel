using System;
using Unity.Mathematics;
using Unity.Networking.Transport;
using VoxelEngine.Edits.Api;
using VoxelEngine.Edits.Runtime;
using VoxelEngine.Net.Runtime.Client;
using VoxelEngine.Net.Runtime.Protocol;
using VoxelEngine.Net.Runtime.Server;

namespace VoxelEngine.Net.Validation
{
    /// <summary>
    /// Bounded transport-only orchestration shared by the Net player scene and its EditMode test.
    /// Real production runtimes own connections, framing, dispatch and teardown. Synthetic packet
    /// inputs and copied observations prove delivery only; this is not a Sessions provider, gameplay
    /// authority, admission decision, or a substitute for System25's separate-process gameplay proof.
    /// </summary>
    public sealed class SessionAdmissionTransportProbe : IDisposable
    {
        private readonly AdmissionInbox _requests = new AdmissionInbox();
        private readonly ReplyInbox _repliesA = new ReplyInbox();
        private readonly ReplyInbox _repliesB = new ReplyInbox();
        private readonly byte[] _requestA = MakePayload(SessionAdmissionPacket.MaxPayloadBytes, 0xA1);
        private readonly byte[] _requestB = MakePayload(3, 0xB2);
        private readonly byte[] _reconnectRequest = MakePayload(7, 0xC3);
        private readonly C_AlterationRequest _alteration = new C_AlterationRequest(
            tick: 41, origin: new int3(510, 40, -12), eventKind: AlterationEvent.KindExplosion,
            material: 0, shapeKind: AlterationEvent.KindExplosion, shapeData: 3, seed: 0x12345678, sequence: 9);
        private ServerNetworkRuntime _server;
        private ClientNetworkRuntime _clientA;
        private ClientNetworkRuntime _clientB;
        private int _phase;
        private int _protocolErrors;
        private uint _oldConnection;
        private bool _disposed;

        public event Action<string> Milestone;
        public bool Complete { get; private set; }
        public int ReceivedRequestCount => _requests.Count;
        public bool DistinctSenders { get; private set; }
        public bool IsolatedReplies { get; private set; }
        public bool ReplacedConnection { get; private set; }
        public string PhaseDescription => Complete ? "Complete" : "Transport phase " + _phase;

        public SessionAdmissionTransportProbe()
        {
            try
            {
                _server = new ServerNetworkRuntime(_requests, maxConnections: 2);
                _clientA = new ClientNetworkRuntime(new DeterministicAlterationApplier(), sessionAdmissionHandler: _repliesA);
                _clientB = new ClientNetworkRuntime(new DeterministicAlterationApplier(), sessionAdmissionHandler: _repliesB);
                _server.ProtocolError += _ => _protocolErrors++;
                _clientA.PacketRejected += () => _protocolErrors++;
                _clientB.PacketRejected += () => _protocolErrors++;
                Require(_server.Listen(NetworkEndpoint.LoopbackIpv4.WithPort(0)) == 0, "listen");
                Require(_server.LocalEndpoint.Port != 0, "assigned loopback port");
                Require(_clientA.Connect(_server.LocalEndpoint), "connect A");
                Require(_clientB.Connect(_server.LocalEndpoint), "connect B");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>One bounded pump; callers enforce their own monotonic deadline, never a sleep.</summary>
        public void Step()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SessionAdmissionTransportProbe));
            if (Complete) return;
            _server.PumpTransport();
            _clientA.PumpTransport();
            _clientB.PumpTransport();
            Require(_protocolErrors == 0, "unexpected protocol rejection");

            switch (_phase)
            {
                case 0:
                    if (!_clientA.IsConnected || !_clientB.IsConnected || _server.ConnectionCount != 2) return;
                    Milestone?.Invoke("SESSION_ADMISSION_TRANSPORT connected: clients=2");
                    Require(_clientA.TrySendSessionAdmission(_requestA), "send A");
                    Require(_clientB.TrySendSessionAdmission(_requestB), "send B");
                    _clientA.FlushSends();
                    _clientB.FlushSends();
                    _phase = 1;
                    return;
                case 1:
                    if (_requests.Count != 2) return;
                    _oldConnection = _requests.FindSender(_requestA);
                    uint senderB = _requests.FindSender(_requestB);
                    DistinctSenders = _oldConnection != 0 && senderB != 0 && _oldConnection != senderB;
                    Require(DistinctSenders, "distinct transport-owned senders");
                    // Echo opaque observations outside transport callbacks, not an admission grant.
                    Require(_server.TrySendSessionAdmissionReply(_oldConnection, _requestA), "reply A");
                    Require(_server.TrySendSessionAdmissionReply(senderB, _requestB), "reply B");
                    _server.FlushSends();
                    _phase = 2;
                    return;
                case 2:
                    if (_repliesA.Count == 0 || _repliesB.Count == 0) return;
                    IsolatedReplies = _repliesA.Count == 1 && _repliesB.Count == 1 &&
                        _repliesA.Last.AsSpan().SequenceEqual(_requestA) &&
                        _repliesB.Last.AsSpan().SequenceEqual(_requestB);
                    Require(IsolatedReplies, "replies delivered only to originating clients");
                    RequireNoGameplayBinding();
                    Milestone?.Invoke("SESSION_ADMISSION_TRANSPORT roundtrip: distinctSenders=True isolatedReplies=True");
                    Require(_clientA.TrySendAlterationRequest(in _alteration), "unchanged alteration request");
                    _clientA.FlushSends();
                    _phase = 3;
                    return;
                case 3:
                    if (_requests.AlterationCount == 0) return;
                    Require(_requests.AlterationCount == 1 && _requests.AlterationSender == _oldConnection &&
                        _requests.LastAlteration.Equals(_alteration), "existing EVENT traffic stays intact");
                    Milestone?.Invoke("SESSION_ADMISSION_TRANSPORT existing-traffic: alteration=True");
                    _clientA.Disconnect();
                    Require(!_clientA.TrySendSessionAdmission(_reconnectRequest), "cannot send while disconnected");
                    _phase = 4;
                    return;
                case 4:
                    if (_server.ConnectionCount != 1) return;
                    Require(!_server.TrySendSessionAdmissionReply(_oldConnection, _requestA), "old connection no longer routable");
                    Require(_clientA.Connect(_server.LocalEndpoint), "reconnect A");
                    _phase = 5;
                    return;
                case 5:
                    if (!_clientA.IsConnected || _server.ConnectionCount != 2) return;
                    Require(_clientA.TrySendSessionAdmission(_reconnectRequest), "fresh admission on replacement transport");
                    _clientA.FlushSends();
                    _phase = 6;
                    return;
                case 6:
                    if (_requests.Count != 3) return;
                    uint newConnection = _requests.FindSender(_reconnectRequest);
                    ReplacedConnection = newConnection != 0 && newConnection != _oldConnection;
                    Require(ReplacedConnection, "replacement has a fresh transient connection");
                    Require(_server.TrySendSessionAdmissionReply(newConnection, _reconnectRequest), "replacement reply");
                    _server.FlushSends();
                    _phase = 7;
                    return;
                case 7:
                    if (_repliesA.Count < 2) return;
                    Require(_repliesA.Count == 2 && _repliesB.Count == 1 &&
                        _repliesA.Last.AsSpan().SequenceEqual(_reconnectRequest), "replacement reply isolation");
                    RequireNoGameplayBinding();
                    Milestone?.Invoke("SESSION_ADMISSION_TRANSPORT reconnect: newConnection=True");
                    Complete = true;
                    Milestone?.Invoke("SESSION_ADMISSION_TRANSPORT complete: productionRuntimes=True");
                    return;
                default:
                    throw new InvalidOperationException("Unknown transport probe phase.");
            }
        }

        private void RequireNoGameplayBinding()
        {
            Require(_clientA.LocalPlayerId == 0 && _clientB.LocalPlayerId == 0 &&
                _clientA.PendingAuthoritativeEvents == 0 && _clientB.PendingAuthoritativeEvents == 0 &&
                _clientA.PendingPlayerStateUpdates == 0 && _clientB.PendingPlayerStateUpdates == 0,
                "admission delivery must not bind identity or produce gameplay state");
        }

        private static byte[] MakePayload(int length, byte marker)
        {
            var result = new byte[length];
            for (int i = 0; i < length; i++) result[i] = (byte)(i * 37);
            result[0] = marker;
            return result;
        }

        private static void Require(bool condition, string detail)
        {
            if (!condition) throw new InvalidOperationException("Session admission transport invariant: " + detail);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _clientA?.Dispose(); }
            finally
            {
                try { _clientB?.Dispose(); }
                finally { _server?.Dispose(); }
            }
            _clientA = null;
            _clientB = null;
            _server = null;
        }

        // Copied, fixed-capacity packet observations only. No PartySession or admission policy here.
        private sealed class AdmissionInbox : IClientEventCommandHandler, IClientSessionAdmissionHandler
        {
            private readonly byte[][] _payloads = new byte[3][];
            private readonly uint[] _senders = new uint[3];
            public int Count { get; private set; }
            public int AlterationCount { get; private set; }
            public uint AlterationSender { get; private set; }
            public C_AlterationRequest LastAlteration { get; private set; }
            public bool TryEnqueueSessionAdmission(uint connectionId, ReadOnlySpan<byte> payload)
            {
                if (Count == _payloads.Length) return false;
                _payloads[Count] = payload.ToArray();
                _senders[Count++] = connectionId;
                return true;
            }
            public uint FindSender(ReadOnlySpan<byte> payload)
            {
                for (int i = 0; i < Count; i++)
                    if (_payloads[i].AsSpan().SequenceEqual(payload)) return _senders[i];
                throw new InvalidOperationException("Expected admission observation was not delivered.");
            }
            public void HandleAlterationRequest(uint connectionId, in C_AlterationRequest request)
            {
                AlterationCount++;
                AlterationSender = connectionId;
                LastAlteration = request;
            }
        }

        private sealed class ReplyInbox : IServerSessionAdmissionHandler
        {
            public int Count { get; private set; }
            public byte[] Last { get; private set; }
            public bool TryEnqueueSessionAdmissionReply(ReadOnlySpan<byte> payload)
            {
                if (Count == 2) return false;
                Last = payload.ToArray();
                Count++;
                return true;
            }
        }
    }
}
