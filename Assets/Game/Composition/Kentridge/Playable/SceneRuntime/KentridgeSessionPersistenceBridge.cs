using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Game.Characters.Api;
using Game.Composition.Campaign.Runtime;
using Game.Composition.Kentridge.Playable;
using Game.Composition.Kentridge.Runtime;
using Game.Cutscenes.Api;
using Game.Encounters.Api;
using Game.Inventory.Api;
using Game.Persistence.Api;
using Game.Persistence.Runtime;
using Game.Progression.Api;
using Game.SessionOrchestration.Api;
using Game.SessionOrchestration.Runtime;
using UnityEngine;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Kentridge-owned adapter between the generic System 16 persistence service and the semantic
    /// owners composed in the production Kentridge session graph. No Unity object, presentation,
    /// input, transport or scene state is serialized.
    /// </summary>
    internal sealed class KentridgeSessionPersistenceBridge :
        ISessionPersistenceBridge,
        ISessionSaveCatalog
    {
        internal const string DefaultSaveId = "kentridge-autosave";
        private static readonly SessionContentId ContentId = new SessionContentId("kentridge.production");

        private readonly KentridgeSessionRuntimeGraphFactory _factory;
        private readonly KentridgeForestBanditEncounter _forest;
        private readonly SessionPersistenceService _service;
        private readonly RestoreFactory _restoreFactory;
        private ulong _captureRevision;
        private KentridgeSessionRuntimeGraph _restoreTarget;

        public KentridgeSessionPersistenceBridge(
            KentridgeSessionRuntimeGraphFactory factory,
            KentridgeForestBanditEncounter forest,
            string saveDirectory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _forest = forest ?? throw new ArgumentNullException(nameof(forest));
            if (string.IsNullOrWhiteSpace(saveDirectory))
                throw new ArgumentException("Kentridge save directory is required.", nameof(saveDirectory));

            _restoreFactory = new RestoreFactory(this);
            ISessionSnapshotContributor[] contributors = CreateContributors();
            _service = new SessionPersistenceService(
                new CaptureBarrier(this),
                contributors,
                new FileSessionSaveStore(saveDirectory),
                _restoreFactory);
        }

        public IReadOnlyList<SessionSaveMetadata> ListSaves() => _service.ListSaves();

        public void Capture(GameSessionIdentity identity, ISessionRuntimeGraph graph)
        {
            KentridgeSessionRuntimeGraph target = RequireGraph(graph);
            if (!ReferenceEquals(target, _factory.Current))
                throw new InvalidOperationException("Only the current Kentridge graph can be captured.");

            SessionPersistenceResult result = _service.CaptureAndSave(
                new SessionCaptureRequest(
                    new SessionSaveId(DefaultSaveId),
                    identity.SessionId,
                    ContentId,
                    new SessionWorldId(identity.WorldId),
                    DateTime.UtcNow.Ticks,
                    "Kentridge production autosave"));
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    "Kentridge save failed: " + result.Failure + " " + result.Detail);
        }

        public void Restore(
            GameSessionIdentity identity,
            string restoreSourceId,
            ISessionRuntimeGraph graph)
        {
            KentridgeSessionRuntimeGraph target = RequireGraph(graph);
            if (!ReferenceEquals(target, _factory.Current))
                throw new InvalidOperationException("Only the freshly composed current Kentridge graph can be restored.");
            if (_restoreTarget != null)
                throw new InvalidOperationException("A Kentridge restore is already active.");

            _restoreTarget = target;
            try
            {
                SessionPersistenceResult result = _service.Restore(
                    new SessionRestoreRequest(
                        new SessionSaveId(restoreSourceId),
                        ContentId,
                        new SessionWorldId(identity.WorldId)));
                if (!result.Succeeded)
                    throw new InvalidOperationException(
                        "Kentridge restore failed: " + result.Failure + " " + result.Detail);
            }
            finally
            {
                _restoreTarget = null;
            }
        }

        private ISessionSnapshotContributor[] CreateContributors() => new ISessionSnapshotContributor[]
        {
            new SemanticContributor(
                "kentridge.characters", "CharacterRegistryState", 100,
                CaptureCharacters, ValidateCharacters, RestoreCharacters),
            new SemanticContributor(
                "kentridge.campaign", "CampaignProgressState", 200,
                CaptureCampaign, ValidateCampaign, RestoreCampaign),
            new SemanticContributor(
                "kentridge.inventory", "InventoryState", 300,
                CaptureInventory, ValidateInventory, RestoreInventory),
            new SemanticContributor(
                "kentridge.encounters", "EncounterRegistryState", 400,
                CaptureEncounters, ValidateEncounters, RestoreEncounters)
        };

        private byte[] CaptureCharacters()
        {
            ICharacterRegistryPersistence persistence = ResolveCharacterPersistence();
            return Encode(CharacterStateDto.From(persistence.CaptureState()));
        }

        private string ValidateCharacters(byte[] payload) => Validate<CharacterStateDto>(payload);

        private void RestoreCharacters(byte[] payload)
        {
            CharacterRegistryState state = Decode<CharacterStateDto>(payload).ToState();
            CharacterRegistryFailure failure = ResolveCharacterPersistence().RestoreState(state);
            if (failure != CharacterRegistryFailure.None)
                throw new InvalidOperationException("Character restore failed: " + failure + ".");

            KentridgeCharacterHost host = _factory.ActorHost as KentridgeCharacterHost
                ?? throw new InvalidOperationException("Kentridge restore requires the production character host.");
            for (int i = 0; i < state.Characters.Count; i++)
            {
                CharacterRecord record = state.Characters[i];
                if (!record.Definition.Id.Equals(host.PlayerCharacterId)) continue;
                host.RestorePlayerKinematics(record.Kinematics);
                return;
            }
            throw new InvalidOperationException("Restored character state does not contain the local Kentridge player.");
        }

        private byte[] CaptureCampaign() =>
            Encode(CampaignStateDto.From(CurrentGraph().Session.Runtime.CaptureProgress()));

        private string ValidateCampaign(byte[] payload) => Validate<CampaignStateDto>(payload);

        private void RestoreCampaign(byte[] payload) =>
            RestoreGraph().Session.Runtime.RestoreProgress(Decode<CampaignStateDto>(payload).ToState());

        private byte[] CaptureInventory() =>
            Encode(InventoryStateDto.From(CurrentGraph().Session.InventoryState.CaptureState()));

        private string ValidateInventory(byte[] payload) => Validate<InventoryStateDto>(payload);

        private void RestoreInventory(byte[] payload)
        {
            InventoryFailureReason failure = RestoreGraph().Session.InventoryState.RestoreState(
                Decode<InventoryStateDto>(payload).ToState());
            if (failure != InventoryFailureReason.None)
                throw new InvalidOperationException("Inventory restore failed: " + failure + ".");
        }

        private byte[] CaptureEncounters()
        {
            IEncounterRegistry registry = ResolveEncounterRegistry();
            return Encode(EncounterStateDto.From(registry.Capture()));
        }

        private string ValidateEncounters(byte[] payload) => Validate<EncounterStateDto>(payload);

        private void RestoreEncounters(byte[] payload)
        {
            EncounterMutationFailure failure = ResolveEncounterRegistry().Restore(
                Decode<EncounterStateDto>(payload).ToState());
            if (failure != EncounterMutationFailure.None)
                throw new InvalidOperationException("Encounter restore failed: " + failure + ".");
        }

        private ICharacterRegistryPersistence ResolveCharacterPersistence()
        {
            KentridgeCharacterHost host = _factory.ActorHost as KentridgeCharacterHost
                ?? throw new InvalidOperationException("Kentridge production character host is unavailable.");
            return host.Characters as ICharacterRegistryPersistence
                ?? throw new InvalidOperationException("Kentridge character registry does not expose persistence.");
        }

        private IEncounterRegistry ResolveEncounterRegistry() =>
            _forest.EncounterQuery as IEncounterRegistry
            ?? throw new InvalidOperationException("Kentridge encounter registry is unavailable.");

        private KentridgeSessionRuntimeGraph CurrentGraph() =>
            _factory.Current
            ?? throw new InvalidOperationException("No current Kentridge graph is available for capture.");

        private KentridgeSessionRuntimeGraph RestoreGraph() =>
            _restoreTarget
            ?? throw new InvalidOperationException("No Kentridge graph is active for restore.");

        private static KentridgeSessionRuntimeGraph RequireGraph(ISessionRuntimeGraph graph) =>
            graph as KentridgeSessionRuntimeGraph
            ?? throw new InvalidOperationException("Kentridge persistence received a non-Kentridge runtime graph.");

        private static byte[] Encode<T>(T dto) where T : class =>
            Encoding.UTF8.GetBytes(JsonUtility.ToJson(dto));

        private static T Decode<T>(byte[] payload) where T : class
        {
            if (payload == null || payload.Length == 0)
                throw new InvalidOperationException("Semantic persistence payload is empty.");
            T value = JsonUtility.FromJson<T>(Encoding.UTF8.GetString(payload));
            return value ?? throw new InvalidOperationException("Semantic persistence payload could not be decoded.");
        }

        private static string Validate<T>(byte[] payload) where T : class
        {
            try { Decode<T>(payload); return string.Empty; }
            catch (Exception ex) { return ex.Message; }
        }

        private sealed class CaptureBarrier : ISessionCaptureBarrier
        {
            private readonly KentridgeSessionPersistenceBridge _owner;
            private bool _leased;
            public CaptureBarrier(KentridgeSessionPersistenceBridge owner) => _owner = owner;
            public bool TryEnter(out ISessionCaptureLease lease)
            {
                if (_leased) { lease = null; return false; }
                _leased = true;
                lease = new CaptureLease(++_owner._captureRevision, () => _leased = false);
                return true;
            }
        }

        private sealed class CaptureLease : ISessionCaptureLease
        {
            private Action _dispose;
            public ulong AuthoritativeRevision { get; }
            public CaptureLease(ulong revision, Action dispose) { AuthoritativeRevision = revision; _dispose = dispose; }
            public void Dispose() { Action dispose = _dispose; _dispose = null; dispose?.Invoke(); }
        }

        private sealed class SemanticContributor : ISessionSnapshotContributor
        {
            private readonly string _semanticType;
            private readonly Func<byte[]> _capture;
            private readonly Func<byte[], string> _validate;
            private readonly Action<byte[]> _restore;
            public string SectionId { get; }
            public int SchemaVersion => 1;
            public int RestoreOrder { get; }
            public bool RequiredForRestore => true;

            public SemanticContributor(
                string sectionId,
                string semanticType,
                int restoreOrder,
                Func<byte[]> capture,
                Func<byte[], string> validate,
                Action<byte[]> restore)
            {
                SectionId = sectionId;
                _semanticType = semanticType;
                RestoreOrder = restoreOrder;
                _capture = capture;
                _validate = validate;
                _restore = restore;
            }

            public SessionContributorCapture Capture(ulong authoritativeRevision)
            {
                try
                {
                    return SessionContributorCapture.Success(new SessionSectionSnapshot(
                        SectionId, _semanticType, SchemaVersion, authoritativeRevision, _capture()));
                }
                catch (Exception ex) { return SessionContributorCapture.Reject(ex.Message); }
            }

            public SessionContributorResult Validate(SessionSectionSnapshot section)
            {
                string error = _validate(section.CopyPayload());
                return string.IsNullOrEmpty(error)
                    ? SessionContributorResult.Success()
                    : SessionContributorResult.Reject(error);
            }

            public SessionContributorResult Restore(SessionSectionSnapshot section)
            {
                try { _restore(section.CopyPayload()); return SessionContributorResult.Success(); }
                catch (Exception ex) { return SessionContributorResult.Reject(ex.Message); }
            }
        }

        private sealed class RestoreFactory : ISessionRestoreGraphFactory
        {
            private readonly KentridgeSessionPersistenceBridge _owner;
            public RestoreFactory(KentridgeSessionPersistenceBridge owner) => _owner = owner;
            public bool TryCreate(GameSessionSnapshotHeader header, out ISessionRestoreGraph graph, out string error)
            {
                if (_owner._restoreTarget == null)
                {
                    graph = null; error = "No freshly composed Kentridge graph is available for restore."; return false;
                }
                graph = new RestoreGraph(_owner, _owner._restoreTarget);
                error = string.Empty;
                return true;
            }
        }

        private sealed class RestoreGraph : ISessionRestoreGraph
        {
            private readonly KentridgeSessionPersistenceBridge _owner;
            private readonly KentridgeSessionRuntimeGraph _graph;
            public IReadOnlyList<ISessionSnapshotContributor> Contributors { get; }
            public RestoreGraph(KentridgeSessionPersistenceBridge owner, KentridgeSessionRuntimeGraph graph)
            {
                _owner = owner; _graph = graph; Contributors = owner.CreateContributors();
            }
            public void CompleteRestore()
            {
                if (!ReferenceEquals(_owner._restoreTarget, _graph))
                    throw new InvalidOperationException("Kentridge restore target changed during restore.");
                _graph.MarkRestoredFromPersistence();
            }
            public void AbortRestore() { }
        }

        [Serializable]
        private sealed class CampaignStateDto
        {
            public string[] completedCutscenes;
            public string[] joinedPartyMembers;
            public string[] grantedSpells;
            public ProgressionDto progression;
            public static CampaignStateDto From(CampaignProgressSnapshot state)
            {
                var dto = new CampaignStateDto
                {
                    completedCutscenes = new string[state.CompletedCutscenes.Count],
                    joinedPartyMembers = CopyStrings(state.JoinedPartyMembers),
                    grantedSpells = CopyStrings(state.GrantedSpells),
                    progression = ProgressionDto.From(state.Progression)
                };
                for (int i = 0; i < dto.completedCutscenes.Length; i++)
                    dto.completedCutscenes[i] = state.CompletedCutscenes[i].Id;
                return dto;
            }
            public CampaignProgressSnapshot ToState()
            {
                var cutscenes = new CutsceneRef[completedCutscenes?.Length ?? 0];
                for (int i = 0; i < cutscenes.Length; i++) cutscenes[i] = new CutsceneRef(completedCutscenes[i]);
                return new CampaignProgressSnapshot(
                    cutscenes,
                    joinedPartyMembers ?? Array.Empty<string>(),
                    grantedSpells ?? Array.Empty<string>(),
                    progression?.ToState());
            }
        }

        [Serializable]
        private sealed class ProgressionDto
        {
            public string revision;
            public QuestDto[] quests;
            public ObjectiveDto[] standalone;
            public string[] appliedOperationIds;
            public long compatibilitySequence;
            public static ProgressionDto From(ProgressionSnapshot state)
            {
                if (state == null) return null;
                var dto = new ProgressionDto
                {
                    revision = state.Revision.ToString(CultureInfo.InvariantCulture),
                    quests = new QuestDto[state.Quests.Count],
                    standalone = new ObjectiveDto[state.StandaloneObjectives.Count],
                    appliedOperationIds = CopyStrings(state.AppliedOperationIds),
                    compatibilitySequence = state.CompatibilitySequence
                };
                for (int i = 0; i < dto.quests.Length; i++) dto.quests[i] = QuestDto.From(state.Quests[i]);
                for (int i = 0; i < dto.standalone.Length; i++) dto.standalone[i] = ObjectiveDto.From(state.StandaloneObjectives[i]);
                return dto;
            }
            public ProgressionSnapshot ToState()
            {
                var q = new QuestProgressSnapshot[quests?.Length ?? 0];
                for (int i = 0; i < q.Length; i++) q[i] = quests[i].ToState();
                var o = new ObjectiveProgressSnapshot[standalone?.Length ?? 0];
                for (int i = 0; i < o.Length; i++) o[i] = standalone[i].ToState();
                return new ProgressionSnapshot(ParseUlong(revision), q, o,
                    appliedOperationIds ?? Array.Empty<string>(), compatibilitySequence);
            }
        }

        [Serializable]
        private sealed class QuestDto
        {
            public string id;
            public int state;
            public string activeStepId;
            public StepDto[] steps;
            public ObjectiveDto[] objectives;
            public string revision;
            public static QuestDto From(QuestProgressSnapshot value)
            {
                var dto = new QuestDto
                {
                    id = value.Id.Value, state = (int)value.State, activeStepId = value.ActiveStepId,
                    steps = new StepDto[value.Steps.Count], objectives = new ObjectiveDto[value.Objectives.Count],
                    revision = value.Revision.ToString(CultureInfo.InvariantCulture)
                };
                for (int i = 0; i < dto.steps.Length; i++) dto.steps[i] = StepDto.From(value.Steps[i]);
                for (int i = 0; i < dto.objectives.Length; i++) dto.objectives[i] = ObjectiveDto.From(value.Objectives[i]);
                return dto;
            }
            public QuestProgressSnapshot ToState()
            {
                if (steps != null && steps.Length > 0)
                {
                    var s = new QuestStepProgressSnapshot[steps.Length];
                    for (int i = 0; i < s.Length; i++) s[i] = steps[i].ToState();
                    return new QuestProgressSnapshot(new QuestId(id), (ProgressionLifecycleState)state,
                        activeStepId ?? string.Empty, s, ParseUlong(revision));
                }
                var o = new ObjectiveProgressSnapshot[objectives?.Length ?? 0];
                for (int i = 0; i < o.Length; i++) o[i] = objectives[i].ToState();
                return new QuestProgressSnapshot(new QuestId(id), (ProgressionLifecycleState)state, o, ParseUlong(revision));
            }
        }

        [Serializable]
        private sealed class StepDto
        {
            public string id; public int state; public ObjectiveDto[] objectives;
            public static StepDto From(QuestStepProgressSnapshot value)
            {
                var dto = new StepDto { id = value.StepId, state = (int)value.State, objectives = new ObjectiveDto[value.Objectives.Count] };
                for (int i = 0; i < dto.objectives.Length; i++) dto.objectives[i] = ObjectiveDto.From(value.Objectives[i]);
                return dto;
            }
            public QuestStepProgressSnapshot ToState()
            {
                var o = new ObjectiveProgressSnapshot[objectives?.Length ?? 0];
                for (int i = 0; i < o.Length; i++) o[i] = objectives[i].ToState();
                return new QuestStepProgressSnapshot(id, (ProgressionLifecycleState)state, o);
            }
        }

        [Serializable]
        private sealed class ObjectiveDto
        {
            public string id; public int state; public int current; public int required; public string revision;
            public static ObjectiveDto From(ObjectiveProgressSnapshot value) => new ObjectiveDto
            {
                id = value.Id.Value, state = (int)value.State, current = value.CurrentCount,
                required = value.RequiredCount, revision = value.Revision.ToString(CultureInfo.InvariantCulture)
            };
            public ObjectiveProgressSnapshot ToState() => new ObjectiveProgressSnapshot(
                new ObjectiveId(id), (ProgressionLifecycleState)state, current, required, ParseUlong(revision));
        }

        [Serializable]
        private sealed class InventoryStateDto
        {
            public InventoryDto[] inventories;
            public static InventoryStateDto From(InventoryStateCapture state)
            {
                var dto = new InventoryStateDto { inventories = new InventoryDto[state.Inventories.Count] };
                for (int i = 0; i < dto.inventories.Length; i++) dto.inventories[i] = InventoryDto.From(state.Inventories[i]);
                return dto;
            }
            public InventoryStateCapture ToState()
            {
                var values = new InventorySnapshot[inventories?.Length ?? 0];
                for (int i = 0; i < values.Length; i++) values[i] = inventories[i].ToState();
                return new InventoryStateCapture(values);
            }
        }

        [Serializable]
        private sealed class InventoryDto
        {
            public string id; public string revision; public InventoryEntryDto[] entries;
            public static InventoryDto From(InventorySnapshot value)
            {
                var dto = new InventoryDto { id = value.Id.Value, revision = value.Revision.ToString(CultureInfo.InvariantCulture), entries = new InventoryEntryDto[value.Entries.Count] };
                for (int i = 0; i < dto.entries.Length; i++) dto.entries[i] = new InventoryEntryDto { item = value.Entries[i].Item.Id, quantity = value.Entries[i].Quantity };
                return dto;
            }
            public InventorySnapshot ToState()
            {
                var values = new InventoryEntry[entries?.Length ?? 0];
                for (int i = 0; i < values.Length; i++) values[i] = new InventoryEntry(new ItemRef(entries[i].item), entries[i].quantity);
                return new InventorySnapshot(new InventoryId(id), ParseUlong(revision), values);
            }
        }
        [Serializable] private sealed class InventoryEntryDto { public string item; public int quantity; }

        [Serializable]
        private sealed class CharacterStateDto
        {
            public CharacterDto[] characters; public string[] retiredIds;
            public static CharacterStateDto From(CharacterRegistryState state)
            {
                var dto = new CharacterStateDto { characters = new CharacterDto[state.Characters.Count], retiredIds = new string[state.RetiredIds.Count] };
                for (int i = 0; i < dto.characters.Length; i++) dto.characters[i] = CharacterDto.From(state.Characters[i]);
                for (int i = 0; i < dto.retiredIds.Length; i++) dto.retiredIds[i] = state.RetiredIds[i].Value;
                return dto;
            }
            public CharacterRegistryState ToState()
            {
                var values = new CharacterRecord[characters?.Length ?? 0];
                for (int i = 0; i < values.Length; i++) values[i] = characters[i].ToState();
                var retired = new CharacterId[retiredIds?.Length ?? 0];
                for (int i = 0; i < retired.Length; i++) retired[i] = new CharacterId(retiredIds[i]);
                return new CharacterRegistryState(values, retired);
            }
        }

        [Serializable]
        private sealed class CharacterDto
        {
            public string id; public int traits; public int lifecycle; public VectorDto position; public VectorDto velocity; public VectorDto facing; public string revision; public BindingDto[] bindings;
            public static CharacterDto From(CharacterRecord value)
            {
                var dto = new CharacterDto
                {
                    id = value.Definition.Id.Value, traits = (int)value.Definition.Traits, lifecycle = (int)value.Lifecycle,
                    position = VectorDto.From(value.Kinematics.Position), velocity = VectorDto.From(value.Kinematics.Velocity), facing = VectorDto.From(value.Kinematics.Facing),
                    revision = value.Revision.ToString(CultureInfo.InvariantCulture), bindings = new BindingDto[value.Bindings.Count]
                };
                for (int i = 0; i < dto.bindings.Length; i++) dto.bindings[i] = new BindingDto { scope = value.Bindings[i].Scope, key = value.Bindings[i].Key };
                return dto;
            }
            public CharacterRecord ToState()
            {
                var b = new CharacterBinding[bindings?.Length ?? 0];
                for (int i = 0; i < b.Length; i++) b[i] = new CharacterBinding(bindings[i].scope, bindings[i].key);
                return new CharacterRecord(new CharacterDefinition(new CharacterId(id), (CharacterTraits)traits),
                    (CharacterLifecycleState)lifecycle,
                    new CharacterKinematicState(position.ToState(), velocity.ToState(), facing.ToState()),
                    ParseUlong(revision), b);
            }
        }
        [Serializable] private sealed class BindingDto { public string scope; public string key; }
        [Serializable]
        private sealed class VectorDto
        {
            public float x; public float y; public float z;
            public static VectorDto From(CharacterVector3 v) => new VectorDto { x = v.X, y = v.Y, z = v.Z };
            public CharacterVector3 ToState() => new CharacterVector3(x, y, z);
        }

        [Serializable]
        private sealed class EncounterStateDto
        {
            public EncounterDto[] encounters; public string sequence;
            public static EncounterStateDto From(EncounterRegistrySnapshot state)
            {
                var dto = new EncounterStateDto { encounters = new EncounterDto[state.Encounters.Count], sequence = state.Sequence.ToString(CultureInfo.InvariantCulture) };
                for (int i = 0; i < dto.encounters.Length; i++) dto.encounters[i] = EncounterDto.From(state.Encounters[i]);
                return dto;
            }
            public EncounterRegistrySnapshot ToState()
            {
                var values = new EncounterSnapshot[encounters?.Length ?? 0];
                for (int i = 0; i < values.Length; i++) values[i] = encounters[i].ToState();
                return new EncounterRegistrySnapshot(values, ParseUlong(sequence));
            }
        }

        [Serializable]
        private sealed class EncounterDto
        {
            public string id; public int combatPolicy; public string semanticKind; public int lifecycle;
            public ParticipantDto[] participants; public bool hasResolution; public int resolutionResult; public string resolutionReason;
            public string activationCause; public string realizationId; public string revision;
            public static EncounterDto From(EncounterSnapshot value)
            {
                var dto = new EncounterDto
                {
                    id = value.Id.Value, combatPolicy = (int)value.Definition.CombatPolicy, semanticKind = value.Definition.SemanticKind,
                    lifecycle = (int)value.Lifecycle, participants = new ParticipantDto[value.Membership.Participants.Count],
                    hasResolution = value.Resolution.HasValue, activationCause = value.ActivationCause, realizationId = value.RealizationId,
                    revision = value.Revision.ToString(CultureInfo.InvariantCulture)
                };
                if (value.Resolution.HasValue) { dto.resolutionResult = (int)value.Resolution.Value.Result; dto.resolutionReason = value.Resolution.Value.Reason; }
                for (int i = 0; i < dto.participants.Length; i++)
                {
                    EncounterParticipant p = value.Membership.Participants[i];
                    dto.participants[i] = new ParticipantDto { characterId = p.CharacterId.Value, ownership = (int)p.Ownership, role = p.Role };
                }
                return dto;
            }
            public EncounterSnapshot ToState()
            {
                var p = new EncounterParticipant[participants?.Length ?? 0];
                for (int i = 0; i < p.Length; i++) p[i] = new EncounterParticipant(new CharacterId(participants[i].characterId), (EncounterParticipantOwnership)participants[i].ownership, participants[i].role);
                EncounterResolution? resolution = hasResolution ? new EncounterResolution((EncounterResolutionResult)resolutionResult, resolutionReason) : (EncounterResolution?)null;
                return new EncounterSnapshot(new EncounterDefinition(new EncounterId(id), (EncounterCombatPolicy)combatPolicy, semanticKind),
                    (EncounterLifecycleState)lifecycle, new EncounterMembershipSnapshot(p), resolution,
                    activationCause ?? string.Empty, realizationId ?? string.Empty, ParseUlong(revision));
            }
        }
        [Serializable] private sealed class ParticipantDto { public string characterId; public int ownership; public string role; }

        private static string[] CopyStrings(IReadOnlyList<string> values)
        {
            var copy = new string[values?.Count ?? 0];
            for (int i = 0; i < copy.Length; i++) copy[i] = values[i];
            return copy;
        }

        private static ulong ParseUlong(string value) =>
            ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong parsed)
                ? parsed
                : throw new InvalidDataException("Invalid semantic revision value: " + value);
    }
}
