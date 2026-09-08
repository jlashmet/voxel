using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
using Game.Persistence.Runtime;
using Game.SessionOrchestration.Api;
using Game.SessionPresentation.Api;
using Game.Sessions.Api;
using Game.Sessions.Runtime;
using Game.WorldBuilder.Api;
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
using VoxelEngine.Net.Runtime.Server;
using VoxelEngine.Storage.Runtime;

namespace Game.Composition.Kentridge.Playable.Validation.Release
{
    /// <summary>
    /// Scheduled/release proof for System25's expensive multiplayer cases. Every process enters through
    /// the production Application/Sessions/provider/UTP topology. Validation-side files only remember
    /// observations for cross-process assertions; they never seed membership, gameplay, or reconnect.
    /// </summary>
    public sealed class KentridgeMultiplayerCapacityJipRehostValidation : MonoBehaviour
    {
        private const string SessionValue = "gamesystem25-release";
        private const string SaveValue = "gamesystem25-release-rehost";
        private const string Protocol = "gamesystem25-release-v1";
        private const string Content = "kentridge-generated-world";
        private const string ContentionObjectValue = KentridgeWellQuestDefinition.WellTargetId;
        private const string WellObjectiveValue = "rescue-boy-at-well.completion";
        private const uint Seed = 0x4B454E54u;
        private const string MilestonePrefix = "VOXEL_VALIDATION_MILESTONE ";
        private static readonly CharacterVector3 ContentionPosition = new CharacterVector3(12f, 0f, -4f);

        private string _role;
        private int _attempt;
        private ushort _port;
        private SessionStartupConfiguration _configuration;
        private KentridgeAuthoritativeMultiplayerApplication _authority;
        private KentridgeClientMultiplayerApplication _client;
        private KentridgeSessionRuntimeGraphFactory _campaignGraph;
        private KentridgeCharacterHost _actors;
        private KentridgeMultiplayerPersistenceBridge _persistence;
        private WorldObjectRegistry _worldObjects;
        private ItemPickupObject _contentionPickup;
        private KentridgeAuthoritativePlayerInputRouter _inputRouter;
        private KentridgeAuthoritativeGameplayCommandSink _gameplayCommands;
        private RegionTable _table;
        private BrickPool _pool;
        private bool _tableCreated;
        private bool _poolCreated;
        private uint _serverTick;
        private bool _joinedReported;
        private bool _startRequested;
        private bool _fixtureInitialized;
        private bool _mutationInputSent;
        private bool _mutationReported;
        private bool _capacityReported;
        private bool _jipReported;
        private bool _reconnectReported;
        private bool _rehostReported;
        private bool _saveReported;
        private int _recoveryCount;
        private bool _hasRecoveryState;
        private RecoveryState _lastRecoveryState;
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
                int capacity = ParseCapacity(args);
                _configuration = new SessionStartupConfiguration(capacity, Protocol, Content, true);
                if (_role == "authority") StartAuthority();
                else if (_role == "client-a" || _role == "client-b" || _role == "client-c" || _role == "client-d")
                    StartClient();
                else throw new InvalidOperationException("Unsupported GameSystem25 release role: " + _role);
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

            string stateRoot = RequireStateRoot();
            _persistence = new KentridgeMultiplayerPersistenceBridge(
                BuildPersistenceContributors,
                new FileSessionSaveStore(Path.Combine(stateRoot, "session-saves")),
                new SessionSaveId(SaveValue),
                () => _serverTick,
                () => _campaignGraph.Current?.Session.SynchronizeRewards());

            KentridgeMultiplayerApplicationDependencies dependencies = BuildApplicationDependencies(_persistence);
            var plans = new KentridgeMultiplayerSessionPlanProvider(
                "kentridge-opening-campaign", KentridgeDefinition.Id, "kentridge-generated-world");
            KentridgeMultiplayerGameplayReplication gameplay = KentridgeMultiplayerGameplayReplication.Create(
                _actors.Characters,
                () => _campaignGraph.Current?.Session.Inventory,
                () => _campaignGraph.Current?.Session.Runtime.Progression,
                () => null,
                () => null,
                () => null,
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
                    maxConnections: _configuration.Capacity,
                    gameplayStateEmitter: emitter,
                    sessionAdmissionConsumer: admission),
                () => NetworkEndpoint.LoopbackIpv4.WithPort(_port),
                server => server.LocalEndpoint,
                TickAuthority,
                new KentridgeMultiplayerCharacterRoster(_actors.Characters, _ => ContentionPosition),
                gameplay,
                _persistence);

            Require(_authority.Application.CompleteBoot(), "authority boot");
            Require(_authority.Application.RequestHost(
                new HostSessionRequest(new GameSessionId(SessionValue), _configuration, "host")),
                "authority host request");

            if (_attempt > 1)
                Require(_authority.Application.PreparePartyResume(SaveValue), "authority prepare persisted rehost");

            if (_authority.Server == null || _authority.Server.LocalEndpoint.Port != _port)
                throw new InvalidOperationException("Production authority did not bind the requested loopback endpoint.");

            Emit(new Milestone
            {
                name = "authority-listening",
                role = _role,
                sessionId = SessionValue,
                port = _port,
                configuredCapacity = _configuration.Capacity,
                processAttempt = _attempt
            });

            if (_attempt > 1)
            {
                PartySessionStateCapture restored = _authority.PartySession.CaptureState();
                if (restored.Members.Count != _configuration.Capacity)
                    throw new InvalidOperationException(
                        "Persisted rehost restored " + restored.Members.Count + " members, expected " + _configuration.Capacity + ".");
                string signature = SemanticIdentitySignature(restored);
                RequireSavedIdentitySignature(signature);
                Emit(new Milestone
                {
                    name = "rehost-restore-prepared",
                    role = _role,
                    sessionId = SessionValue,
                    configuredCapacity = _configuration.Capacity,
                    rosterCount = restored.Members.Count,
                    signature = signature,
                    processAttempt = _attempt,
                    transportMode = "fresh-authority-process"
                });
            }
        }

        private void StartClient()
        {
            KentridgeMultiplayerApplicationDependencies dependencies = BuildApplicationDependencies(EmptySaveCatalog.Instance);
            var plans = new KentridgeMultiplayerSessionPlanProvider(
                "kentridge-opening-campaign", KentridgeDefinition.Id, "kentridge-generated-world");
            _client = new KentridgeClientMultiplayerApplication(
                dependencies,
                plans,
                () => NetworkEndpoint.LoopbackIpv4.WithPort(_port),
                CreateClient,
                KentridgeMultiplayerGameplayReplication.Descriptors);
            Require(_client.Application.CompleteBoot(), _role + " boot");
            bool joinInProgress = _role == "client-d" && _attempt == 1;
            Require(_client.Application.RequestJoin(new JoinSessionRequest(
                new JoinRequest(new GameSessionId(SessionValue), _role, Protocol, Content, joinInProgress))),
                _role + " join request");
        }

        private void TickHostStart()
        {
            if (_startRequested || !_authority.Application.TryCapturePartyScreen(out PartyScreenPresentationSnapshot party))
                return;

            int requiredMembers = _attempt == 1
                ? _configuration.Capacity - 1
                : _configuration.Capacity;
            if (party.Members.Count != requiredMembers || !party.CanStart) return;

            if (_attempt == 1)
            {
                Require(_authority.Application.RequestPartyStart(), "authority initial party start");
                EnsureMutationFixture();
            }
            else
            {
                Require(_authority.Application.RequestPartyResume(SaveValue), "authority resumed party start");
            }
            _startRequested = true;
        }

        private void TickMilestones(Game.Application.Runtime.ApplicationFlowCoordinator application)
        {
            if (!application.TryCapturePartyScreen(out PartyScreenPresentationSnapshot party)) return;
            if (!_joinedReported && TryFindLocal(party, out PartyMemberPresentationSnapshot local))
            {
                VerifyOrCaptureLocalIdentity(local);
                _joinedReported = true;
                Emit(new Milestone
                {
                    name = "party-joined",
                    role = _role,
                    sessionId = party.SessionId.Value,
                    memberId = local.MemberId.Value,
                    characterId = local.CharacterId.Value,
                    slot = local.Slot.Value,
                    rosterCount = party.Members.Count,
                    configuredCapacity = _configuration.Capacity,
                    processAttempt = _attempt,
                    transportMode = "process-local-utp"
                });
            }

            ApplicationFlowSnapshot flow = application.Snapshot;
            if (flow.Lifecycle != ApplicationLifecycle.InGame || !flow.GameplayReady ||
                !TryFindLocal(party, out PartyMemberPresentationSnapshot localReady) ||
                !localReady.GameplayReady ||
                localReady.Connection != MemberConnectionPresentationState.Connected)
                return;

            IGameplayReplicationReadState readState = _authority != null
                ? _authority.ReadState
                : _client?.ReadState;
            if (readState == null || !readState.GameplayReady) return;

            if (_attempt == 1)
            {
                if (_authority != null) EnsureMutationFixture();
                if (_role == "client-a") TrySendMutationInput();
                TickMutationMilestone(party, readState);
                if (_authority != null)
                {
                    TickCapacityMilestone(party);
                    TickRepeatedReconnectAndSave();
                }
                else if (_role == "client-d")
                {
                    TickJoinInProgressMilestone(party, localReady, readState);
                }
                return;
            }

            bool repeatedReconnectAttempt = _role == "client-a" && (_attempt == 2 || _attempt == 3);
            if (repeatedReconnectAttempt)
            {
                TickReconnectCurrentState(party, localReady, readState);
                return;
            }

            bool freshRehostClient = _client != null &&
                ((_role == "client-a" && _attempt >= 4) || (_role != "client-a" && _attempt >= 2));
            if (_authority != null || freshRehostClient)
                TickRehostCurrentState(party, localReady, readState);
        }

        private void EnsureMutationFixture()
        {
            if (_fixtureInitialized || _campaignGraph?.Current?.Session == null) return;

            KentridgeCampaignSession session = _campaignGraph.Current.Session;
            var inventory = new InventoryTransactionsAdapter(
                session.InventoryAuthority,
                session.Inventory,
                session.InventoryState);
            var bindings = new CharacterInventoryBindings();
            for (int slot = 0; slot < _configuration.Capacity; slot++)
            {
                if (!bindings.TryBind(
                        KentridgeMultiplayerCharacterRoster.CharacterIdForSlot(slot),
                        session.PlayerInventoryId))
                    throw new InvalidOperationException("Failed to bind release validation inventory for slot " + slot + ".");
            }

            var transfer = new WorldObjectLootAdapter(inventory, bindings);
            _contentionPickup = new ItemPickupObject(
                new WorldObjectId(ContentionObjectValue),
                ContentionPosition,
                new WorldItemPayload(KentridgeWellQuestDefinition.RewardItemId, 1),
                transfer);
            if (!_worldObjects.TryRegister(_contentionPickup))
                throw new InvalidOperationException("Failed to register System25 release mutation pickup.");

            var questObservations = new KentridgeWorldInteractionQuestObservationAdapter(
                observation => _campaignGraph.Current.ObserveQuest(observation));
            var interactions = new InteractionClickedProcessor(
                _actors.Characters,
                _worldObjects,
                questObservations);
            _gameplayCommands = new KentridgeAuthoritativeGameplayCommandSink(
                interactions,
                new KentridgeAuthoritativeCombatActionSink(() => null));
            _inputRouter = new KentridgeAuthoritativePlayerInputRouter(
                () => _authority?.PartySession,
                _gameplayCommands);
            _fixtureInitialized = true;
        }

        private void TrySendMutationInput()
        {
            if (_mutationInputSent) return;
            ClientNetworkRuntime network = _client?.UtpFormation.ActiveClient;
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
                throw new InvalidOperationException("client-a failed to send the production release mutation input.");
            network.FlushSends();
            _mutationInputSent = true;
        }

        private void TickMutationMilestone(
            PartyScreenPresentationSnapshot party,
            IGameplayReplicationReadState readState)
        {
            if (_mutationReported) return;
            if (_authority != null)
            {
                if (!_fixtureInitialized || _inputRouter == null || _inputRouter.AppliedInputs < 1 ||
                    _contentionPickup == null || _contentionPickup.Enabled ||
                    _campaignGraph?.Current?.Session == null)
                    return;
                int directQuantity = _campaignGraph.Current.Session.Inventory.Count(
                    _campaignGraph.Current.Session.PlayerInventoryId,
                    new ItemRef(KentridgeWellQuestDefinition.RewardItemId));
                if (directQuantity != 1)
                    throw new InvalidOperationException("Authoritative release mutation did not conserve exactly one reward item.");
            }

            if (!TryReadMutationState(readState, out int quantity, out string progressionState)) return;
            if (quantity != 1 || !string.Equals(progressionState, "Completed", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    _role + " release mutation diverged: quantity=" + quantity + " progression=" + progressionState + ".");

            _mutationReported = true;
            Emit(new Milestone
            {
                name = "release-mutated-state",
                role = _role,
                sessionId = SessionValue,
                rosterCount = party.Members.Count,
                configuredCapacity = _configuration.Capacity,
                quantity = quantity,
                progressionState = progressionState,
                revision = readState.Revision.Value.ToString(CultureInfo.InvariantCulture),
                processAttempt = _attempt
            });
        }

        private void TickCapacityMilestone(PartyScreenPresentationSnapshot party)
        {
            if (_capacityReported || !_mutationReported || party.Members.Count != _configuration.Capacity) return;
            PartySessionStateCapture state = _authority.PartySession.CaptureState();
            if (state.Members.Count != _configuration.Capacity) return;
            RequireUniqueDurableTopology(state);
            _capacityReported = true;
            Emit(new Milestone
            {
                name = "configured-capacity-full",
                role = _role,
                sessionId = SessionValue,
                configuredCapacity = _configuration.Capacity,
                rosterCount = state.Members.Count,
                signature = SemanticIdentitySignature(state),
                processAttempt = _attempt
            });
        }

        private void TickJoinInProgressMilestone(
            PartyScreenPresentationSnapshot party,
            PartyMemberPresentationSnapshot local,
            IGameplayReplicationReadState readState)
        {
            if (_jipReported || !_mutationReported || party.Members.Count != _configuration.Capacity) return;
            if (local.Slot.Value != _configuration.Capacity - 1)
                throw new InvalidOperationException(
                    "Join-in-progress did not allocate the final configured slot: " + local.Slot.Value + ".");
            string expectedMember = SessionValue + ":member:" + _configuration.Capacity.ToString(CultureInfo.InvariantCulture);
            if (!string.Equals(local.MemberId.Value, expectedMember, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Join-in-progress durable member allocation was " + local.MemberId.Value + ", expected " + expectedMember + ".");
            CharacterId expectedCharacter = KentridgeMultiplayerCharacterRoster.CharacterIdForSlot(local.Slot.Value);
            if (local.CharacterId != expectedCharacter)
                throw new InvalidOperationException("Join-in-progress CharacterId did not match its production slot allocation.");
            if (!TryReadMutationState(readState, out int quantity, out string progressionState)) return;

            _jipReported = true;
            Emit(new Milestone
            {
                name = "jip-current-state",
                role = _role,
                sessionId = SessionValue,
                memberId = local.MemberId.Value,
                characterId = local.CharacterId.Value,
                slot = local.Slot.Value,
                configuredCapacity = _configuration.Capacity,
                rosterCount = party.Members.Count,
                quantity = quantity,
                progressionState = progressionState,
                processAttempt = _attempt,
                recoveryMode = "current-projections-only"
            });
        }

        private void TickRepeatedReconnectAndSave()
        {
            if (!_capacityReported || _authority?.Continuity == null ||
                !TryFindMemberByApplicant("client-a", out PartyMemberStateCapture durable))
                return;
            if (!_authority.Continuity.TryGetRecovery(durable.MemberId, out RecoverySnapshot recovery)) return;

            if (!_hasRecoveryState)
            {
                _lastRecoveryState = recovery.State;
                _hasRecoveryState = true;
            }
            else if (recovery.State != _lastRecoveryState)
            {
                _lastRecoveryState = recovery.State;
                if (recovery.State == RecoveryState.ConnectionInterrupted)
                {
                    Emit(new Milestone
                    {
                        name = "reconnect-interrupted",
                        role = _role,
                        sessionId = SessionValue,
                        memberId = durable.MemberId.Value,
                        characterId = durable.CharacterId.Value,
                        slot = durable.Slot.Value,
                        reconnectCount = _recoveryCount + 1,
                        recoveryState = recovery.State.ToString(),
                        processAttempt = _attempt
                    });
                }
                else if (recovery.State == RecoveryState.Recovered)
                {
                    _recoveryCount++;
                    Emit(new Milestone
                    {
                        name = "reconnect-recovered",
                        role = _role,
                        sessionId = SessionValue,
                        memberId = durable.MemberId.Value,
                        characterId = durable.CharacterId.Value,
                        slot = durable.Slot.Value,
                        reconnectCount = _recoveryCount,
                        recoveryState = recovery.State.ToString(),
                        processAttempt = _attempt
                    });
                }
            }

            if (_saveReported || _recoveryCount < 2) return;
            PartySessionStateCapture state = _authority.PartySession.CaptureState();
            RequireUniqueDurableTopology(state);
            string signature = SemanticIdentitySignature(state);
            SaveIdentitySignature(signature);
            Require(_authority.Session.Capture(), "authority persisted rehost capture");
            IReadOnlyList<SessionSaveMetadata> saves = _persistence.ListSaves();
            bool found = false;
            for (int i = 0; i < saves.Count; i++)
                found |= string.Equals(saves[i].SaveId.Value, SaveValue, StringComparison.Ordinal);
            if (!found) throw new InvalidOperationException("System16 did not publish the expected multiplayer rehost save.");

            _saveReported = true;
            Emit(new Milestone
            {
                name = "release-save-published",
                role = _role,
                sessionId = SessionValue,
                saveId = SaveValue,
                configuredCapacity = _configuration.Capacity,
                rosterCount = state.Members.Count,
                signature = signature,
                reconnectCount = _recoveryCount,
                processAttempt = _attempt
            });
        }

        private void TickReconnectCurrentState(
            PartyScreenPresentationSnapshot party,
            PartyMemberPresentationSnapshot local,
            IGameplayReplicationReadState readState)
        {
            if (_reconnectReported) return;
            VerifyOrCaptureLocalIdentity(local);
            if (party.Members.Count != _configuration.Capacity ||
                !TryReadMutationState(readState, out int quantity, out string progressionState))
                return;
            if (quantity != 1 || !string.Equals(progressionState, "Completed", StringComparison.Ordinal))
                throw new InvalidOperationException("Repeated reconnect did not recover the current mutation state.");

            _reconnectReported = true;
            Emit(new Milestone
            {
                name = "reconnect-current-state",
                role = _role,
                sessionId = SessionValue,
                memberId = local.MemberId.Value,
                characterId = local.CharacterId.Value,
                slot = local.Slot.Value,
                configuredCapacity = _configuration.Capacity,
                rosterCount = party.Members.Count,
                quantity = quantity,
                progressionState = progressionState,
                processAttempt = _attempt,
                transportMode = "fresh-client-process",
                recoveryMode = "current-projections-only"
            });
        }

        private void TickRehostCurrentState(
            PartyScreenPresentationSnapshot party,
            PartyMemberPresentationSnapshot local,
            IGameplayReplicationReadState readState)
        {
            if (_rehostReported || party.Members.Count != _configuration.Capacity) return;
            VerifyOrCaptureLocalIdentity(local);
            if (!TryReadMutationState(readState, out int quantity, out string progressionState)) return;
            if (quantity != 1 || !string.Equals(progressionState, "Completed", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Persisted rehost did not restore the saved inventory/progression mutation.");

            string signature = string.Empty;
            if (_authority != null)
            {
                PartySessionStateCapture state = _authority.PartySession.CaptureState();
                RequireUniqueDurableTopology(state);
                signature = SemanticIdentitySignature(state);
                RequireSavedIdentitySignature(signature);
            }

            _rehostReported = true;
            Emit(new Milestone
            {
                name = "rehost-current-state",
                role = _role,
                sessionId = SessionValue,
                memberId = local.MemberId.Value,
                characterId = local.CharacterId.Value,
                slot = local.Slot.Value,
                configuredCapacity = _configuration.Capacity,
                rosterCount = party.Members.Count,
                quantity = quantity,
                progressionState = progressionState,
                signature = signature,
                processAttempt = _attempt,
                transportMode = "fresh-process-utp",
                recoveryMode = "restored-system16-current-projections"
            });
        }

        private IReadOnlyList<ISessionSnapshotContributor> BuildPersistenceContributors()
        {
            var contributors = new List<ISessionSnapshotContributor>
            {
                new KentridgePartyPersistenceContributor(() =>
                    _authority?.PartySession ?? throw new InvalidOperationException("Authority party is unavailable for persistence."))
            };
            IReadOnlyList<ISessionSnapshotContributor> campaign = KentridgeCampaignPersistenceContributors.Create(() =>
                _campaignGraph?.Current?.Session ?? throw new InvalidOperationException("Campaign session is unavailable for persistence."));
            for (int i = 0; i < campaign.Count; i++) contributors.Add(campaign[i]);
            return contributors;
        }

        private bool TryFindMemberByApplicant(string applicantKey, out PartyMemberStateCapture member)
        {
            PartySessionStateCapture state = _authority.PartySession.CaptureState();
            for (int i = 0; i < state.Members.Count; i++)
            {
                if (!string.Equals(state.Members[i].ApplicantKey, applicantKey, StringComparison.Ordinal)) continue;
                member = state.Members[i];
                return true;
            }
            member = default;
            return false;
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
            return new KentridgeSessionRuntimeGraphFactory(
                content.Blueprint,
                generation,
                KentridgeCampaignRealizationFacts.FromVoxelGeneration(generation, 1),
                _actors,
                ImmediatePresentation.Instance);
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

        private static KentridgeMultiplayerApplicationDependencies BuildApplicationDependencies(ISessionSaveCatalog saves) =>
            new KentridgeMultiplayerApplicationDependencies(
                saves,
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

        private static void RequireUniqueDurableTopology(PartySessionStateCapture state)
        {
            var members = new HashSet<PartyMemberId>();
            var slots = new HashSet<int>();
            var characters = new HashSet<CharacterId>();
            var applicants = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < state.Members.Count; i++)
            {
                PartyMemberStateCapture member = state.Members[i];
                if (!members.Add(member.MemberId) || !slots.Add(member.Slot.Value) ||
                    !applicants.Add(member.ApplicantKey) || !member.HasCharacter ||
                    !characters.Add(member.CharacterId))
                    throw new InvalidOperationException("Configured-capacity roster contains duplicate or incomplete durable identity.");
            }
        }

        private static string SemanticIdentitySignature(PartySessionStateCapture state)
        {
            var ordered = new List<PartyMemberStateCapture>(state.Members.Count);
            for (int i = 0; i < state.Members.Count; i++) ordered.Add(state.Members[i]);
            ordered.Sort((left, right) => StringComparer.Ordinal.Compare(left.ApplicantKey, right.ApplicantKey));
            var text = new StringBuilder();
            for (int i = 0; i < ordered.Count; i++)
            {
                if (i > 0) text.Append(';');
                PartyMemberStateCapture member = ordered[i];
                text.Append(member.ApplicantKey).Append('=').Append(member.MemberId.Value)
                    .Append('/').Append(member.Slot.Value.ToString(CultureInfo.InvariantCulture))
                    .Append('/').Append(member.CharacterId.Value);
            }
            return text.ToString();
        }

        private void VerifyOrCaptureLocalIdentity(PartyMemberPresentationSnapshot local)
        {
            string observed = local.MemberId.Value + "|" +
                              local.Slot.Value.ToString(CultureInfo.InvariantCulture) + "|" +
                              local.CharacterId.Value;
            string path = Path.Combine(RequireStateRoot(), "observed-durable-identity.txt");
            if (_attempt == 1)
            {
                if (File.Exists(path))
                {
                    string prior = File.ReadAllText(path).Trim();
                    if (!string.Equals(prior, observed, StringComparison.Ordinal))
                        throw new InvalidOperationException("First-process durable identity changed within one release run.");
                }
                else
                {
                    File.WriteAllText(path, observed + Environment.NewLine);
                }
                return;
            }

            if (!File.Exists(path))
                throw new InvalidOperationException("Fresh process has no prior durable identity observation to compare.");
            string expected = File.ReadAllText(path).Trim();
            if (!string.Equals(expected, observed, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Fresh process durable identity changed. expected=" + expected + " observed=" + observed + ".");
        }

        private void SaveIdentitySignature(string signature)
        {
            File.WriteAllText(
                Path.Combine(RequireStateRoot(), "saved-roster-signature.txt"),
                signature + Environment.NewLine);
        }

        private void RequireSavedIdentitySignature(string observed)
        {
            string path = Path.Combine(RequireStateRoot(), "saved-roster-signature.txt");
            if (!File.Exists(path))
                throw new InvalidOperationException("Fresh authority has no saved roster signature evidence.");
            string expected = File.ReadAllText(path).Trim();
            if (!string.Equals(expected, observed, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Persisted roster identity changed across authority processes. expected=" + expected +
                    " observed=" + observed + ".");
        }

        private static string RequireStateRoot()
        {
            string root = Environment.GetEnvironmentVariable("VOXEL_VALIDATION_STATE_ROOT") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(root))
                throw new InvalidOperationException("VOXEL_VALIDATION_STATE_ROOT is required for release persistence validation.");
            Directory.CreateDirectory(root);
            return root;
        }

        private static bool TryReadMutationState(
            IGameplayReplicationReadState readState,
            out int quantity,
            out string progressionState)
        {
            quantity = 0;
            progressionState = string.Empty;
            if (readState == null ||
                !readState.TryGetProjection(
                    KentridgeMultiplayerGameplayReplication.InventoryDescriptor.Id,
                    out GameplayProjectionState inventory) ||
                !readState.TryGetProjection(
                    KentridgeMultiplayerGameplayReplication.ProgressionDescriptor.Id,
                    out GameplayProjectionState progression))
                return false;

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
            if (!foundItem) return false;

            string key = "quest/" + KentridgeWellQuestDefinition.QuestId +
                         "/objective/" + WellObjectiveValue + "/state";
            for (int i = 0; i < progression.Entries.Count; i++)
            {
                GameplayProjectionEntry entry = progression.Entries[i];
                if (!string.Equals(entry.Key, key, StringComparison.Ordinal)) continue;
                progressionState = entry.Value;
                return true;
            }
            return false;
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

        private static int ParseCapacity(string[] args)
        {
            const string flag = "-gamesystem25-capacity";
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], flag, StringComparison.Ordinal)) continue;
                if (int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value >= 5)
                    return value;
                throw new InvalidOperationException(flag + " must be at least 5 for the release scenario.");
            }
            throw new InvalidOperationException(flag + " is required.");
        }

        private static void Require(ApplicationOperationResult result, string operation)
        {
            if (!result.Succeeded)
                throw new InvalidOperationException(operation + " failed: " + result.Failure + " " + result.Detail);
        }

        private static void Require(GameSessionOperationResult result, string operation)
        {
            if (!result.Succeeded)
                throw new InvalidOperationException(operation + " failed: " + result.Failure + " " + result.Diagnostic);
        }

        private static void Emit(Milestone milestone) =>
            Debug.Log(MilestonePrefix + JsonUtility.ToJson(milestone));

        private void Fail(Exception exception)
        {
            _failure = exception.ToString();
            Debug.LogError("GAMESYSTEM25_MULTIPLAYER_RELEASE FAIL role=" + _role + " " + exception);
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
            public string saveId;
            public int slot;
            public int rosterCount;
            public int configuredCapacity;
            public int port;
            public int processAttempt;
            public int reconnectCount;
            public int quantity;
            public string progressionState;
            public string revision;
            public string signature;
            public string recoveryState;
            public string recoveryMode;
            public string transportMode;
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
                    _owner = owner;
                    _previous = previous;
                    Context = context;
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
