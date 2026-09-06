using System;
using System.Collections.Generic;
using System.Text;
using Game.Application.Api;
using Game.Composition.Campaign.Content;
using Game.Composition.Kentridge.Playable;
using Game.Composition.Kentridge.Runtime;
using Game.Composition.WorldBuilderWorldGen.Runtime;
using Game.Cutscenes.Api;
using Game.Input.Api;
using Game.Outcomes.Api;
using Game.Persistence.Api;
using Game.SessionPresentation.Api;
using Game.Sessions.Api;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEngine;
using VoxelEngine.Edits.Runtime;
using VoxelEngine.Net.Api;
using VoxelEngine.Net.Runtime.Client;
using VoxelEngine.Net.Runtime.Server;
using VoxelEngine.Storage.Runtime;

namespace Game.Composition.Kentridge.Playable.Validation
{
    /// <summary>
    /// Build-once, separate-process smoke for the production Kentridge multiplayer composition.
    /// The harness supplies only process role/port/lifecycle. Application, Sessions, UTP admission,
    /// authority, gameplay replication, and the authority campaign graph are production types.
    /// </summary>
    public sealed class KentridgeMultiplayerTopologyValidation : MonoBehaviour
    {
        private const string SessionValue = "gamesystem25-topology";
        private const string Protocol = "gamesystem25-v1";
        private const string Content = "kentridge-generated-world";
        private const uint Seed = 0x4B454E54u;
        private const string MilestonePrefix = "VOXEL_VALIDATION_MILESTONE ";

        private string _role;
        private ushort _port;
        private KentridgeAuthoritativeMultiplayerApplication _authority;
        private KentridgeClientMultiplayerApplication _client;
        private KentridgeCharacterHost _actors;
        private RegionTable _table;
        private BrickPool _pool;
        private bool _tableCreated;
        private bool _poolCreated;
        private uint _serverTick;
        private bool _joinedReported;
        private bool _topologyReported;
        private bool _startRequested;
        private string _failure;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            try
            {
                _role = Environment.GetEnvironmentVariable("VOXEL_VALIDATION_ROLE") ?? string.Empty;
                _port = ParsePort(Environment.GetCommandLineArgs());
                if (_role == "authority") StartAuthority();
                else if (_role == "client-a" || _role == "client-b") StartClient();
                else throw new InvalidOperationException("Unsupported GameSystem25 validation role: " + _role);
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private void Update()
        {
            if (_failure != null) return;
            try
            {
                int elapsedMs = Mathf.Max(0, Mathf.RoundToInt(Time.unscaledDeltaTime * 1000f));
                if (_authority != null)
                {
                    _actors?.Tick(Time.unscaledDeltaTime);
                    _authority.TickNetworkAndAuthority();
                    Require(_authority.Application.Update(elapsedMs), "authority application update");
                    TickHostStart();
                    TickMilestones(_authority.Application);
                }
                else if (_client != null)
                {
                    _client.TickNetwork();
                    Require(_client.Application.Update(elapsedMs), _role + " application update");
                    TickMilestones(_client.Application);
                }
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private void StartAuthority()
        {
            _table = new RegionTable(1, Allocator.Persistent);
            _tableCreated = true;
            _pool = new BrickPool(4, Allocator.Persistent);
            _poolCreated = true;
            _table.LoadRegion(Unity.Mathematics.int3.zero);

            KentridgeSessionRuntimeGraphFactory graph = BuildProductionCampaignGraph();
            KentridgeMultiplayerApplicationDependencies dependencies = BuildApplicationDependencies();
            var plans = new KentridgeMultiplayerSessionPlanProvider(
                "kentridge-opening-campaign", KentridgeDefinition.Id, "kentridge-generated-world");

            _authority = new KentridgeAuthoritativeMultiplayerApplication(
                graph,
                dependencies,
                plans,
                CreateClient,
                (emitter, admission) => new AuthoritativeServerSession(
                    Seed,
                    new VoxelEngine.Net.Runtime.Server.Validation.DensityCap(1f, 0),
                    new DeterministicAlterationApplier(),
                    maxConnections: 4,
                    gameplayStateEmitter: emitter,
                    sessionAdmissionConsumer: admission),
                () => NetworkEndpoint.LoopbackIpv4.WithPort(_port),
                server => server.LocalEndpoint,
                TickAuthority);

            Require(_authority.Application.CompleteBoot(), "authority boot");
            var configuration = new SessionStartupConfiguration(3, Protocol, Content, true);
            Require(_authority.Application.RequestHost(
                new HostSessionRequest(new GameSessionId(SessionValue), configuration, "host")), "authority host request");
            if (_authority.Server == null || _authority.Server.LocalEndpoint.Port != _port)
                throw new InvalidOperationException("Production authority did not bind the requested loopback endpoint.");
            Emit(new Milestone
            {
                name = "authority-listening",
                role = _role,
                sessionId = SessionValue,
                port = _port
            });
        }

        private void StartClient()
        {
            KentridgeMultiplayerApplicationDependencies dependencies = BuildApplicationDependencies();
            var plans = new KentridgeMultiplayerSessionPlanProvider(
                "kentridge-opening-campaign", KentridgeDefinition.Id, "kentridge-generated-world");
            _client = new KentridgeClientMultiplayerApplication(
                dependencies,
                plans,
                () => NetworkEndpoint.LoopbackIpv4.WithPort(_port),
                CreateClient);
            Require(_client.Application.CompleteBoot(), _role + " boot");
            Require(_client.Application.RequestJoin(new JoinSessionRequest(
                new JoinRequest(new GameSessionId(SessionValue), _role, Protocol, Content))), _role + " join request");
        }

        private void TickHostStart()
        {
            if (_startRequested || !_authority.Application.TryCapturePartyScreen(out PartyScreenPresentationSnapshot party))
                return;
            if (party.Members.Count != 3 || !party.CanStart) return;
            Require(_authority.Application.RequestPartyStart(), "authority party start");
            _startRequested = true;
        }

        private void TickMilestones(Game.Application.Runtime.ApplicationFlowCoordinator application)
        {
            if (!application.TryCapturePartyScreen(out PartyScreenPresentationSnapshot party)) return;
            if (!_joinedReported)
            {
                PartyMemberPresentationSnapshot local = FindLocal(party);
                if (local != null)
                {
                    _joinedReported = true;
                    Emit(new Milestone
                    {
                        name = "party-joined",
                        role = _role,
                        sessionId = party.SessionId.Value,
                        memberId = local.MemberId.Value,
                        characterId = local.CharacterId.Value,
                        slot = local.Slot.Value,
                        rosterCount = party.Members.Count
                    });
                }
            }

            ApplicationFlowSnapshot flow = application.Snapshot;
            if (_topologyReported || flow.Lifecycle != ApplicationLifecycle.InGame || !flow.GameplayReady ||
                party.Members.Count != 3)
                return;

            PartyMemberPresentationSnapshot localReady = FindLocal(party);
            if (localReady == null || !localReady.GameplayReady ||
                localReady.Connection != MemberConnectionPresentationState.Connected)
                return;

            string signature = TopologySignature(party);
            const string expected =
                "0=gamesystem25-topology:member:1/kentridge-player-1;" +
                "1=gamesystem25-topology:member:2/kentridge-player-2;" +
                "2=gamesystem25-topology:member:3/kentridge-player-3";
            if (!string.Equals(signature, expected, StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected durable topology: " + signature);

            _topologyReported = true;
            Emit(new Milestone
            {
                name = "topology-ready",
                role = _role,
                sessionId = party.SessionId.Value,
                memberId = localReady.MemberId.Value,
                characterId = localReady.CharacterId.Value,
                slot = localReady.Slot.Value,
                rosterCount = party.Members.Count,
                signature = signature
            });
        }

        private KentridgeSessionRuntimeGraphFactory BuildProductionCampaignGraph()
        {
            var destinationSpeaker = new CutsceneActorId("destination-npc");
            KnownOpeningCampaignContent content = KnownOpeningCampaignContent.Build(
                new CutsceneDefinition(
                    "destination-conversation",
                    CutsceneStageSetupDefinition.Empty,
                    new[] { CutsceneStep.Dialogue(destinationSpeaker, new CutsceneCueId("destination-conversation.dialogue")) }),
                (scene, roles) => scene.Bind(destinationSpeaker, roles.DestinationNpc));
            SettlementPlan settlement = KentridgeDefinition.Build(Seed);
            KentridgeCampaignGenerationPlan generation = KentridgeCampaignSessionBootstrap.Plan(content.Blueprint, settlement);
            _actors = new KentridgeCharacterHost(5.5f);
            return new KentridgeSessionRuntimeGraphFactory(
                content.Blueprint,
                generation,
                new KentridgeCampaignRealizationFacts(new KentridgeVoxelSiteRealizationFacts(settlement, 1)),
                _actors,
                ImmediatePresentation.Instance);
        }

        private ClientNetworkRuntime CreateClient(
            Game.GameplayReplication.Runtime.GameplayStateClientPacketHandler gameplay,
            IServerSessionAdmissionHandler admission) =>
            new ClientNetworkRuntime(
                new DeterministicAlterationApplier(),
                gameplayStateHandler: gameplay,
                sessionAdmissionHandler: admission);

        private void TickAuthority(AuthoritativeServerSession server)
        {
            ProtectedZones zones = default;
            var read = new RegionReadSource(in _table, in _pool);
            var mutations = new RegionMutationStore(in _table, in _pool);
            server.ProcessAuthoritativeTick(++_serverTick, read, mutations, read, in zones, NoInputSink.Instance);
        }

        private static KentridgeMultiplayerApplicationDependencies BuildApplicationDependencies() =>
            new KentridgeMultiplayerApplicationDependencies(
                EmptySaveCatalog.Instance,
                RunningOutcomeQuery.Instance,
                new InputContexts(),
                EmptyBindings.Instance,
                DefaultPreferences.Instance,
                NoAudio.Instance,
                NoExit.Instance);

        private static PartyMemberPresentationSnapshot FindLocal(PartyScreenPresentationSnapshot party)
        {
            for (int i = 0; i < party.Members.Count; i++)
                if (party.Members[i].IsLocal) return party.Members[i];
            return null;
        }

        private static string TopologySignature(PartyScreenPresentationSnapshot party)
        {
            var ordered = new List<PartyMemberPresentationSnapshot>(party.Members.Count);
            for (int i = 0; i < party.Members.Count; i++) ordered.Add(party.Members[i]);
            ordered.Sort((a, b) => a.Slot.Value.CompareTo(b.Slot.Value));
            var text = new StringBuilder();
            for (int i = 0; i < ordered.Count; i++)
            {
                if (i > 0) text.Append(';');
                text.Append(ordered[i].Slot.Value).Append('=').Append(ordered[i].MemberId.Value)
                    .Append('/').Append(ordered[i].CharacterId.Value);
            }
            return text.ToString();
        }

        private static ushort ParsePort(string[] args)
        {
            const string flag = "-gamesystem25-port";
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], flag, StringComparison.Ordinal) &&
                    ushort.TryParse(args[i + 1], out ushort value) && value != 0)
                    return value;
            throw new InvalidOperationException(flag + " requires a non-zero UInt16 port.");
        }

        private static void Require(ApplicationOperationResult result, string operation)
        {
            if (!result.Succeeded)
                throw new InvalidOperationException(operation + " failed: " + result.Failure + " " + result.Detail);
        }

        private static void Emit(Milestone milestone) =>
            Debug.Log(MilestonePrefix + JsonUtility.ToJson(milestone));

        private void Fail(Exception exception)
        {
            _failure = exception.ToString();
            Debug.LogError("GAMESYSTEM25_MULTIPLAYER_TOPOLOGY FAIL role=" + _role + " " + exception);
            DisposeRuntime();
        }

        private void OnDisable() => DisposeRuntime();

        private void DisposeRuntime()
        {
            try { _client?.Dispose(); }
            finally
            {
                _client = null;
                try { _authority?.Dispose(); }
                finally
                {
                    _authority = null;
                    try { _actors?.Dispose(); }
                    finally
                    {
                        _actors = null;
                        try { if (_poolCreated) _pool.Dispose(); }
                        finally
                        {
                            _poolCreated = false;
                            if (_tableCreated) _table.Dispose();
                            _tableCreated = false;
                        }
                    }
                }
            }
        }

        [Serializable]
        private sealed class Milestone
        {
            public string name;
            public string role;
            public string sessionId;
            public string memberId;
            public string characterId;
            public int slot;
            public int rosterCount;
            public int port;
            public string signature;
        }

        private sealed class EmptySaveCatalog : ISessionSaveCatalog
        {
            public static readonly EmptySaveCatalog Instance = new EmptySaveCatalog();
            public IReadOnlyList<SessionSaveMetadata> ListSaves() => Array.Empty<SessionSaveMetadata>();
        }

        private sealed class RunningOutcomeQuery : IGameOutcomeQuery
        {
            public static readonly RunningOutcomeQuery Instance = new RunningOutcomeQuery();
            public GameOutcomeSnapshot Snapshot() => GameOutcomeSnapshot.Running();
        }

        private sealed class InputContexts : IInputContextService
        {
            public InputContextId ActiveContext { get; private set; } = InputContextId.Exploration;
            public IInputContextLease Push(InputContextId context)
            {
                InputContextId previous = ActiveContext;
                ActiveContext = context;
                return new ContextLease(this, previous, context);
            }

            private sealed class ContextLease : IInputContextLease
            {
                private InputContexts _owner;
                private readonly InputContextId _previous;
                public InputContextId Context { get; }
                public ContextLease(InputContexts owner, InputContextId previous, InputContextId context)
                {
                    _owner = owner; _previous = previous; Context = context;
                }
                public void Dispose()
                {
                    if (_owner == null) return;
                    _owner.ActiveContext = _previous;
                    _owner = null;
                }
            }
        }

        private sealed class EmptyBindings : IInputBindingOverrideService
        {
            public static readonly EmptyBindings Instance = new EmptyBindings();
            public IReadOnlyList<InputBindingOverride> SnapshotOverrides() => Array.Empty<InputBindingOverride>();
            public bool TryApplyOverride(InputBindingOverride bindingOverride, out string error)
            {
                error = string.Empty;
                return true;
            }
            public void ClearOverrides() { }
        }

        private sealed class DefaultPreferences : IUserPreferencesStore
        {
            public static readonly DefaultPreferences Instance = new DefaultPreferences();
            public bool TryLoad(out UserPreferences preferences) { preferences = UserPreferences.Default; return true; }
            public void Save(UserPreferences preferences) { }
        }

        private sealed class NoAudio : IAudioPreferencesSink
        {
            public static readonly NoAudio Instance = new NoAudio();
            public void Apply(UserPreferences preferences) { }
        }

        private sealed class NoExit : IApplicationExitPort
        {
            public static readonly NoExit Instance = new NoExit();
            public void RequestExit() { }
        }

        private sealed class ImmediatePresentation : ICutscenePresentation
        {
            public static readonly ImmediatePresentation Instance = new ImmediatePresentation();
            public ICutsceneOperation SetCamera(CutsceneCueId cameraCue) => CompletedCutsceneOperation.Instance;
            public ICutsceneOperation ShowDialogue(CutsceneActorId speaker, CutsceneCueId dialogueCue) => CompletedCutsceneOperation.Instance;
            public ICutsceneOperation PlaySound(CutsceneCueId soundCue) => CompletedCutsceneOperation.Instance;
        }

        private sealed class NoInputSink : IAuthoritativePlayerInputSink
        {
            public static readonly NoInputSink Instance = new NoInputSink();
            public void ApplyInput(ushort playerId, in C_PlayerInput input, uint serverTick) { }
        }
    }
}
