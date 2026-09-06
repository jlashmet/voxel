using System;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Networking.Transport;
using VoxelEngine.Edits.Api;
using VoxelEngine.Edits.Runtime;
using VoxelEngine.Net.Runtime.Client;
using VoxelEngine.Net.Runtime.Protocol;
using VoxelEngine.Net.Runtime.Server;
using VoxelEngine.Storage.Runtime;

namespace VoxelEngine.Net.Validation
{
    /// <summary>
    /// Bounded transport-only orchestration shared by the Net player scene and its EditMode test.
    /// The canonical authority root owns transport, queued intake and fixed-tick processing. Copied
    /// packet observations are not Sessions admission policy or separate-process gameplay evidence.
    /// </summary>
    public sealed class SessionAdmissionTransportProbe : IDisposable
    {
        private readonly AdmissionObservations _requests = new AdmissionObservations();
        private readonly ReplyInbox _repliesA = new ReplyInbox();
        private readonly ReplyInbox _repliesB = new ReplyInbox();
        private readonly byte[] _requestA = MakePayload(SessionAdmissionPacket.MaxPayloadBytes, 0xA1);
        private readonly byte[] _requestB = MakePayload(3, 0xB2);
        private readonly byte[] _reconnectRequest = MakePayload(7, 0xC3);
        private readonly byte[] _staleRequest = MakePayload(5, 0xD4);
        private readonly C_AlterationRequest _alteration = new C_AlterationRequest(
            tick: 41, origin: new int3(510, 40, -12), eventKind: AlterationEvent.KindExplosion,
            material: 0, shapeKind: AlterationEvent.KindExplosion, shapeData: 3, seed: 0x12345678, sequence: 9);
        private readonly NoInputSink _inputSink = new NoInputSink();
        private AuthoritativeServerSession _server;
        private ClientNetworkRuntime _clientA;
        private ClientNetworkRuntime _clientB;
        private RegionTable _table;
        private BrickPool _pool;
        private bool _tableCreated;
        private bool _poolCreated;
        private int _phase;
        private int _protocolErrors;
        private uint _oldConnection;
        private uint _tick;
        private bool _disposed;

        public event Action<string> Milestone;
        public bool Complete { get; private set; }
        public int ReceivedRequestCount => _requests.Count;
        public bool DistinctSenders { get; private set; }
        public bool IsolatedReplies { get; private set; }
        public bool ReplacedConnection { get; private set; }
        public bool TickDeferred { get; private set; }
        public bool DisconnectedRequestDiscarded { get; private set; }
        public string PhaseDescription => Complete ? "Complete" : "Transport phase " + _phase;

        public SessionAdmissionTransportProbe()
        {
            try
            {
                _table = new RegionTable(1, Allocator.Persistent);
                _tableCreated = true;
                _pool = new BrickPool(4, Allocator.Persistent);
                _poolCreated = true;
                _table.LoadRegion(int3.zero);
                _server = new AuthoritativeServerSession(
                    serverSeed: 0x12345678,
                    densityCap: new VoxelEngine.Net.Runtime.Server.Validation.DensityCap(1f, 0),
                    alterationApplier: new DeterministicAlterationApplier(),
                    maxConnections: 2,
                    sessionAdmissionConsumer: _requests);
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

        /// <summary>One bounded pump; callers enforce their monotonic deadline, never a correctness sleep.</summary>
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
                    if (_server.CommandInbox.PendingSessionAdmissions != 2) return;
                    TickDeferred = _requests.Count == 0;
                    Require(TickDeferred, "transport callbacks cannot run admission policy");
                    Tick();
                    Require(_requests.Count == 2 && _server.CommandInbox.PendingTotal == 0, "fixed-tick admission handoff");
                    Milestone?.Invoke("SESSION_ADMISSION_TRANSPORT authority: tickDeferred=True consumed=2");
                    _oldConnection = _requests.FindSender(_requestA);
                    uint senderB = _requests.FindSender(_requestB);
                    DistinctSenders = _oldConnection != 0 && senderB != 0 && _oldConnection != senderB;
                    Require(DistinctSenders, "distinct transport-owned senders");
                    // Echo opaque observations outside transport callbacks, not an admission grant.
                    Require(_server.TrySendSessionAdmissionReply(_oldConnection, _requestA), "reply A");
                    Require(_server.TrySendSessionAdmissionReply(senderB, _requestB), "reply B");
                    Tick();
                    Require(_requests.Count == 2, "consumed requests cannot replay on later ticks");
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
                    if (_server.CommandInbox.PendingAlterations != 1) return;
                    Tick();
                    Require(_server.CommandInbox.PendingTotal == 0 &&
                        _server.Processor.UnauthenticatedCommands == 1 && _server.Processor.AcceptedAlterations == 0,
                        "existing EVENT traffic retains authoritative authentication checks");
                    Milestone?.Invoke("SESSION_ADMISSION_TRANSPORT existing-traffic: alteration=True");
                    Require(_clientA.TrySendSessionAdmission(_staleRequest), "queue request before interruption");
                    _clientA.FlushSends();
                    _phase = 4;
                    return;
                case 4:
                    if (_server.CommandInbox.PendingSessionAdmissions != 1) return;
                    Require(_requests.Count == 2, "new request remains pending until tick");
                    _clientA.Disconnect();
                    Require(!_clientA.TrySendSessionAdmission(_reconnectRequest), "cannot send while disconnected");
                    _phase = 5;
                    return;
                case 5:
                    if (_server.ConnectionCount != 1) return;
                    Require(_server.CommandInbox.PendingTotal == 0, "disconnect releases admission reservations");
                    Tick();
                    DisconnectedRequestDiscarded = _requests.Count == 2;
                    Require(DisconnectedRequestDiscarded, "dead sender cannot be admitted on a later tick");
                    Milestone?.Invoke("SESSION_ADMISSION_TRANSPORT disconnect: queuedRequestDiscarded=True");
                    Require(!_server.TrySendSessionAdmissionReply(_oldConnection, _requestA), "old connection no longer routable");
                    Require(_clientA.Connect(_server.LocalEndpoint), "reconnect A");
                    _phase = 6;
                    return;
                case 6:
                    if (!_clientA.IsConnected || _server.ConnectionCount != 2) return;
                    Require(_clientA.TrySendSessionAdmission(_reconnectRequest), "fresh admission on replacement transport");
                    _clientA.FlushSends();
                    _phase = 7;
                    return;
                case 7:
                    if (_server.CommandInbox.PendingSessionAdmissions != 1) return;
                    Require(_requests.Count == 2, "replacement also waits for tick");
                    Tick();
                    Require(_requests.Count == 3, "fresh request consumed once");
                    uint newConnection = _requests.FindSender(_reconnectRequest);
                    ReplacedConnection = newConnection != 0 && newConnection != _oldConnection;
                    Require(ReplacedConnection, "replacement has a fresh transient connection");
                    Require(_server.TrySendSessionAdmissionReply(newConnection, _reconnectRequest), "replacement reply");
                    Tick();
                    _phase = 8;
                    return;
                case 8:
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

        private void Tick()
        {
            ProtectedZones zones = default;
            var read = new RegionReadSource(in _table, in _pool);
            var mutations = new RegionMutationStore(in _table, in _pool);
            _server.ProcessAuthoritativeTick(++_tick, read, mutations, read, in zones, _inputSink);
        }

        private void RequireNoGameplayBinding()
        {
            Require(_clientA.LocalPlayerId == 0 && _clientB.LocalPlayerId == 0 &&
                !_requests.HasAuthenticatedSender(_server.Players) &&
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
                finally
                {
                    try { _server?.Dispose(); }
                    finally
                    {
                        try { if (_poolCreated) _pool.Dispose(); }
                        finally { if (_tableCreated) _table.Dispose(); }
                    }
                }
            }
            _clientA = null;
            _clientB = null;
            _server = null;
        }

        private sealed class NoInputSink : IAuthoritativePlayerInputSink
        {
            public void ApplyInput(ushort playerId, in C_PlayerInput input, uint serverTick) =>
                throw new InvalidOperationException("Unadmitted clients must not produce gameplay input.");
        }

        // Fixed-capacity copied observations, invoked by the authority tick rather than transport.
        private sealed class AdmissionObservations : IAuthoritativeSessionAdmissionConsumer
        {
            private readonly byte[][] _payloads = new byte[3][];
            private readonly uint[] _senders = new uint[3];
            public int Count { get; private set; }
            public void HandleSessionAdmission(uint connectionId, ReadOnlySpan<byte> payload)
            {
                if (Count == _payloads.Length) throw new InvalidOperationException("Duplicate admission consumption.");
                _payloads[Count] = payload.ToArray();
                _senders[Count++] = connectionId;
            }
            public uint FindSender(ReadOnlySpan<byte> payload)
            {
                for (int i = 0; i < Count; i++)
                    if (_payloads[i].AsSpan().SequenceEqual(payload)) return _senders[i];
                throw new InvalidOperationException("Expected admission observation was not delivered.");
            }
            public bool HasAuthenticatedSender(ServerPlayerRegistry players)
            {
                for (int i = 0; i < Count; i++)
                    if (players.TryGetByConnection(_senders[i], out _)) return true;
                return false;
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
