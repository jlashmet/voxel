using System;
using Game.Characters.Api;
using Game.Sessions.Api;
using Game.Sessions.Runtime;
using Unity.Mathematics;
using Unity.Networking.Transport;
using VoxelEngine.Edits.Runtime;
using VoxelEngine.Net.Api;
using VoxelEngine.Net.Runtime.Client;
using VoxelEngine.Net.Runtime.Server;

namespace Game.Sessions.Validation
{
    /// <summary>
    /// Focused Sessions/Net admission boundary proof. The real authority and client runtimes own
    /// every connection. Deterministic membership/readiness inputs isolate the adapter; this is not
    /// a provider, gameplay authority, or separate-process multiplayer acceptance implementation.
    /// </summary>
    public sealed class SessionNetworkAdmissionProbe : IDisposable
    {
        private readonly PartySession _party;
        private readonly PartyMemberSnapshot _memberA;
        private readonly PartyMemberSnapshot _memberB;
        private readonly NetworkSpawnPosition _spawn = new NetworkSpawnPosition(3, 4, 5);
        private AuthoritativeServerSession _server;
        private ClientNetworkRuntime _clientA;
        private ClientNetworkRuntime _clientB;
        private SessionNetworkAdmissionAdapter _admission;
        private uint _lastOpened;
        private uint _connectionA;
        private uint _connectionB;
        private uint _oldA;
        private int _phase;
        private int _sessionEvents;
        private int _networkErrors;
        private bool _disposed;

        public event Action<string> Milestone;
        public bool Complete { get; private set; }
        public bool RejectionPreservedState { get; private set; }
        public bool DuplicatePreservedState { get; private set; }
        public bool ReconnectPreservedIdentity { get; private set; }
        public string PhaseDescription => Complete ? "Complete" : "Sessions admission phase " + _phase;

        public SessionNetworkAdmissionProbe()
        {
            var id = new GameSessionId("sessions-admission-validation");
            _party = new PartySession(id, new SessionStartupConfiguration(2, "validation-v1", "validation-content", true));
            _memberA = _party.Join(new JoinRequest(id, "applicant-a", "validation-v1", "validation-content")).Member;
            _memberB = _party.Join(new JoinRequest(id, "applicant-b", "validation-v1", "validation-content")).Member;
            Require(_party.BindCharacter(_memberA.MemberId, new CharacterId("character-a")), "character A input");
            Require(_party.BindCharacter(_memberB.MemberId, new CharacterId("character-b")), "character B input");
            _party.Changed += _ => _sessionEvents++;
            try
            {
                _server = new AuthoritativeServerSession(17,
                    new VoxelEngine.Net.Runtime.Server.Validation.DensityCap(1f, 0), maxConnections: 2);
                _admission = new SessionNetworkAdmissionAdapter(_party, _server);
                _server.ConnectionOpened += (connection, _) => _lastOpened = connection;
                _server.ConnectionClosed += connection => _admission.Disconnect(connection);
                _server.ProtocolError += _ => _networkErrors++;
                _clientA = new ClientNetworkRuntime(new DeterministicAlterationApplier());
                _clientB = new ClientNetworkRuntime(new DeterministicAlterationApplier());
                _clientA.PacketRejected += () => _networkErrors++;
                _clientB.PacketRejected += () => _networkErrors++;
                Require(_server.Listen(NetworkEndpoint.LoopbackIpv4.WithPort(0)) == 0, "listen");
                Require(_server.LocalEndpoint.Port != 0, "assigned port");
                Require(_clientA.Connect(_server.LocalEndpoint), "connect A");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>One bounded production pump; callers enforce a monotonic deadline.</summary>
        public void Step()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SessionNetworkAdmissionProbe));
            if (Complete) return;
            _server.PumpTransport();
            _clientA.PumpTransport();
            _clientB.PumpTransport();
            Require(_networkErrors == 0, "unexpected protocol error");

            switch (_phase)
            {
                case 0:
                    if (!_clientA.IsConnected || _server.ConnectionCount != 1 || _lastOpened == 0) return;
                    _connectionA = _lastOpened;
                    Require(_clientB.Connect(_server.LocalEndpoint), "connect B");
                    _phase = 1;
                    return;
                case 1:
                    if (!_clientB.IsConnected || _server.ConnectionCount != 2 || _lastOpened == _connectionA) return;
                    _connectionB = _lastOpened;
                    // A conflicting, already-authenticated Net identity is an external boundary input.
                    // It reproduces a real registry rejection, not a fake admission result.
                    Require(_server.AuthenticateNetworkPlayer(_connectionB, Actor(_memberA), _spawn, 8, false), "occupy conflicting actor");
                    PartyMemberSnapshot before = Read(_memberA.MemberId);
                    int eventsBefore = _sessionEvents;
                    Require(!_admission.Authenticate(_memberA.MemberId, _connectionA, _spawn, 8, false), "real Net actor collision rejects");
                    RequireSame(before, Read(_memberA.MemberId), "rejected member unchanged");
                    Require(_sessionEvents == eventsBefore && _server.Players.Count == 1, "rejection has no Sessions lifecycle side effects");
                    Require(!_party.TryResolveConnection(SessionNetworkAdmissionAdapter.FromConnectionId(_connectionA), out _), "no rejected handle bound");
                    RejectionPreservedState = true;
                    Milestone?.Invoke("SESSION_NETWORK_ADMISSION rejected: sessionsUnchanged=True productionRegistry=True");
                    Require(_server.Disconnect(_connectionB), "release conflicting transport through authority");
                    _phase = 2;
                    return;
                case 2:
                    if (_server.ConnectionCount != 1 || _clientB.IsConnected) return;
                    Require(_server.Players.Count == 0, "authority removed the old actor");
                    Require(_clientB.Connect(_server.LocalEndpoint), "replace B transport");
                    _phase = 3;
                    return;
                case 3:
                    if (!_clientB.IsConnected || _server.ConnectionCount != 2 || _lastOpened == _connectionB) return;
                    _connectionB = _lastOpened;
                    Require(_admission.Authenticate(_memberA.MemberId, _connectionA, _spawn, 8, false), "admit A");
                    Require(Read(_memberA.MemberId).Readiness == SessionReadinessState.Connected, "admission is not gameplay readiness");
                    MarkReady(_memberA.MemberId);
                    PartyMemberSnapshot liveA = Read(_memberA.MemberId);
                    int events = _sessionEvents;
                    Require(!_admission.Authenticate(_memberA.MemberId, _connectionB, _spawn, 8, false), "live replacement rejected");
                    RequireSame(liveA, Read(_memberA.MemberId), "live replacement cannot move identity");
                    Require(_sessionEvents == events && !_server.Players.TryGetByConnection(_connectionB, out _), "no second actor allocated");
                    Require(_admission.Authenticate(_memberB.MemberId, _connectionB, _spawn, 8, false), "admit B");
                    MarkReady(_memberB.MemberId);
                    Require(_server.UpdateAuthoritativePlayerPosition(_connectionA, new int3(11, 12, 13)), "authoritative position input");
                    events = _sessionEvents;
                    Require(_admission.Authenticate(_memberA.MemberId, _connectionA, new NetworkSpawnPosition(90, 91, 92), 99, true), "duplicate same identity");
                    RequireSame(liveA, Read(_memberA.MemberId), "duplicate retains durable state and readiness");
                    Require(_sessionEvents == events && _server.Players.Count == 2, "duplicate emits no connection event or actor");
                    Require(_server.Players.TryGetByConnection(_connectionA, out var actor) &&
                        actor.PositionVoxels.Equals(new int3(11, 12, 13)) && actor.ReachVoxels == 8 && !actor.CanAlterWorld,
                        "duplicate cannot respawn or escalate permissions");
                    Require(!_admission.Authenticate(_memberB.MemberId, _connectionA, _spawn, 8, false), "cross-member connection claim rejects");
                    Require(Read(_memberB.MemberId).Readiness == SessionReadinessState.GameplayReady, "other member remains ready");
                    DuplicatePreservedState = true;
                    Milestone?.Invoke("SESSION_NETWORK_ADMISSION retry: readinessPreserved=True permissionEscalation=False liveReplacementRejected=True");
                    _oldA = _connectionA;
                    _clientA.Disconnect();
                    _phase = 4;
                    return;
                case 4:
                    if (_server.ConnectionCount != 1 || Read(_memberA.MemberId).Presence != PartyPresenceState.Disconnected) return;
                    Require(_server.Players.Count == 1, "disconnect removed old network actor");
                    PartyMemberSnapshot interrupted = Read(_memberA.MemberId);
                    Require(!_admission.Authenticate(_memberA.MemberId, _oldA, _spawn, 8, false), "dead connection cannot be readmitted");
                    RequireSame(interrupted, Read(_memberA.MemberId), "dead-connection rejection does not mutate identity");
                    Require(_clientA.Connect(_server.LocalEndpoint), "replace A transport");
                    _phase = 5;
                    return;
                case 5:
                    if (!_clientA.IsConnected || _server.ConnectionCount != 2 || _lastOpened == _connectionB) return;
                    _connectionA = _lastOpened;
                    Require(_connectionA != _oldA, "replacement transport identity is fresh");
                    Require(_admission.Authenticate(_memberA.MemberId, _connectionA, _spawn, 8, false), "admit replacement A");
                    PartyMemberSnapshot rebound = Read(_memberA.MemberId);
                    Require(rebound.MemberId == _memberA.MemberId && rebound.Slot == _memberA.Slot &&
                        rebound.CharacterId == new CharacterId("character-a"), "durable identity survives transport replacement");
                    Require(rebound.Readiness == SessionReadinessState.Connected &&
                        Read(_memberB.MemberId).Readiness == SessionReadinessState.GameplayReady, "replacement must synchronize independently");
                    Require(!_admission.Disconnect(_oldA), "late old disconnect cannot affect new binding");
                    Require(_party.TryResolveConnection(SessionNetworkAdmissionAdapter.FromConnectionId(_connectionA), out var owner) &&
                        owner == _memberA.MemberId && _server.Players.Count == 2 && _party.Snapshot().Members.Count == 2,
                        "one durable member and one actor per client");
                    ReconnectPreservedIdentity = true;
                    Milestone?.Invoke("SESSION_NETWORK_ADMISSION reconnect: stableMember=True stableSlot=True stableCharacter=True freshConnection=True");
                    Require(_server.Disconnect(_connectionA) && _server.Disconnect(_connectionB), "normal transport cleanup");
                    _phase = 6;
                    return;
                case 6:
                    if (_server.ConnectionCount != 0) return;
                    Require(_server.Players.Count == 0, "no leaked authenticated actors");
                    Complete = true;
                    Milestone?.Invoke("SESSION_NETWORK_ADMISSION complete: productionSessions=True productionNet=True");
                    return;
                default:
                    throw new InvalidOperationException("Unknown Sessions admission phase.");
            }
        }

        private void MarkReady(PartyMemberId member)
        {
            // Explicit synchronization inputs: authentication itself must never grant readiness.
            Require(_party.MarkSynchronized(member) && _party.MarkGameplayReady(member), "semantic readiness input");
        }

        private PartyMemberSnapshot Read(PartyMemberId member)
        {
            Require(_party.TryGetMember(member, out PartyMemberSnapshot value), "member exists");
            return value;
        }

        private static ushort Actor(PartyMemberSnapshot member) => checked((ushort)(member.Slot.Value + 1));

        private static void RequireSame(PartyMemberSnapshot before, PartyMemberSnapshot after, string detail)
        {
            Require(before.MemberId == after.MemberId && before.Slot == after.Slot &&
                before.CharacterId == after.CharacterId && before.LeadershipRole == after.LeadershipRole &&
                before.Presence == after.Presence && before.Readiness == after.Readiness, detail);
        }

        private static void Require(bool condition, string detail)
        {
            if (!condition) throw new InvalidOperationException("Sessions network admission invariant: " + detail);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (_server != null)
                {
                    if (_connectionA != 0) _server.Disconnect(_connectionA);
                    if (_connectionB != 0) _server.Disconnect(_connectionB);
                }
            }
            finally
            {
                try { _clientA?.Dispose(); }
                finally
                {
                    try { _clientB?.Dispose(); }
                    finally { _server?.Dispose(); }
                }
            }
            _server = null;
            _clientA = null;
            _clientB = null;
        }
    }
}
