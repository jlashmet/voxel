using System;
using System.Collections.Generic;
using Game.Characters.Api;
using Game.Encounters.Api;
using Game.Encounters.Runtime;
using NUnit.Framework;

namespace Game.Encounters.Tests
{
    public sealed class EncounterRegistryTests
    {
        [Test]
        public void LifecycleIsDeterministicIdempotentAndRejectsInvalidTransitions()
        {
            var characters = new FakeCharacters(Active("npc:a"));
            var registry = new EncounterRegistry(characters);
            var id = new EncounterId("roadside-parley");
            Assert.That(registry.Register(new EncounterDefinition(id, EncounterCombatPolicy.None, "parley"), out _), Is.EqualTo(EncounterMutationFailure.None));

            var activation = new EncounterActivationRequest(id, "proximity", "world-feature:road-12");
            Assert.That(registry.Activate(activation, out EncounterSnapshot active), Is.EqualTo(EncounterMutationFailure.None));
            ulong activeRevision = active.Revision;
            Assert.That(registry.Activate(activation, out active), Is.EqualTo(EncounterMutationFailure.None));
            Assert.That(active.Revision, Is.EqualTo(activeRevision));
            Assert.That(registry.Cleanup(id, out _), Is.EqualTo(EncounterMutationFailure.InvalidTransition));

            var result = new EncounterResolution(EncounterResolutionResult.Completed, "agreement-reached");
            Assert.That(registry.ResolveWithoutCombat(id, result, out EncounterSnapshot resolved), Is.EqualTo(EncounterMutationFailure.None));
            ulong resolvedRevision = resolved.Revision;
            Assert.That(registry.ResolveWithoutCombat(id, result, out resolved), Is.EqualTo(EncounterMutationFailure.None));
            Assert.That(resolved.Revision, Is.EqualTo(resolvedRevision));
            Assert.That(registry.ResolveWithoutCombat(id,
                new EncounterResolution(EncounterResolutionResult.Failed, "different"), out _),
                Is.EqualTo(EncounterMutationFailure.ConflictingResolution));
            Assert.That(registry.Cleanup(id, out EncounterSnapshot cleaned), Is.EqualTo(EncounterMutationFailure.None));
            Assert.That(cleaned.Lifecycle, Is.EqualTo(EncounterLifecycleState.Cleaned));
            Assert.That(registry.Activate(activation, out _), Is.EqualTo(EncounterMutationFailure.InvalidTransition));
        }

        [Test]
        public void MembershipUsesStableCharacterIdsAndCleanupOnlyTargetsEncounterOwnedCharacters()
        {
            CharacterSnapshot persistent = Active("npc:merchant");
            CharacterSnapshot temporary = Active("encounter:guard");
            CharacterSnapshot defeated = Defeated("npc:defeated");
            var registry = NewNonCombatRegistry(new FakeCharacters(persistent, temporary, defeated), out EncounterId id);

            var tempParticipant = new EncounterParticipant(temporary.Id, EncounterParticipantOwnership.EncounterOwned, "guard");
            var persistentParticipant = new EncounterParticipant(persistent.Id, EncounterParticipantOwnership.Persistent, "merchant");
            Assert.That(registry.Join(id, tempParticipant, out _), Is.EqualTo(EncounterMutationFailure.None));
            Assert.That(registry.Join(id, persistentParticipant, out EncounterSnapshot joined), Is.EqualTo(EncounterMutationFailure.None));
            Assert.That(registry.Join(id, persistentParticipant, out EncounterSnapshot duplicate), Is.EqualTo(EncounterMutationFailure.None));
            Assert.That(duplicate.Revision, Is.EqualTo(joined.Revision));
            Assert.That(duplicate.Membership.Participants[0].CharacterId.Value, Is.EqualTo("encounter:guard"));
            Assert.That(duplicate.Membership.Participants[1].CharacterId.Value, Is.EqualTo("npc:merchant"));
            Assert.That(registry.Join(id,
                new EncounterParticipant(new CharacterId("npc:missing"), EncounterParticipantOwnership.Persistent, "missing"), out _),
                Is.EqualTo(EncounterMutationFailure.UnknownCharacter));
            Assert.That(registry.Join(id,
                new EncounterParticipant(defeated.Id, EncounterParticipantOwnership.Persistent, "defeated"), out _),
                Is.EqualTo(EncounterMutationFailure.DefeatedCharacter));
            Assert.That(registry.Leave(id, new CharacterId("npc:not-member"), out _), Is.EqualTo(EncounterMutationFailure.None));

            Assert.That(registry.Activate(new EncounterActivationRequest(id, "conversation"), out _), Is.EqualTo(EncounterMutationFailure.None));
            Assert.That(registry.ResolveWithoutCombat(id,
                new EncounterResolution(EncounterResolutionResult.Completed, "merchant-safe"), out _), Is.EqualTo(EncounterMutationFailure.None));
            Assert.That(registry.Cleanup(id, out _), Is.EqualTo(EncounterMutationFailure.None));

            IReadOnlyList<EncounterFact> facts = registry.DrainFacts();
            int cleanupCharacters = 0;
            CharacterId cleanedCharacter = default;
            for (int i = 0; i < facts.Count; i++)
            {
                if (facts[i].Kind != EncounterFactKind.CleanupCharacter) continue;
                cleanupCharacters++;
                cleanedCharacter = facts[i].CharacterId;
            }
            Assert.That(cleanupCharacters, Is.EqualTo(1));
            Assert.That(cleanedCharacter, Is.EqualTo(temporary.Id));
        }

        [Test]
        public void CombatEncounterRequestsCombatSemanticallyThenConsumesResolution()
        {
            CharacterSnapshot player = Active("player:local");
            CharacterSnapshot bandit = Active("enemy:bandit");
            var registry = new EncounterRegistry(new FakeCharacters(player, bandit));
            var id = new EncounterId("forest-bandits");
            registry.Register(new EncounterDefinition(id, EncounterCombatPolicy.Required, "ambush"), out _);
            registry.Join(id, new EncounterParticipant(player.Id, EncounterParticipantOwnership.Persistent, "player"), out _);
            registry.Join(id, new EncounterParticipant(bandit.Id, EncounterParticipantOwnership.EncounterOwned, "enemy"), out _);

            Assert.That(registry.Activate(new EncounterActivationRequest(id, "proximity", "kentridge-road-band"), out _),
                Is.EqualTo(EncounterMutationFailure.None));
            Assert.That(registry.TryTakeCombatRequest(out EncounterCombatRequest combat), Is.True);
            Assert.That(combat.EncounterId, Is.EqualTo(id));
            Assert.That(combat.Participants.Count, Is.EqualTo(2));
            Assert.That(registry.ResolveWithoutCombat(id,
                new EncounterResolution(EncounterResolutionResult.Completed, "bypassed-combat"), out _),
                Is.EqualTo(EncounterMutationFailure.CombatRequired));

            Assert.That(registry.ApplyCombatResolved(id,
                new EncounterResolution(EncounterResolutionResult.Completed, "combat-victory"), out EncounterSnapshot resolved),
                Is.EqualTo(EncounterMutationFailure.None));
            Assert.That(resolved.Lifecycle, Is.EqualTo(EncounterLifecycleState.Resolved));
            Assert.That(registry.Cleanup(id, out EncounterSnapshot cleaned), Is.EqualTo(EncounterMutationFailure.None));
            Assert.That(cleaned.Membership.Participants.Count, Is.EqualTo(0));
        }

        [Test]
        public void IndependentHightownMarketFixtureResolvesWithoutCombat()
        {
            CharacterSnapshot vendor = Active("npc:hightown-vendor");
            CharacterSnapshot customer = Active("npc:hightown-customer");
            var registry = new EncounterRegistry(new FakeCharacters(vendor, customer));
            var id = new EncounterId("hightown-market-dispute");
            registry.Register(new EncounterDefinition(id, EncounterCombatPolicy.None, "social-dispute"), out _);
            registry.Join(id, new EncounterParticipant(customer.Id, EncounterParticipantOwnership.Persistent, "customer"), out _);
            registry.Join(id, new EncounterParticipant(vendor.Id, EncounterParticipantOwnership.Persistent, "vendor"), out _);

            registry.Activate(new EncounterActivationRequest(id, "dialogue-choice", "hightown-market-stall"), out EncounterSnapshot active);
            Assert.That(active.RealizationId, Is.EqualTo("hightown-market-stall"));
            Assert.That(registry.TryTakeCombatRequest(out _), Is.False);
            Assert.That(registry.ResolveWithoutCombat(id,
                new EncounterResolution(EncounterResolutionResult.Completed, "price-agreed"), out EncounterSnapshot resolved),
                Is.EqualTo(EncounterMutationFailure.None));
            Assert.That(resolved.Resolution.Value.Reason, Is.EqualTo("price-agreed"));
        }

        [Test]
        public void RestorePreservesActiveTruthWithoutReplayingActivationOrCombatRequests()
        {
            CharacterSnapshot scout = Active("npc:scout");
            var source = new EncounterRegistry(new FakeCharacters(scout));
            var id = new EncounterId("ruin-search");
            source.Register(new EncounterDefinition(id, EncounterCombatPolicy.Required, "search"), out _);
            source.Join(id, new EncounterParticipant(scout.Id, EncounterParticipantOwnership.Persistent, "scout"), out _);
            source.Activate(new EncounterActivationRequest(id, "site-entered", "ruin:17"), out _);
            source.TryTakeCombatRequest(out _);
            source.DrainFacts();

            EncounterRegistrySnapshot saved = source.Capture();
            var restored = new EncounterRegistry(new FakeCharacters(scout));
            var events = new List<EncounterEvent>();
            restored.Changed += events.Add;
            Assert.That(restored.Restore(saved), Is.EqualTo(EncounterMutationFailure.None));
            Assert.That(restored.TryGet(id, out EncounterSnapshot snapshot), Is.True);
            Assert.That(snapshot.Lifecycle, Is.EqualTo(EncounterLifecycleState.Active));
            Assert.That(snapshot.Membership.Participants.Count, Is.EqualTo(1));
            Assert.That(snapshot.ActivationCause, Is.EqualTo("site-entered"));
            Assert.That(snapshot.RealizationId, Is.EqualTo("ruin:17"));
            Assert.That(restored.TryTakeCombatRequest(out _), Is.False);
            Assert.That(restored.DrainFacts().Count, Is.EqualTo(0));
            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(events[0].Kind, Is.EqualTo(EncounterEventKind.Restored));
        }

        private static EncounterRegistry NewNonCombatRegistry(FakeCharacters characters, out EncounterId id)
        {
            var registry = new EncounterRegistry(characters);
            id = new EncounterId("merchant-rescue");
            registry.Register(new EncounterDefinition(id, EncounterCombatPolicy.None, "rescue"), out _);
            return registry;
        }

        private static CharacterSnapshot Active(string id) => Snapshot(id, CharacterLifecycleState.Active);
        private static CharacterSnapshot Defeated(string id) => Snapshot(id, CharacterLifecycleState.Defeated);
        private static CharacterSnapshot Snapshot(string id, CharacterLifecycleState lifecycle) =>
            new CharacterSnapshot(new CharacterDefinition(new CharacterId(id), CharacterTraits.None), lifecycle, default, 1);

        private sealed class FakeCharacters : ICharacterQuery
        {
            private readonly SortedDictionary<CharacterId, CharacterSnapshot> _characters =
                new SortedDictionary<CharacterId, CharacterSnapshot>();

            public FakeCharacters(params CharacterSnapshot[] characters)
            {
                for (int i = 0; i < characters.Length; i++) _characters.Add(characters[i].Id, characters[i]);
            }

            public IReadOnlyList<CharacterSnapshot> GetAll()
            {
                var copy = new CharacterSnapshot[_characters.Count];
                int index = 0;
                foreach (KeyValuePair<CharacterId, CharacterSnapshot> pair in _characters) copy[index++] = pair.Value;
                return Array.AsReadOnly(copy);
            }

            public bool TryGet(CharacterId id, out CharacterSnapshot snapshot) => _characters.TryGetValue(id, out snapshot);

            public bool TryResolve(CharacterBinding binding, out CharacterId id)
            {
                id = default;
                return false;
            }
        }
    }
}
