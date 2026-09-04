using System;
using System.Collections.Generic;
using Game.Characters.Api;
using Game.Encounters.Api;
using Game.Hud.Api;
using Game.Hud.Runtime;
using Game.Input.Api;
using Game.Sessions.Api;
using Game.Vitality.Api;
using NUnit.Framework;

namespace Game.Hud.Tests
{
    public sealed class HudSnapshotProjectorTests
    {
        [Test]
        public void Project_UsesControlledCharacterAndSemanticBinding()
        {
            Fixture f = new Fixture();
            HudSnapshot hud = f.Projector.Project(f.Local0);
            Assert.That(hud.Identity.CharacterId, Is.EqualTo(f.CharacterA));
            Assert.That(hud.Vitality.Current, Is.EqualTo(73));
            Assert.That(hud.Interaction.BindingLabel, Is.EqualTo("E"));
            Assert.That(hud.Interaction.ActionText, Is.EqualTo("Talk"));
            Assert.That(hud.Readiness, Is.EqualTo(HudReadinessState.GameplayReady));
            Assert.That(hud.Encounter.CombatRequired, Is.True);
        }

        [Test]
        public void BindingChange_UpdatesPromptWithoutChangingCandidate()
        {
            Fixture f = new Fixture();
            Assert.That(f.Projector.Project(f.Local0).Interaction.BindingLabel, Is.EqualTo("E"));
            f.Bindings.Label = "F";
            HudSnapshot changed = f.Projector.Project(f.Local0);
            Assert.That(changed.Interaction.BindingLabel, Is.EqualTo("F"));
            Assert.That(changed.Interaction.TargetId, Is.EqualTo("npc:destination"));
        }

        [Test]
        public void LocalPlayers_CannotSeeEachOthersControlledCharacterState()
        {
            Fixture f = new Fixture();
            HudSnapshot first = f.Projector.Project(f.Local0);
            HudSnapshot second = f.Projector.Project(f.Local1);
            Assert.That(first.Identity.CharacterId, Is.EqualTo(f.CharacterA));
            Assert.That(first.Vitality.Current, Is.EqualTo(73));
            Assert.That(second.Identity.CharacterId, Is.EqualTo(f.CharacterB));
            Assert.That(second.Vitality.Current, Is.EqualTo(29));
            Assert.That(second.Interaction.TargetId, Is.EqualTo("chest:beta"));
        }

        [Test]
        public void RebuildAfterReconnect_ReprojectsPersistentStateAndBaselinesOldTransientEvents()
        {
            Fixture f = new Fixture();
            Assert.That(f.Projector.Project(f.Local0).TransientEvents.Count, Is.EqualTo(1));
            f.Transients.Events.Add(new HudTransientEvent("old-2", "Old two"));
            f.Projector.RebuildAfterReconnect(f.Local0);
            f.Vitality.A = new VitalitySnapshot(f.CharacterA, 61, 100, false, 2);
            HudSnapshot rebuilt = f.Projector.Project(f.Local0);
            Assert.That(rebuilt.Vitality.Current, Is.EqualTo(61));
            Assert.That(rebuilt.TransientEvents.Count, Is.Zero);
            f.Transients.Events.Add(new HudTransientEvent("new-3", "Fresh"));
            Assert.That(f.Projector.Project(f.Local0).TransientEvents.Count, Is.EqualTo(1));
        }

        [Test]
        public void UiContext_HidesInteractionWithoutHidingPersistentHud()
        {
            Fixture f = new Fixture();
            f.Context.Context = InputContextId.Ui;
            HudSnapshot hud = f.Projector.Project(f.Local0);
            Assert.That(hud.Vitality.Visible, Is.True);
            Assert.That(hud.Interaction.Visible, Is.False);
            Assert.That(hud.InputContext, Is.EqualTo(InputContextId.Ui));
        }

        private sealed class Fixture
        {
            public readonly LocalPlayerId Local0 = new LocalPlayerId(0);
            public readonly LocalPlayerId Local1 = new LocalPlayerId(1);
            public readonly PartyMemberId MemberA = new PartyMemberId("member-a");
            public readonly PartyMemberId MemberB = new PartyMemberId("member-b");
            public readonly CharacterId CharacterA = new CharacterId("character-a");
            public readonly CharacterId CharacterB = new CharacterId("character-b");
            public readonly FakeBindings Bindings = new FakeBindings();
            public readonly FakeContext Context = new FakeContext();
            public readonly FakeTransientSource Transients = new FakeTransientSource();
            public readonly FakeVitality Vitality;
            public readonly HudSnapshotProjector Projector;

            public Fixture()
            {
                var resolver = new FakeResolver(Local0, MemberA, Local1, MemberB);
                var party = new FakeParty(
                    new PartyMemberSnapshot(MemberA, new PlayerSlot(0), PartyLeadershipRole.Leader, PartyPresenceState.Connected, SessionReadinessState.GameplayReady, CharacterA),
                    new PartyMemberSnapshot(MemberB, new PlayerSlot(1), PartyLeadershipRole.Member, PartyPresenceState.Connected, SessionReadinessState.GameplayReady, CharacterB));
                Vitality = new FakeVitality(
                    new VitalitySnapshot(CharacterA, 73, 100, false, 1),
                    new VitalitySnapshot(CharacterB, 29, 80, false, 1));
                var encounters = new FakeEncounters(CharacterA);
                var interaction = new FakeInteraction(CharacterA, CharacterB);
                Transients.Events.Add(new HudTransientEvent("old-1", "Old one"));
                Projector = new HudSnapshotProjector(resolver, party, Vitality, encounters, interaction, null, Transients, Bindings, Context);
            }
        }

        private sealed class FakeResolver : IHudLocalPlayerResolver
        {
            private readonly LocalPlayerId _a; private readonly PartyMemberId _ma; private readonly LocalPlayerId _b; private readonly PartyMemberId _mb;
            public FakeResolver(LocalPlayerId a, PartyMemberId ma, LocalPlayerId b, PartyMemberId mb) { _a = a; _ma = ma; _b = b; _mb = mb; }
            public bool TryResolveMember(LocalPlayerId id, out PartyMemberId member) { if (id.Equals(_a)) { member = _ma; return true; } if (id.Equals(_b)) { member = _mb; return true; } member = default; return false; }
        }
        private sealed class FakeParty : IPartySessionQuery
        {
            private readonly PartyMemberSnapshot _a, _b;
            public FakeParty(PartyMemberSnapshot a, PartyMemberSnapshot b) { _a = a; _b = b; }
            public PartyRosterSnapshot Snapshot() => new PartyRosterSnapshot(new GameSessionId("session"), new[] { _a, _b });
            public bool TryGetMember(PartyMemberId id, out PartyMemberSnapshot member) { if (id == _a.MemberId) { member = _a; return true; } if (id == _b.MemberId) { member = _b; return true; } member = default; return false; }
        }
        private sealed class FakeVitality : IVitalityQuery
        {
            public VitalitySnapshot A, B;
            public FakeVitality(VitalitySnapshot a, VitalitySnapshot b) { A = a; B = b; }
            public IReadOnlyList<VitalitySnapshot> GetAll() => new[] { A, B };
            public bool TryGet(CharacterId id, out VitalitySnapshot state) { if (id == A.CharacterId) { state = A; return true; } if (id == B.CharacterId) { state = B; return true; } state = default; return false; }
        }
        private sealed class FakeEncounters : IEncounterQuery
        {
            private readonly EncounterSnapshot _encounter;
            public FakeEncounters(CharacterId character) { _encounter = new EncounterSnapshot(new EncounterDefinition(new EncounterId("ambush"), EncounterCombatPolicy.Required, "bandit-ambush"), EncounterLifecycleState.Active, new EncounterMembershipSnapshot(new[] { new EncounterParticipant(character, EncounterParticipantOwnership.Persistent, "player") }), null, "test", "", 1); }
            public bool TryGet(EncounterId id, out EncounterSnapshot state) { state = _encounter; return id == _encounter.Id; }
            public IReadOnlyList<EncounterSnapshot> GetAll() => new[] { _encounter };
        }
        private sealed class FakeInteraction : IHudInteractionSource
        {
            private readonly CharacterId _a, _b;
            public FakeInteraction(CharacterId a, CharacterId b) { _a = a; _b = b; }
            public bool TryGetCurrent(LocalPlayerId player, CharacterId character, out HudInteractionCandidate candidate)
            { if (character == _a) { candidate = new HudInteractionCandidate("npc:destination", "Talk", "Conversation", StandardInputActions.Interact); return true; } if (character == _b) { candidate = new HudInteractionCandidate("chest:beta", "Open", "Container", StandardInputActions.Interact); return true; } candidate = default; return false; }
        }
        private sealed class FakeBindings : IInputBindingPresentation
        { public string Label = "E"; public bool TryGetDisplayLabel(LocalPlayerId player, InputActionId action, out string displayLabel) { displayLabel = Label; return true; } }
        private sealed class FakeContext : IInputContextService
        { public InputContextId Context = InputContextId.Exploration; public InputContextId ActiveContext => Context; public IInputContextLease Push(InputContextId context) { throw new NotSupportedException(); } }
        private sealed class FakeTransientSource : IHudTransientEventSource
        { public readonly List<HudTransientEvent> Events = new List<HudTransientEvent>(); public IReadOnlyList<HudTransientEvent> Snapshot(LocalPlayerId localPlayerId) => Events; }
    }
}
