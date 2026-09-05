using System;
using System.Collections.Generic;
using System.IO;
using Game.Characters.Api;
using Game.Composition.Campaign.Runtime;
using Game.Composition.Kentridge.Playable;
using Game.Composition.Kentridge.Runtime;
using Game.Encounters.Api;
using Game.Inventory.Api;
using Game.Persistence.Api;
using Game.Persistence.Runtime;
using Game.Progression.Api;
using Game.SessionOrchestration.Api;
using Game.SessionOrchestration.Runtime;
using Game.WorldBuilder.Api;
using Game.WorldObjects.Api;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Composition-only adapter between System14 session lifetime and System16 persistence. Every
    /// section delegates capture/restore to its owning gameplay API; no Unity object or presentation
    /// state enters the persisted envelope.
    /// </summary>
    internal sealed class KentridgeProductionPersistenceBridge :
        ISessionPersistenceBridge,
        ISessionCaptureBarrier,
        ISessionRestoreGraphFactory
    {
        private readonly KentridgePlayableSlice _slice;
        private readonly KentridgeForestBanditEncounter _forest;
        private readonly KentridgeProductionWorldInteraction _worldInteraction;
        private readonly ISessionSnapshotContributor[] _contributors;
        private KentridgeSessionRuntimeGraph _operationGraph;
        private SessionSaveId _armedSaveId;
        private string _armedLabel = string.Empty;
        private ulong _captureRevision;

        public SessionPersistenceService Service { get; }
        public string LastPublishedSaveId { get; private set; } = string.Empty;

        public KentridgeProductionPersistenceBridge(
            KentridgePlayableSlice slice,
            KentridgeForestBanditEncounter forest,
            KentridgeProductionWorldInteraction worldInteraction,
            ISessionSaveStore store)
        {
            _slice = slice ?? throw new ArgumentNullException(nameof(slice));
            _forest = forest ?? throw new ArgumentNullException(nameof(forest));
            _worldInteraction = worldInteraction ?? throw new ArgumentNullException(nameof(worldInteraction));
            if (store == null) throw new ArgumentNullException(nameof(store));

            _contributors = new ISessionSnapshotContributor[]
            {
                new DelegateSessionSnapshotContributor<CampaignProgressSnapshot>(
                    "campaign-progress",
                    "KentridgeCampaignProgress",
                    1,
                    100,
                    true,
                    () => RequireGraph().Session.Runtime.CaptureProgress(),
                    KentridgeProductionSnapshotCodec.EncodeCampaign,
                    KentridgeProductionSnapshotCodec.DecodeCampaign,
                    ValidateCampaign,
                    RestoreCampaign),
                new DelegateSessionSnapshotContributor<InventoryStateCapture>(
                    "inventory",
                    "InventoryState",
                    1,
                    200,
                    true,
                    () => RequireGraph().Session.InventoryState.CaptureState(),
                    KentridgeProductionSnapshotCodec.EncodeInventory,
                    KentridgeProductionSnapshotCodec.DecodeInventory,
                    ValidateInventory,
                    RestoreInventory),
                new DelegateSessionSnapshotContributor<KentridgePlayerSemanticState>(
                    "player-character",
                    "KentridgePlayerSemanticState",
                    1,
                    300,
                    true,
                    CapturePlayer,
                    KentridgeProductionSnapshotCodec.EncodePlayer,
                    KentridgeProductionSnapshotCodec.DecodePlayer,
                    ValidatePlayer,
                    RestorePlayer),
                new DelegateSessionSnapshotContributor<EncounterRegistrySnapshot>(
                    "forest-encounter",
                    "EncounterRegistryState",
                    1,
                    400,
                    true,
                    () => _forest.CaptureEncounterState(),
                    KentridgeProductionSnapshotCodec.EncodeEncounter,
                    KentridgeProductionSnapshotCodec.DecodeEncounter,
                    ValidateEncounter,
                    RestoreEncounter),
                new DelegateSessionSnapshotContributor<WorldObjectStateSnapshot[]>(
                    "world-objects",
                    "WorldObjectState",
                    1,
                    500,
                    true,
                    CaptureWorldObjects,
                    KentridgeProductionSnapshotCodec.EncodeWorldObjects,
                    KentridgeProductionSnapshotCodec.DecodeWorldObjects,
                    ValidateWorldObjects,
                    RestoreWorldObjects)
            };

            Service = new SessionPersistenceService(this, _contributors, store, this);
        }

        public void ArmCapture(SessionSaveId saveId, string label)
        {
            if (!saveId.IsValid) throw new ArgumentException("A semantic save id is required.", nameof(saveId));
            _armedSaveId = saveId;
            _armedLabel = label ?? string.Empty;
        }

        public void Restore(
            GameSessionIdentity identity,
            string restoreSourceId,
            ISessionRuntimeGraph graph)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            KentridgeSessionRuntimeGraph kentridge = RequireKentridgeGraph(graph);
            _operationGraph = kentridge;
            try
            {
                SessionPersistenceResult result = Service.Restore(new SessionRestoreRequest(
                    new SessionSaveId(restoreSourceId),
                    new SessionContentId(KentridgeProductionCompositionRoot.ContentId),
                    new SessionWorldId(identity.WorldId)));
                if (!result.Succeeded)
                    throw new SessionCompositionException(
                        GameSessionFailure.RestoreFailed,
                        "Kentridge persistence restore failed: " + result.Failure + " " + result.Detail);
            }
            finally
            {
                _operationGraph = null;
            }
        }

        public void Capture(GameSessionIdentity identity, ISessionRuntimeGraph graph)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (!_armedSaveId.IsValid)
                throw new InvalidOperationException("Kentridge capture requires an explicitly armed semantic save id.");

            KentridgeSessionRuntimeGraph kentridge = RequireKentridgeGraph(graph);
            _operationGraph = kentridge;
            try
            {
                SessionPersistenceResult result = Service.CaptureAndSave(new SessionCaptureRequest(
                    _armedSaveId,
                    identity.SessionId,
                    new SessionContentId(KentridgeProductionCompositionRoot.ContentId),
                    new SessionWorldId(identity.WorldId),
                    DateTime.UtcNow.Ticks,
                    _armedLabel));
                if (!result.Succeeded)
                    throw new InvalidOperationException(
                        "Kentridge persistence capture failed: " + result.Failure + " " + result.Detail);
                LastPublishedSaveId = _armedSaveId.Value;
            }
            finally
            {
                _operationGraph = null;
                _armedSaveId = default;
                _armedLabel = string.Empty;
            }
        }

        public bool TryEnter(out ISessionCaptureLease lease)
        {
            KentridgeSessionRuntimeGraph graph = RequireGraph();
            if (graph.IsDisposed)
            {
                lease = null;
                return false;
            }
            _captureRevision++;
            lease = new CaptureLease(_captureRevision);
            return true;
        }

        public bool TryCreate(
            GameSessionSnapshotHeader header,
            out ISessionRestoreGraph graph,
            out string error)
        {
            KentridgeSessionRuntimeGraph current = RequireGraph();
            if (current.IsDisposed)
            {
                graph = null;
                error = "Fresh Kentridge graph was disposed before restore composition.";
                return false;
            }
            graph = new RestoreGraph(current, _contributors);
            error = string.Empty;
            return true;
        }

        private KentridgeSessionRuntimeGraph RequireGraph()
        {
            KentridgeSessionRuntimeGraph graph = _operationGraph ?? _slice.SessionFactory?.Current;
            if (graph == null || graph.IsDisposed)
                throw new InvalidOperationException("No live Kentridge session graph is available for persistence composition.");
            return graph;
        }

        private static KentridgeSessionRuntimeGraph RequireKentridgeGraph(ISessionRuntimeGraph graph)
        {
            if (!(graph is KentridgeSessionRuntimeGraph kentridge))
                throw new InvalidOperationException("Kentridge persistence received a non-Kentridge runtime graph.");
            return kentridge;
        }

        private SessionContributorResult ValidateCampaign(CampaignProgressSnapshot state)
        {
            if (state == null) return SessionContributorResult.Reject("Campaign progress is required.");
            if (RequireGraph().Session.Runtime.HasActiveCutscene)
                return SessionContributorResult.Reject("Restore target must be a fresh graph without an active cutscene.");
            return SessionContributorResult.Success();
        }

        private SessionContributorResult RestoreCampaign(CampaignProgressSnapshot state)
        {
            try
            {
                RequireGraph().Session.Runtime.RestoreProgress(state);
                return SessionContributorResult.Success();
            }
            catch (Exception ex)
            {
                return SessionContributorResult.Reject(ex.Message);
            }
        }

        private SessionContributorResult ValidateInventory(InventoryStateCapture state)
        {
            KentridgeCampaignSession session = RequireGraph().Session;
            for (int i = 0; i < state.Inventories.Count; i++)
            {
                InventorySnapshot inventory = state.Inventories[i];
                if (!session.Inventory.TryGetDescriptor(inventory.Id, out _))
                    return SessionContributorResult.Reject("Unknown inventory in save: " + inventory.Id + ".");
                for (int e = 0; e < inventory.Entries.Count; e++)
                    if (!session.Inventory.TryGetDefinition(inventory.Entries[e].Item, out _))
                        return SessionContributorResult.Reject("Unknown item in save: " + inventory.Entries[e].Item + ".");
            }
            return SessionContributorResult.Success();
        }

        private SessionContributorResult RestoreInventory(InventoryStateCapture state)
        {
            InventoryFailureReason failure = RequireGraph().Session.InventoryState.RestoreState(state);
            return failure == InventoryFailureReason.None
                ? SessionContributorResult.Success()
                : SessionContributorResult.Reject("Inventory restore failed: " + failure + ".");
        }

        private KentridgePlayerSemanticState CapturePlayer()
        {
            KentridgeCharacterHost host = _slice.CharacterHost
                ?? throw new InvalidOperationException("Kentridge player character host is unavailable.");
            CharacterId playerId = host.PlayerCharacterId;
            if (!host.Characters.TryGet(playerId, out CharacterSnapshot snapshot))
                throw new InvalidOperationException("Kentridge player character is unavailable for capture.");
            return new KentridgePlayerSemanticState(
                snapshot.Id,
                snapshot.Lifecycle,
                snapshot.Kinematics);
        }

        private SessionContributorResult ValidatePlayer(KentridgePlayerSemanticState state)
        {
            KentridgeCharacterHost host = _slice.CharacterHost;
            if (host == null) return SessionContributorResult.Reject("Fresh Kentridge player host is unavailable.");
            if (state.CharacterId != host.PlayerCharacterId)
                return SessionContributorResult.Reject(
                    "Saved player id " + state.CharacterId + " does not match fresh production id " + host.PlayerCharacterId + ".");
            return SessionContributorResult.Success();
        }

        private SessionContributorResult RestorePlayer(KentridgePlayerSemanticState state)
        {
            KentridgeCharacterHost host = _slice.CharacterHost;
            host.RestorePlayerKinematics(state.Kinematics);
            if (state.Lifecycle == CharacterLifecycleState.Defeated)
            {
                CharacterRegistryFailure failure = host.Characters.MarkDefeated(state.CharacterId, out _);
                if (failure != CharacterRegistryFailure.None &&
                    failure != CharacterRegistryFailure.CharacterAlreadyDefeated)
                    return SessionContributorResult.Reject("Player lifecycle restore failed: " + failure + ".");
            }
            return SessionContributorResult.Success();
        }

        private SessionContributorResult ValidateEncounter(EncounterRegistrySnapshot state)
        {
            if (state == null || state.Encounters == null)
                return SessionContributorResult.Reject("Encounter snapshot is required.");
            for (int i = 0; i < state.Encounters.Count; i++)
            {
                EncounterSnapshot encounter = state.Encounters[i];
                if (encounter == null || !encounter.Id.IsValid)
                    return SessionContributorResult.Reject("Encounter snapshot contains an invalid encounter.");
            }
            return SessionContributorResult.Success();
        }

        private SessionContributorResult RestoreEncounter(EncounterRegistrySnapshot state)
        {
            EncounterMutationFailure failure = _forest.RestoreEncounterState(state);
            return failure == EncounterMutationFailure.None
                ? SessionContributorResult.Success()
                : SessionContributorResult.Reject("Forest encounter restore failed: " + failure + ".");
        }

        private WorldObjectStateSnapshot[] CaptureWorldObjects()
        {
            IReadOnlyList<WorldObjectStateSnapshot> state =
                _worldInteraction.CaptureWorldObjectState(RequireGraph());
            var copy = new WorldObjectStateSnapshot[state.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = state[i];
            return copy;
        }

        private static SessionContributorResult ValidateWorldObjects(WorldObjectStateSnapshot[] state)
        {
            if (state == null) return SessionContributorResult.Reject("WorldObject snapshot is required.");
            var ids = new HashSet<WorldObjectId>();
            for (int i = 0; i < state.Length; i++)
            {
                if (!state[i].ObjectId.IsValid || !ids.Add(state[i].ObjectId))
                    return SessionContributorResult.Reject("WorldObject snapshot contains an invalid or duplicate id.");
            }
            return SessionContributorResult.Success();
        }

        private SessionContributorResult RestoreWorldObjects(WorldObjectStateSnapshot[] state)
        {
            WorldInteractionResult result =
                _worldInteraction.RestoreWorldObjectState(RequireGraph(), state);
            return result.Succeeded
                ? SessionContributorResult.Success()
                : SessionContributorResult.Reject("WorldObject restore failed: " + result.Failure + ".");
        }

        private sealed class CaptureLease : ISessionCaptureLease
        {
            public ulong AuthoritativeRevision { get; }
            public CaptureLease(ulong revision) => AuthoritativeRevision = revision;
            public void Dispose() { }
        }

        private sealed class RestoreGraph : ISessionRestoreGraph
        {
            private readonly KentridgeSessionRuntimeGraph _graph;
            private readonly IReadOnlyList<ISessionSnapshotContributor> _contributors;
            private bool _completed;

            public RestoreGraph(
                KentridgeSessionRuntimeGraph graph,
                IReadOnlyList<ISessionSnapshotContributor> contributors)
            {
                _graph = graph ?? throw new ArgumentNullException(nameof(graph));
                _contributors = contributors ?? throw new ArgumentNullException(nameof(contributors));
            }

            public IReadOnlyList<ISessionSnapshotContributor> Contributors => _contributors;

            public void CompleteRestore()
            {
                if (_completed) return;
                _graph.MarkRestoredFromPersistence();
                _graph.Session.SynchronizeRewards();
                _completed = true;
            }

            public void AbortRestore()
            {
                // The owning GameSessionOrchestrator disposes the fresh graph when Prepare fails.
            }
        }
    }

    internal readonly struct KentridgePlayerSemanticState
    {
        public CharacterId CharacterId { get; }
        public CharacterLifecycleState Lifecycle { get; }
        public CharacterKinematicState Kinematics { get; }

        public KentridgePlayerSemanticState(
            CharacterId characterId,
            CharacterLifecycleState lifecycle,
            CharacterKinematicState kinematics)
        {
            if (!characterId.IsValid) throw new ArgumentException("Player character id is required.", nameof(characterId));
            CharacterId = characterId;
            Lifecycle = lifecycle;
            Kinematics = kinematics;
        }
    }

    internal static class KentridgeProductionSnapshotCodec
    {
        public static byte[] EncodeCampaign(CampaignProgressSnapshot state) =>
            Write(writer =>
            {
                writer.Write(state.CompletedCutscenes.Count);
                for (int i = 0; i < state.CompletedCutscenes.Count; i++)
                    writer.Write(state.CompletedCutscenes[i].Id);
                WriteStrings(writer, state.JoinedPartyMembers);
                WriteStrings(writer, state.GrantedSpells);
                writer.Write(state.Progression != null);
                if (state.Progression != null) WriteProgression(writer, state.Progression);
            });

        public static CampaignProgressSnapshot DecodeCampaign(byte[] payload) =>
            Read(payload, reader =>
            {
                int completedCount = ReadCount(reader);
                var completed = new CutsceneRef[completedCount];
                for (int i = 0; i < completed.Length; i++) completed[i] = new CutsceneRef(reader.ReadString());
                string[] joined = ReadStrings(reader);
                string[] spells = ReadStrings(reader);
                ProgressionSnapshot progression = reader.ReadBoolean() ? ReadProgression(reader) : null;
                return new CampaignProgressSnapshot(completed, joined, spells, progression);
            });

        public static byte[] EncodeInventory(InventoryStateCapture state) =>
            Write(writer =>
            {
                writer.Write(state.Inventories.Count);
                for (int i = 0; i < state.Inventories.Count; i++)
                {
                    InventorySnapshot inventory = state.Inventories[i];
                    writer.Write(inventory.Id.Value);
                    writer.Write(inventory.Revision);
                    writer.Write(inventory.Entries.Count);
                    for (int e = 0; e < inventory.Entries.Count; e++)
                    {
                        writer.Write(inventory.Entries[e].Item.Id);
                        writer.Write(inventory.Entries[e].Quantity);
                    }
                }
            });

        public static InventoryStateCapture DecodeInventory(byte[] payload) =>
            Read(payload, reader =>
            {
                int inventoryCount = ReadCount(reader);
                var inventories = new InventorySnapshot[inventoryCount];
                for (int i = 0; i < inventories.Length; i++)
                {
                    var id = new InventoryId(reader.ReadString());
                    ulong revision = reader.ReadUInt64();
                    int entryCount = ReadCount(reader);
                    var entries = new InventoryEntry[entryCount];
                    for (int e = 0; e < entries.Length; e++)
                        entries[e] = new InventoryEntry(new ItemRef(reader.ReadString()), reader.ReadInt32());
                    inventories[i] = new InventorySnapshot(id, revision, entries);
                }
                return new InventoryStateCapture(inventories);
            });

        public static byte[] EncodePlayer(KentridgePlayerSemanticState state) =>
            Write(writer =>
            {
                writer.Write(state.CharacterId.Value);
                writer.Write((int)state.Lifecycle);
                WriteKinematics(writer, state.Kinematics);
            });

        public static KentridgePlayerSemanticState DecodePlayer(byte[] payload) =>
            Read(payload, reader => new KentridgePlayerSemanticState(
                new CharacterId(reader.ReadString()),
                (CharacterLifecycleState)reader.ReadInt32(),
                ReadKinematics(reader)));

        public static byte[] EncodeEncounter(EncounterRegistrySnapshot state) =>
            Write(writer =>
            {
                writer.Write(state.Sequence);
                writer.Write(state.Encounters.Count);
                for (int i = 0; i < state.Encounters.Count; i++)
                {
                    EncounterSnapshot encounter = state.Encounters[i];
                    writer.Write(encounter.Id.Value);
                    writer.Write((int)encounter.Definition.CombatPolicy);
                    writer.Write(encounter.Definition.SemanticKind);
                    writer.Write((int)encounter.Lifecycle);
                    writer.Write(encounter.Membership.Participants.Count);
                    for (int p = 0; p < encounter.Membership.Participants.Count; p++)
                    {
                        EncounterParticipant participant = encounter.Membership.Participants[p];
                        writer.Write(participant.CharacterId.Value);
                        writer.Write((int)participant.Ownership);
                        writer.Write(participant.Role);
                    }
                    writer.Write(encounter.Resolution.HasValue);
                    if (encounter.Resolution.HasValue)
                    {
                        writer.Write((int)encounter.Resolution.Value.Result);
                        writer.Write(encounter.Resolution.Value.Reason);
                    }
                    writer.Write(encounter.ActivationCause);
                    writer.Write(encounter.RealizationId);
                    writer.Write(encounter.Revision);
                }
            });

        public static EncounterRegistrySnapshot DecodeEncounter(byte[] payload) =>
            Read(payload, reader =>
            {
                ulong sequence = reader.ReadUInt64();
                int encounterCount = ReadCount(reader);
                var encounters = new EncounterSnapshot[encounterCount];
                for (int i = 0; i < encounters.Length; i++)
                {
                    var definition = new EncounterDefinition(
                        new EncounterId(reader.ReadString()),
                        (EncounterCombatPolicy)reader.ReadInt32(),
                        reader.ReadString());
                    EncounterLifecycleState lifecycle = (EncounterLifecycleState)reader.ReadInt32();
                    int participantCount = ReadCount(reader);
                    var participants = new EncounterParticipant[participantCount];
                    for (int p = 0; p < participants.Length; p++)
                        participants[p] = new EncounterParticipant(
                            new CharacterId(reader.ReadString()),
                            (EncounterParticipantOwnership)reader.ReadInt32(),
                            reader.ReadString());
                    EncounterResolution? resolution = null;
                    if (reader.ReadBoolean())
                        resolution = new EncounterResolution(
                            (EncounterResolutionResult)reader.ReadInt32(),
                            reader.ReadString());
                    string activationCause = reader.ReadString();
                    string realizationId = reader.ReadString();
                    ulong revision = reader.ReadUInt64();
                    encounters[i] = new EncounterSnapshot(
                        definition,
                        lifecycle,
                        new EncounterMembershipSnapshot(participants),
                        resolution,
                        activationCause,
                        realizationId,
                        revision);
                }
                return new EncounterRegistrySnapshot(encounters, sequence);
            });

        public static byte[] EncodeWorldObjects(WorldObjectStateSnapshot[] state) =>
            Write(writer =>
            {
                writer.Write(state.Length);
                for (int i = 0; i < state.Length; i++)
                {
                    WorldObjectStateSnapshot snapshot = state[i];
                    writer.Write(snapshot.ObjectId.Value);
                    writer.Write((int)snapshot.Kind);
                    writer.Write(snapshot.Enabled);
                    writer.Write(snapshot.StateCode);
                    writer.Write(snapshot.Revision);
                }
            });

        public static WorldObjectStateSnapshot[] DecodeWorldObjects(byte[] payload) =>
            Read(payload, reader =>
            {
                int count = ReadCount(reader);
                var state = new WorldObjectStateSnapshot[count];
                for (int i = 0; i < state.Length; i++)
                    state[i] = new WorldObjectStateSnapshot(
                        new WorldObjectId(reader.ReadString()),
                        (WorldObjectKind)reader.ReadInt32(),
                        reader.ReadBoolean(),
                        reader.ReadInt32(),
                        reader.ReadUInt64());
                return state;
            });

        private static void WriteProgression(BinaryWriter writer, ProgressionSnapshot state)
        {
            writer.Write(state.Revision);
            writer.Write(state.CompatibilitySequence);
            WriteStrings(writer, state.AppliedOperationIds);
            writer.Write(state.Quests.Count);
            for (int i = 0; i < state.Quests.Count; i++) WriteQuest(writer, state.Quests[i]);
            writer.Write(state.StandaloneObjectives.Count);
            for (int i = 0; i < state.StandaloneObjectives.Count; i++)
                WriteObjective(writer, state.StandaloneObjectives[i]);
        }

        private static ProgressionSnapshot ReadProgression(BinaryReader reader)
        {
            ulong revision = reader.ReadUInt64();
            long compatibility = reader.ReadInt64();
            string[] applied = ReadStrings(reader);
            int questCount = ReadCount(reader);
            var quests = new QuestProgressSnapshot[questCount];
            for (int i = 0; i < quests.Length; i++) quests[i] = ReadQuest(reader);
            int standaloneCount = ReadCount(reader);
            var standalone = new ObjectiveProgressSnapshot[standaloneCount];
            for (int i = 0; i < standalone.Length; i++) standalone[i] = ReadObjective(reader);
            return new ProgressionSnapshot(revision, quests, standalone, applied, compatibility);
        }

        private static void WriteQuest(BinaryWriter writer, QuestProgressSnapshot quest)
        {
            writer.Write(quest.Id.Value);
            writer.Write((int)quest.State);
            writer.Write(quest.ActiveStepId ?? string.Empty);
            writer.Write(quest.Revision);
            bool hasSteps = quest.Steps.Count > 0;
            writer.Write(hasSteps);
            if (hasSteps)
            {
                writer.Write(quest.Steps.Count);
                for (int s = 0; s < quest.Steps.Count; s++)
                {
                    QuestStepProgressSnapshot step = quest.Steps[s];
                    writer.Write(step.StepId);
                    writer.Write((int)step.State);
                    writer.Write(step.Objectives.Count);
                    for (int o = 0; o < step.Objectives.Count; o++) WriteObjective(writer, step.Objectives[o]);
                }
            }
            else
            {
                writer.Write(quest.Objectives.Count);
                for (int o = 0; o < quest.Objectives.Count; o++) WriteObjective(writer, quest.Objectives[o]);
            }
        }

        private static QuestProgressSnapshot ReadQuest(BinaryReader reader)
        {
            var id = new QuestId(reader.ReadString());
            ProgressionLifecycleState state = (ProgressionLifecycleState)reader.ReadInt32();
            string activeStep = reader.ReadString();
            ulong revision = reader.ReadUInt64();
            bool hasSteps = reader.ReadBoolean();
            int count = ReadCount(reader);
            if (!hasSteps)
            {
                var objectives = new ObjectiveProgressSnapshot[count];
                for (int i = 0; i < objectives.Length; i++) objectives[i] = ReadObjective(reader);
                return new QuestProgressSnapshot(id, state, objectives, revision);
            }

            var steps = new QuestStepProgressSnapshot[count];
            for (int s = 0; s < steps.Length; s++)
            {
                string stepId = reader.ReadString();
                ProgressionLifecycleState stepState = (ProgressionLifecycleState)reader.ReadInt32();
                int objectiveCount = ReadCount(reader);
                var objectives = new ObjectiveProgressSnapshot[objectiveCount];
                for (int o = 0; o < objectives.Length; o++) objectives[o] = ReadObjective(reader);
                steps[s] = new QuestStepProgressSnapshot(stepId, stepState, objectives);
            }
            return new QuestProgressSnapshot(id, state, activeStep, steps, revision);
        }

        private static void WriteObjective(BinaryWriter writer, ObjectiveProgressSnapshot objective)
        {
            writer.Write(objective.Id.Value);
            writer.Write((int)objective.State);
            writer.Write(objective.CurrentCount);
            writer.Write(objective.RequiredCount);
            writer.Write(objective.Revision);
        }

        private static ObjectiveProgressSnapshot ReadObjective(BinaryReader reader) =>
            new ObjectiveProgressSnapshot(
                new ObjectiveId(reader.ReadString()),
                (ProgressionLifecycleState)reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadUInt64());

        private static void WriteKinematics(BinaryWriter writer, CharacterKinematicState state)
        {
            WriteVector(writer, state.Position);
            WriteVector(writer, state.Velocity);
            WriteVector(writer, state.Facing);
        }

        private static CharacterKinematicState ReadKinematics(BinaryReader reader) =>
            new CharacterKinematicState(ReadVector(reader), ReadVector(reader), ReadVector(reader));

        private static void WriteVector(BinaryWriter writer, CharacterVector3 vector)
        {
            writer.Write(vector.X);
            writer.Write(vector.Y);
            writer.Write(vector.Z);
        }

        private static CharacterVector3 ReadVector(BinaryReader reader) =>
            new CharacterVector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

        private static void WriteStrings(BinaryWriter writer, IReadOnlyList<string> values)
        {
            writer.Write(values.Count);
            for (int i = 0; i < values.Count; i++) writer.Write(values[i] ?? string.Empty);
        }

        private static string[] ReadStrings(BinaryReader reader)
        {
            int count = ReadCount(reader);
            var values = new string[count];
            for (int i = 0; i < values.Length; i++) values[i] = reader.ReadString();
            return values;
        }

        private static int ReadCount(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > 65536) throw new InvalidDataException("Snapshot collection count is invalid: " + count + ".");
            return count;
        }

        private static byte[] Write(Action<BinaryWriter> write)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                write(writer);
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static T Read<T>(byte[] payload, Func<BinaryReader, T> read)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            using (var stream = new MemoryStream(payload, false))
            using (var reader = new BinaryReader(stream))
            {
                T value = read(reader);
                if (stream.Position != stream.Length)
                    throw new InvalidDataException("Snapshot payload contains trailing data.");
                return value;
            }
        }
    }
}
