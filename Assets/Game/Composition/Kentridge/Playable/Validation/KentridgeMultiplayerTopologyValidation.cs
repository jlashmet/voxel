using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Game.Application.Api;
using Game.Characters.Api;
using Game.Composition.Campaign.Content;
using Game.Composition.Kentridge.Api;
using Game.Composition.Kentridge.Playable;
using Game.Composition.Kentridge.Runtime;
using Game.Composition.WorldBuilderWorldGen.Runtime;
using Game.Continuity.Api;
using Game.Cutscenes.Api;
using Game.GameplayReplication.Api;
using Game.Input.Api;
using Game.Inventory.Api;
using Game.Inventory.Runtime;
using Game.Loot.Runtime;
using Game.Outcomes.Api;
using Game.Persistence.Api;
using Game.SessionPresentation.Api;
using Game.Sessions.Api;
using Game.Vitality.Api;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using Game.WorldObjects.Api;
using Game.WorldObjects.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Networking.Transport;
using UnityEngine;
using VoxelEngine.Edits.Runtime;
using VoxelEngine.Net.Api;
using VoxelEngine.Net.Runtime.Client;
using VoxelEngine.Net.Runtime.Protocol;
using VoxelEngine.Net.Runtime.Server;
using VoxelEngine.Storage.Runtime;

namespace Game.Composition.Kentridge.Playable.Validation
{
    /// <summary>
    /// Build-once, separate-process smoke for the production Kentridge multiplayer composition.
    /// The harness supplies only deterministic process role/port/player setup and public player input.
    /// Application, Sessions, UTP admission, authoritative world interaction, inventory/progression/combat mutation,
    /// gameplay replication, Continuity, and the authority campaign graph are production types.
    /// </summary>
    public sealed class KentridgeMultiplayerTopologyValidation : MonoBehaviour
    {
        private const string SessionValue = "gamesystem25-topology";
        private const string Protocol = "gamesystem25-v1";
        private const string Content = "kentridge-generated-world";
        private const string ClientAMemberValue = "gamesystem25-topology:member:2";
        private const string ContentionObjectValue = KentridgeWellQuestDefinition.WellTargetId;
        private const string WellObjectiveValue = "rescue-boy-at-well.completion";
        private const string ForestNodeValue = "forest";
        private const uint Seed = 0x4B454E54u;
        private const string MilestonePrefix = "VOXEL_VALIDATION_MILESTONE ";
        private static readonly CharacterVector3 ContentionPosition = new CharacterVector3(12f, 0f, -4f);

        private string _role;
        private int _attempt;
        private ushort _port;
        private KentridgeAuthoritativeMultiplayerApplication _authority;
        private KentridgeClientMultiplayerApplication _client;
        private KentridgeSessionRuntimeGraphFactory _campaignGraph;
        private KentridgeCharacterHost _actors;
        private WorldObjectRegistry _worldObjects;
        private ItemPickupObject _contentionPickup;
        private KentridgeAuthoritativePlayerInputRouter _inputRouter;
        private KentridgeAuthoritativeGameplayCommandSink _gameplayCommands;
        private KentridgeAuthoritativeCombatActionSink _combatActions;
        private GameObject _forestRoot;
        private KentridgeForestBanditEncounter _forestEncounter;
        private RegionTable _table;
        private BrickPool _pool;
        private bool _tableCreated;
        private bool _poolCreated;
        private uint _serverTick;
        private ushort _combatInputSequence;
        private bool _joinedReported;
        private bool _topologyReported;
        private bool _baselineReported;
        private bool _contentionInitialized;
        private bool _contentionInputSent;
        private bool _contentionReported;
        private bool _progressionReported;
        private bool _continuityInterruptedReported;
        private bool _continuityRecoveredReported;
        private bool _combatTriggered;
        private bool _combatReported;
        private bool _recoveryCurrentStateReported;
        private bool _leaveRequested;
        private bool _leaveReported;
        private bool _startRequested;
        private string _failure;

        private void OnEnable()
        {
            if (!UnityEngine.Application.isPlaying) return;
            try
            {
                _role = Environment.GetEnvironmentVariable("VOXEL_VALIDATION_ROLE") ?? string.Empty;
                string[] args = Environment.GetCommandLineArgs();
                _port = ParsePort(args);
                _attempt = ParseAttempt(args);
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
            _table.LoadRegion(int3.zero);
            _worldObjects = new WorldObjectRegistry();

            _campaignGraph = BuildProductionCampaignGraph();
            KentridgeMultiplayerApplicationDependencies dependencies = BuildApplicationDependencies();
            var plans = new KentridgeMultiplayerSessionPlanProvider(
                "kentridge-opening-campaign", KentridgeDefinition.Id, "kentridge-generated-world");
            KentridgeMultiplayerGameplayReplication gameplay = KentridgeMultiplayerGameplayReplication.Create(
                _actors.Characters,
                () => _campaignGraph.Current?.Session.Inventory,
                () => _campaignGraph.Current?.Session.Runtime.Progression,
                () => _forestEncounter?.EncounterQuery,
                () => _forestEncounter?.VitalityQuery,
                () => _forestEncounter?.CombatService,
                () => _worldObjects);

            _authority = new KentridgeAuthoritativeMultiplayerApplication(
                _campaignGraph,
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
                TickAuthority,
                new KentridgeMultiplayerCharacterRoster(_actors.Characters, _ => ContentionPosition),
                gameplay);

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
                CreateClient,
                KentridgeMultiplayerGameplayReplication.Descriptors);
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
            if (!_joinedReported && TryFindLocal(party, out PartyMemberPresentationSnapshot local))
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

            ApplicationFlowSnapshot flow = application.Snapshot;
            bool readyForTopology = flow.Lifecycle == ApplicationLifecycle.InGame &&
                                    flow.GameplayReady &&
                                    party.Members.Count == 3;
            if (!_topologyReported && readyForTopology &&
                TryFindLocal(party, out PartyMemberPresentationSnapshot localReady) &&
                localReady.GameplayReady &&
                localReady.Connection == MemberConnectionPresentationState.Connected)
            {
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

            if (!_topologyReported) return;
            if (_authority != null) EnsureContentionFixture();

            IGameplayReplicationReadState readState = _authority != null
                ? _authority.ReadState
                : _client?.ReadState;
            if (!_baselineReported && TryCaptureBaseline(readState, out string revision, out string stateDigest))
            {
                _baselineReported = true;
                Emit(new Milestone
                {
                    name = _attempt > 1 ? "recovery-baseline-ready" : "baseline-ready",
                    role = _role,
                    sessionId = party.SessionId.Value,
                    rosterCount = party.Members.Count,
                    revision = revision,
                    stateDigest = stateDigest
                });
            }

            if (!_baselineReported) return;
            if (_client != null && _attempt == 1) TrySendContentionInput();
            TickContentionMilestone(readState);
            if (_contentionReported) TickProgressionMilestone(readState);
            if (_authority != null) TickContinuityMilestones();
            if (_progressionReported) TickCombatMilestone(readState);
            if (_client != null && _attempt > 1 && _contentionReported && _progressionReported && _combatReported)
                TickRecoveryCurrentState(application, party, readState);
            if (_authority != null) TickExplicitLeaveObserved();
        }

        private void EnsureContentionFixture()
        {
            if (_contentionInitialized || _campaignGraph?.Current?.Session == null) return;

            KentridgeCampaignSession session = _campaignGraph.Current.Session;
            var inventory = new InventoryTransactionsAdapter(
                session.InventoryAuthority,
                session.Inventory,
                session.InventoryState);
            var bindings = new CharacterInventoryBindings();
            for (int slot = 0; slot < 3; slot++)
            {
                if (!bindings.TryBind(
                        KentridgeMultiplayerCharacterRoster.CharacterIdForSlot(slot),
                        session.PlayerInventoryId))
                    throw new InvalidOperationException("Failed to bind contention character inventory for slot " + slot + ".");
            }

            var transfer = new WorldObjectLootAdapter(inventory, bindings);
            _contentionPickup = new ItemPickupObject(
                new WorldObjectId(ContentionObjectValue),
                ContentionPosition,
                new WorldItemPayload(KentridgeWellQuestDefinition.RewardItemId, 1),
                transfer);
            if (!_worldObjects.TryRegister(_contentionPickup))
                throw new InvalidOperationException("Failed to register GameSystem25 contention pickup.");

            var questObservations = new KentridgeWorldInteractionQuestObservationAdapter(
                observation => _campaignGraph.Current.ObserveQuest(observation));
            var interactions = new InteractionClickedProcessor(
                _actors.Characters,
                _worldObjects,
                questObservations);
            _combatActions = new KentridgeAuthoritativeCombatActionSink(
                () => _forestEncounter?.CombatService);
            _gameplayCommands = new KentridgeAuthoritativeGameplayCommandSink(
                interactions,
                _combatActions);
            _inputRouter = new KentridgeAuthoritativePlayerInputRouter(
                () => _authority?.PartySession,
                _gameplayCommands);
            _contentionInitialized = true;
        }

        private void TrySendContentionInput()
        {
            if (_contentionInputSent) return;
            ClientNetworkRuntime network = _client.UtpFormation.ActiveClient;
            if (network == null || !network.IsConnected) return;

            uint tick = (uint)Mathf.Max(1, Time.frameCount);
            var input = new C_PlayerInput(
                tick,
                1,
                float2.zero,
                new float3(0f, 0f, 1f),
                C_PlayerInput.ActionBits.UseMain,
                0);
            if (!network.TrySendPlayerInput(in input))
                throw new InvalidOperationException(_role + " failed to send production player input for contention.");
            network.FlushSends();
            _contentionInputSent = true;
            Emit(new Milestone
            {
                name = "contention-input-sent",
                role = _role,
                sessionId = SessionValue
            });
        }

        private void TickContentionMilestone(IGameplayReplicationReadState readState)
        {
            if (_contentionReported || readState == null) return;

            if (_authority != null)
            {
                if (!_contentionInitialized || _inputRouter == null || _inputRouter.AppliedInputs < 2 ||
                    _contentionPickup == null || _contentionPickup.Enabled ||
                    _campaignGraph?.Current?.Session == null)
                    return;
                int directQuantity = _campaignGraph.Current.Session.Inventory.Count(
                    _campaignGraph.Current.Session.PlayerInventoryId,
                    new ItemRef(KentridgeWellQuestDefinition.RewardItemId));
                if (directQuantity != 1)
                    throw new InvalidOperationException(
                        "Authoritative contention inventory quantity was " + directQuantity + ", expected exactly one.");
            }

            if (!TryReadContentionProjection(readState, out int quantity, out bool pickupEnabled)) return;
            if (quantity != 1 || pickupEnabled)
                throw new InvalidOperationException(
                    _role + " contention projection is not conserved: quantity=" + quantity +
                    " pickupEnabled=" + pickupEnabled + ".");

            _contentionReported = true;
            Emit(new Milestone
            {
                name = "contention-converged",
                role = _role,
                sessionId = SessionValue,
                quantity = quantity,
                pickupEnabled = pickupEnabled ? "true" : "false",
                appliedInputs = _inputRouter == null ? 0 : (int)_inputRouter.AppliedInputs,
                revision = readState.Revision.Value.ToString(CultureInfo.InvariantCulture)
            });
        }

        private void TickProgressionMilestone(IGameplayReplicationReadState readState)
        {
            if (_progressionReported || readState == null) return;
            if (!TryReadProgressionProjection(readState, out string state)) return;
            if (!string.Equals(state, "Completed", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    _role + " well interaction did not complete the authoritative progression objective: " + state + ".");

            _progressionReported = true;
            Emit(new Milestone
            {
                name = "progression-converged",
                role = _role,
                sessionId = SessionValue,
                progressionState = state,
                revision = readState.Revision.Value.ToString(CultureInfo.InvariantCulture)
            });
        }

        private void TickContinuityMilestones()
        {
            if (_authority?.Continuity == null) return;
            var memberId = new PartyMemberId(ClientAMemberValue);
            if (!_authority.Continuity.TryGetRecovery(memberId, out RecoverySnapshot recovery)) return;

            if (!_continuityInterruptedReported && recovery.State == RecoveryState.ConnectionInterrupted)
            {
                _continuityInterruptedReported = true;
                Emit(new Milestone
                {
                    name = "continuity-interrupted",
                    role = _role,
                    sessionId = SessionValue,
                    memberId = ClientAMemberValue,
                    characterId = KentridgeMultiplayerCharacterRoster.CharacterIdForSlot(1).Value,
                    slot = 1,
                    recoveryState = recovery.State.ToString()
                });
            }

            if (!_continuityRecoveredReported && recovery.State == RecoveryState.Recovered)
            {
                _continuityRecoveredReported = true;
                Emit(new Milestone
                {
                    name = "continuity-recovered",
                    role = _role,
                    sessionId = SessionValue,
                    memberId = ClientAMemberValue,
                    characterId = KentridgeMultiplayerCharacterRoster.CharacterIdForSlot(1).Value,
                    slot = 1,
                    recoveryState = recovery.State.ToString()
                });
            }
        }

        private void TickCombatMilestone(IGameplayReplicationReadState readState)
        {
            if (_combatReported || readState == null) return;

            if (_authority != null)
            {
                // The real combat mutation is deliberately held until client A has been observed as
                // interrupted by production Continuity. This makes the Vitality delta absent-period state.
                if (!_continuityInterruptedReported) return;
                if (_forestEncounter == null || _forestRoot == null || _gameplayCommands == null || _combatActions == null)
                    return;

                if (!_combatTriggered)
                {
                    _forestRoot.transform.position = _forestEncounter.AmbushCenterWorld;
                    _combatTriggered = true;
                    return;
                }

                if (!_forestEncounter.CombatActive) return;
                if (_gameplayCommands.AppliedCombatActions == 0)
                {
                    TrySendAuthorityCombatInput();
                    return;
                }

                CharacterId target = _combatActions.LastTargetCharacterId;
                if (!target.IsValid)
                    throw new InvalidOperationException("Authenticated combat action accepted without a durable target identity.");
                if (!_forestEncounter.VitalityQuery.TryGet(target, out VitalitySnapshot vitality))
                    return;
                if (vitality.Current >= vitality.Maximum)
                    throw new InvalidOperationException("Authenticated combat action produced no Vitality delta for " + target + ".");

                _forestEncounter.StopCommands();
                _combatReported = true;
                Emit(new Milestone
                {
                    name = "combat-vitality-converged",
                    role = _role,
                    sessionId = SessionValue,
                    damagedCharacter = target.Value,
                    currentVitality = vitality.Current,
                    maximumVitality = vitality.Maximum,
                    appliedCombatActions = (int)_gameplayCommands.AppliedCombatActions,
                    revision = readState.Revision.Value.ToString(CultureInfo.InvariantCulture)
                });
                return;
            }

            if (!TryReadDamagedEnemyVitality(readState, out string damagedCharacter, out int current, out int maximum))
                return;
            _combatReported = true;
            Emit(new Milestone
            {
                name = "combat-vitality-converged",
                role = _role,
                sessionId = SessionValue,
                damagedCharacter = damagedCharacter,
                currentVitality = current,
                maximumVitality = maximum,
                revision = readState.Revision.Value.ToString(CultureInfo.InvariantCulture)
            });
        }

        private void TickRecoveryCurrentState(
            Game.Application.Runtime.ApplicationFlowCoordinator application,
            PartyScreenPresentationSnapshot party,
            IGameplayReplicationReadState readState)
        {
            if (_recoveryCurrentStateReported || _role != "client-a" || _attempt <= 1) return;
            if (!TryFindLocal(party, out PartyMemberPresentationSnapshot local) ||
                local.MemberId.Value != ClientAMemberValue ||
                local.Slot.Value != 1 ||
                local.CharacterId != KentridgeMultiplayerCharacterRoster.CharacterIdForSlot(1))
                throw new InvalidOperationException("Recovered client A did not preserve durable member/slot/character identity.");

            if (!TryReadContentionProjection(readState, out int quantity, out bool pickupEnabled) ||
                !TryReadProgressionProjection(readState, out string progressionState) ||
                !TryReadDamagedEnemyVitality(readState, out string damagedCharacter, out int current, out int maximum))
                return;
            if (quantity != 1 || pickupEnabled ||
                !string.Equals(progressionState, "Completed", StringComparison.Ordinal) ||
                current != 4 || maximum != 6)
                throw new InvalidOperationException(
                    "Recovered current state diverged: quantity=" + quantity +
                    " pickupEnabled=" + pickupEnabled +
                    " progression=" + progressionState +
                    " vitality=" + current + "/" + maximum + ".");

            _recoveryCurrentStateReported = true;
            Emit(new Milestone
            {
                name = "recovery-current-state",
                role = _role,
                sessionId = SessionValue,
                memberId = local.MemberId.Value,
                characterId = local.CharacterId.Value,
                slot = local.Slot.Value,
                quantity = quantity,
                pickupEnabled = pickupEnabled ? "true" : "false",
                progressionState = progressionState,
                damagedCharacter = damagedCharacter,
                currentVitality = current,
                maximumVitality = maximum,
                revision = readState.Revision.Value.ToString(CultureInfo.InvariantCulture),
                recoveryMode = "current-projections-only"
            });

            if (_leaveRequested) return;
            Require(application.RequestLeaveGame(), "recovered client explicit leave");
            _leaveRequested = true;
            Emit(new Milestone
            {
                name = "explicit-leave-requested",
                role = _role,
                sessionId = SessionValue,
                memberId = local.MemberId.Value,
                characterId = local.CharacterId.Value,
                slot = local.Slot.Value
            });
        }

        private void TickExplicitLeaveObserved()
        {
            if (_leaveReported || !_continuityRecoveredReported || _authority?.Continuity == null ||
                _authority.PartySession == null)
                return;
            var memberId = new PartyMemberId(ClientAMemberValue);
            if (_authority.PartySession.TryGetMember(memberId, out _)) return;
            if (!_authority.Continuity.TryGetRecovery(memberId, out RecoverySnapshot recovery) ||
                recovery.State != RecoveryState.Left)
                return;

            _leaveReported = true;
            Emit(new Milestone
            {
                name = "explicit-leave-observed",
                role = _role,
                sessionId = SessionValue,
                memberId = ClientAMemberValue,
                characterId = KentridgeMultiplayerCharacterRoster.CharacterIdForSlot(1).Value,
                slot = 1,
                rosterCount = _authority.PartySession.Snapshot().Members.Count,
                recoveryState = recovery.State.ToString()
            });
        }

        private void TrySendAuthorityCombatInput()
        {
            ClientNetworkRuntime network = _authority?.UtpFormation.ActiveClient;
            if (network == null || !network.IsConnected) return;

            _combatInputSequence++;
            if (_combatInputSequence == 0) _combatInputSequence = 1;
            var input = new C_PlayerInput(
                _serverTick == 0 ? 1u : _serverTick,
                _combatInputSequence,
                float2.zero,
                new float3(0f, 0f, 1f),
                C_PlayerInput.ActionBits.UseAlt,
                0);
            if (!network.TrySendPlayerInput(in input))
                throw new InvalidOperationException("Authority host failed to send production combat input.");
            network.FlushSends();
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
            AuthoredTownPlan town = WorldBuilderTownAuthoring.Author(WorldBuilderTownIds.Kentridge, Seed);
            KentridgeCampaignGenerationPlan generation = KentridgeCampaignSessionBootstrap.Plan(content.Blueprint, town);
            _actors = new KentridgeCharacterHost(5.5f);
            PrepareForestEncounter();
            return new KentridgeSessionRuntimeGraphFactory(
                content.Blueprint,
                generation,
                KentridgeCampaignRealizationFacts.FromVoxelGeneration(generation, 1),
                _actors,
                ImmediatePresentation.Instance,
                null,
                _forestEncounter);
        }

        private void PrepareForestEncounter()
        {
            var node = new TopDownWorldNodeSpec(ForestNodeValue, ForestNodeValue, TopDownWorldNodeKind.Region);
            var layout = new TopDownWorldLayout(
                "gamesystem25-forest-layout",
                Seed,
                new[] { new TopDownWorldNodePlacement(node, new TopDownWorldGridPoint(0, 0)) },
                Array.Empty<TopDownWorldRouteSpec>());
            KentridgeForestEncounterRealization.RememberMacroLayout(
                layout,
                ForestNodeValue,
                1000,
                1000,
                100);

            _forestRoot = new GameObject("GameSystem25 Forest Encounter");
            _forestRoot.transform.position = new Vector3(
                ContentionPosition.X,
                ContentionPosition.Y,
                ContentionPosition.Z);
            _forestEncounter = _forestRoot.AddComponent<KentridgeForestBanditEncounter>();
        }

        private ClientNetworkRuntime CreateClient(
            Game.GameplayReplication.Transport.GameplayStateClientPacketHandler gameplay,
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
            IAuthoritativePlayerInputSink inputSink = _inputRouter != null
                ? (IAuthoritativePlayerInputSink)_inputRouter
                : NoInputSink.Instance;
            server.ProcessAuthoritativeTick(
                ++_serverTick,
                read,
                mutations,
                read,
                in zones,
                inputSink);
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

        private static bool TryFindLocal(
            PartyScreenPresentationSnapshot party,
            out PartyMemberPresentationSnapshot local)
        {
            for (int i = 0; i < party.Members.Count; i++)
            {
                if (!party.Members[i].IsLocal) continue;
                local = party.Members[i];
                return true;
            }
            local = default;
            return false;
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

        private static bool TryCaptureBaseline(
            IGameplayReplicationReadState readState,
            out string revision,
            out string stateDigest)
        {
            revision = string.Empty;
            stateDigest = string.Empty;
            if (readState == null || !readState.GameplayReady || readState.Revision.IsInitial)
                return false;

            ulong hash = 14695981039346656037UL;
            IReadOnlyList<GameplayProjectionDescriptor> descriptors = KentridgeMultiplayerGameplayReplication.Descriptors;
            for (int i = 0; i < descriptors.Count; i++)
            {
                GameplayProjectionDescriptor descriptor = descriptors[i];
                if (!readState.TryGetProjection(descriptor.Id, out GameplayProjectionState state))
                    return false;
                if ((descriptor.Id == KentridgeMultiplayerGameplayReplication.CharactersDescriptor.Id ||
                     descriptor.Id == KentridgeMultiplayerGameplayReplication.InventoryDescriptor.Id ||
                     descriptor.Id == KentridgeMultiplayerGameplayReplication.ProgressionDescriptor.Id ||
                     descriptor.Id == KentridgeMultiplayerGameplayReplication.EncountersDescriptor.Id ||
                     descriptor.Id == KentridgeMultiplayerGameplayReplication.VitalityDescriptor.Id ||
                     descriptor.Id == KentridgeMultiplayerGameplayReplication.CombatDescriptor.Id ||
                     descriptor.Id == KentridgeMultiplayerGameplayReplication.WorldObjectsDescriptor.Id) &&
                    state.Entries.Count == 0)
                    return false;

                hash = HashText(hash, descriptor.Id.Value);
                hash = HashText(hash, descriptor.SchemaVersion.ToString(CultureInfo.InvariantCulture));
                for (int entryIndex = 0; entryIndex < state.Entries.Count; entryIndex++)
                {
                    hash = HashText(hash, state.Entries[entryIndex].Key);
                    hash = HashText(hash, state.Entries[entryIndex].Value);
                }
            }

            revision = readState.Revision.Value.ToString(CultureInfo.InvariantCulture);
            stateDigest = hash.ToString("x16", CultureInfo.InvariantCulture);
            return true;
        }

        private static bool TryReadContentionProjection(
            IGameplayReplicationReadState readState,
            out int quantity,
            out bool pickupEnabled)
        {
            quantity = 0;
            pickupEnabled = true;
            if (!readState.TryGetProjection(
                    KentridgeMultiplayerGameplayReplication.WorldObjectsDescriptor.Id,
                    out GameplayProjectionState worldObjects) ||
                !readState.TryGetProjection(
                    KentridgeMultiplayerGameplayReplication.InventoryDescriptor.Id,
                    out GameplayProjectionState inventory))
                return false;

            string enabledKey = "object/" + ContentionObjectValue + "/enabled";
            bool foundPickup = false;
            for (int i = 0; i < worldObjects.Entries.Count; i++)
            {
                GameplayProjectionEntry entry = worldObjects.Entries[i];
                if (!string.Equals(entry.Key, enabledKey, StringComparison.Ordinal)) continue;
                if (!bool.TryParse(entry.Value, out pickupEnabled))
                    throw new InvalidOperationException("Invalid replicated pickup enabled value: " + entry.Value);
                foundPickup = true;
                break;
            }
            if (!foundPickup) return false;

            string itemSuffix = "/item/" + KentridgeWellQuestDefinition.RewardItemId;
            bool foundItem = false;
            for (int i = 0; i < inventory.Entries.Count; i++)
            {
                GameplayProjectionEntry entry = inventory.Entries[i];
                if (!entry.Key.EndsWith(itemSuffix, StringComparison.Ordinal)) continue;
                if (!int.TryParse(entry.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                    throw new InvalidOperationException("Invalid replicated inventory quantity: " + entry.Value);
                quantity += value;
                foundItem = true;
            }
            return foundItem;
        }

        private static bool TryReadProgressionProjection(
            IGameplayReplicationReadState readState,
            out string state)
        {
            state = string.Empty;
            if (!readState.TryGetProjection(
                    KentridgeMultiplayerGameplayReplication.ProgressionDescriptor.Id,
                    out GameplayProjectionState progression))
                return false;

            string key = "quest/" + KentridgeWellQuestDefinition.QuestId +
                         "/objective/" + WellObjectiveValue + "/state";
            for (int i = 0; i < progression.Entries.Count; i++)
            {
                GameplayProjectionEntry entry = progression.Entries[i];
                if (!string.Equals(entry.Key, key, StringComparison.Ordinal)) continue;
                state = entry.Value;
                return true;
            }
            return false;
        }

        private static bool TryReadDamagedEnemyVitality(
            IGameplayReplicationReadState readState,
            out string characterId,
            out int current,
            out int maximum)
        {
            characterId = string.Empty;
            current = 0;
            maximum = 0;
            if (!readState.TryGetProjection(
                    KentridgeMultiplayerGameplayReplication.VitalityDescriptor.Id,
                    out GameplayProjectionState vitality))
                return false;

            var currentByCharacter = new Dictionary<string, int>(StringComparer.Ordinal);
            var maximumByCharacter = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < vitality.Entries.Count; i++)
            {
                GameplayProjectionEntry entry = vitality.Entries[i];
                const string currentSuffix = "/current";
                const string maximumSuffix = "/maximum";
                if (entry.Key.EndsWith(currentSuffix, StringComparison.Ordinal))
                {
                    string id = entry.Key.Substring(0, entry.Key.Length - currentSuffix.Length);
                    if (!int.TryParse(entry.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                        throw new InvalidOperationException("Invalid replicated current Vitality: " + entry.Value);
                    currentByCharacter[id] = value;
                }
                else if (entry.Key.EndsWith(maximumSuffix, StringComparison.Ordinal))
                {
                    string id = entry.Key.Substring(0, entry.Key.Length - maximumSuffix.Length);
                    if (!int.TryParse(entry.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                        throw new InvalidOperationException("Invalid replicated maximum Vitality: " + entry.Value);
                    maximumByCharacter[id] = value;
                }
            }

            var candidates = new List<string>();
            foreach (KeyValuePair<string, int> pair in currentByCharacter)
            {
                if (string.Equals(pair.Key, KentridgeMultiplayerCharacterRoster.CharacterIdForSlot(0).Value, StringComparison.Ordinal))
                    continue;
                if (!maximumByCharacter.TryGetValue(pair.Key, out int max) || pair.Value >= max) continue;
                candidates.Add(pair.Key);
            }
            if (candidates.Count == 0) return false;
            candidates.Sort(StringComparer.Ordinal);
            characterId = candidates[0];
            current = currentByCharacter[characterId];
            maximum = maximumByCharacter[characterId];
            return true;
        }

        private static ulong HashText(ulong hash, string value)
        {
            const ulong prime = 1099511628211UL;
            string text = value ?? string.Empty;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                hash ^= (byte)c;
                hash *= prime;
                hash ^= (byte)(c >> 8);
                hash *= prime;
            }
            hash ^= 0xff;
            hash *= prime;
            return hash;
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

        private static int ParseAttempt(string[] args)
        {
            const string flag = "-voxel-validation-attempt";
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], flag, StringComparison.Ordinal) &&
                    int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value > 0)
                    return value;
            return 1;
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
                    try
                    {
                        if (_forestRoot != null) Destroy(_forestRoot);
                        _forestRoot = null;
                        _forestEncounter = null;
                    }
                    finally
                    {
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
            public string revision;
            public string stateDigest;
            public int quantity;
            public string pickupEnabled;
            public int appliedInputs;
            public string progressionState;
            public string damagedCharacter;
            public int currentVitality;
            public int maximumVitality;
            public int appliedCombatActions;
            public string recoveryState;
            public string recoveryMode;
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
            public bool TryLoad(out UserPreferences preferences)
            {
                preferences = UserPreferences.Default;
                return true;
            }
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
