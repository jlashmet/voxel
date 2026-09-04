using System;
using System.Collections.Generic;
using Game.Characters.Api;
using Game.Encounters.Api;
using Game.Hud.Api;
using Game.Input.Api;
using Game.Input.Runtime;
using Game.Progression.Api;
using Game.ProgressionPresentation.Api;
using Game.Sessions.Api;
using Game.Vitality.Api;
using UnityEngine;

namespace Game.Hud.Runtime.Validation
{
    [DefaultExecutionOrder(-1000)]
    public sealed class HudSemanticPresentationValidation : MonoBehaviour
    {
        private const string Success = "HUD_SEMANTIC_PRESENTATION_VALIDATION PASS";

        private void Awake()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("HUD Validation Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
            }
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.06f, 1f);
        }

        private void Start()
        {
            var local = new LocalPlayerId(0);
            var memberId = new PartyMemberId("validation-member");
            var characterId = CharacterId.FromStableKey("hud-validation", "player");
            var partyMember = new PartyMemberSnapshot(memberId, new PlayerSlot(0), PartyLeadershipRole.Leader,
                PartyPresenceState.Connected, SessionReadinessState.GameplayReady, characterId);
            var encounter = new EncounterSnapshot(
                new EncounterDefinition(new EncounterId("validation-ambush"), EncounterCombatPolicy.Required, "forest ambush"),
                EncounterLifecycleState.Active,
                new EncounterMembershipSnapshot(new[] { new EncounterParticipant(characterId, EncounterParticipantOwnership.Persistent, "player") }),
                null, "validation", "", 1);

            var bindings = new UnityInputBindingService();
            var progression = new TrackedObjectiveHudSource(local, new Progression());
            var projector = new HudSnapshotProjector(
                new Resolver(local, memberId),
                new Party(partyMember),
                new Vitality(new VitalitySnapshot(characterId, 73, 100, false, 4)),
                new Encounters(encounter),
                new Interaction(characterId),
                progression,
                null,
                bindings,
                new Context());

            HudSnapshot snapshot = projector.Project(local);
            if (!snapshot.Vitality.Visible || snapshot.Vitality.Current != 73 || !snapshot.Interaction.Visible
                || snapshot.Interaction.BindingLabel != "E" || !snapshot.Encounter.Visible
                || !snapshot.Encounter.CombatRequired || snapshot.Readiness != HudReadinessState.GameplayReady
                || !snapshot.TrackedProgression.Visible
                || snapshot.TrackedProgression.StableId != "quest:validation/objective:reach-gate")
                throw new InvalidOperationException("HUD semantic projection did not produce the required validation state.");

            bindings.Rebind(StandardInputActions.Interact, KeyCode.F);
            HudSnapshot rebound = projector.Project(local);
            if (!rebound.Interaction.Visible || rebound.Interaction.BindingLabel != "F")
                throw new InvalidOperationException("HUD prompt did not reflect the production input rebinding seam.");
            bindings.Rebind(StandardInputActions.Interact, KeyCode.E);

            GameplayHudPresenter presenter = gameObject.AddComponent<GameplayHudPresenter>();
            presenter.Configure(projector, local);
            Debug.Log(Success + " vitality=73/100 prompt=E combat=true readiness=GameplayReady objective=Reach the gate source=System19");
        }

        private sealed class Resolver : IHudLocalPlayerResolver
        {
            private readonly LocalPlayerId _local; private readonly PartyMemberId _member;
            public Resolver(LocalPlayerId local, PartyMemberId member) { _local = local; _member = member; }
            public bool TryResolveMember(LocalPlayerId local, out PartyMemberId member) { member = _member; return local.Equals(_local); }
        }
        private sealed class Party : IPartySessionQuery
        {
            private readonly PartyMemberSnapshot _member;
            public Party(PartyMemberSnapshot member) { _member = member; }
            public PartyRosterSnapshot Snapshot() => new PartyRosterSnapshot(new GameSessionId("hud-validation"), new[] { _member });
            public bool TryGetMember(PartyMemberId id, out PartyMemberSnapshot member) { member = _member; return id == _member.MemberId; }
        }
        private sealed class Vitality : IVitalityQuery
        {
            private readonly VitalitySnapshot _state;
            public Vitality(VitalitySnapshot state) { _state = state; }
            public IReadOnlyList<VitalitySnapshot> GetAll() => new[] { _state };
            public bool TryGet(CharacterId id, out VitalitySnapshot state) { state = _state; return id == _state.CharacterId; }
        }
        private sealed class Encounters : IEncounterQuery
        {
            private readonly EncounterSnapshot _state;
            public Encounters(EncounterSnapshot state) { _state = state; }
            public bool TryGet(EncounterId id, out EncounterSnapshot state) { state = _state; return id == _state.Id; }
            public IReadOnlyList<EncounterSnapshot> GetAll() => new[] { _state };
        }
        private sealed class Interaction : IHudInteractionSource
        {
            private readonly CharacterId _character;
            public Interaction(CharacterId character) { _character = character; }
            public bool TryGetCurrent(LocalPlayerId local, CharacterId character, out HudInteractionCandidate candidate)
            {
                if (character == _character) { candidate = new HudInteractionCandidate("npc:gatekeeper", "Talk", "Conversation", StandardInputActions.Interact); return true; }
                candidate = default; return false;
            }
        }
        private sealed class Progression : ITrackedObjectiveProjection
        {
            public bool TryGetTrackedObjective(out TrackedObjectiveSummary summary)
            {
                summary = new TrackedObjectiveSummary(
                    new JournalObjectiveKey(
                        new QuestId("quest:validation"),
                        new ObjectiveId("objective:reach-gate")),
                    "Way to Kentridge",
                    "Reach the gate",
                    ProgressionLifecycleState.Active,
                    2,
                    3,
                    5);
                return true;
            }
        }
        private sealed class Context : IInputContextService
        { public InputContextId ActiveContext => InputContextId.Exploration; public IInputContextLease Push(InputContextId context) { throw new NotSupportedException(); } }
    }
}
