using System.Linq;
using Game.Characters.Api;
using Game.Combat.Api;
using Game.Vitality.Api;
using Game.Vitality.Runtime;
using NUnit.Framework;

namespace Game.Vitality.Tests
{
    public sealed class VitalityRegistryTests
    {
        private static CharacterId Id(string key) => CharacterId.FromStableKey("test", key);

        [Test]
        public void CombatParticipant_FromCharacterPreservesCanonicalIdentity()
        {
            var character = Id("combat-binding");

            var participant = CombatParticipant.FromCharacter(character, CombatTeam.Player);

            Assert.That(participant.IsCharacterBacked, Is.True);
            Assert.That(participant.CharacterId, Is.EqualTo(character));
            Assert.That(participant.Id.Value, Is.EqualTo(character.Value));
            Assert.That(participant.Team, Is.EqualTo(CombatTeam.Player));
        }

        [Test]
        public void ApiAssembly_IsEngineFreeAndDoesNotReferenceRuntime()
        {
            var references = typeof(VitalitySnapshot).Assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray();
            CollectionAssert.DoesNotContain(references, "Game.Vitality.Runtime");
            CollectionAssert.DoesNotContain(references, "UnityEngine");
            CollectionAssert.Contains(references, "Game.Characters.Api");
        }

        [Test]
        public void ApplyDamage_RejectsUnknownAndInvalidDamageDeterministically()
        {
            var registry = new VitalityRegistry();
            var known = Id("known");
            registry.Register(VitalitySnapshot.Alive(known, 10));

            var unknown = registry.ApplyDamage(new DamageRequest(Id("missing"), 1));
            Assert.That(unknown.Accepted, Is.False);
            Assert.That(unknown.RejectionReason, Is.EqualTo(DamageRejectionReason.UnknownCharacter));

            var zero = registry.ApplyDamage(new DamageRequest(known, 0));
            Assert.That(zero.Accepted, Is.False);
            Assert.That(zero.RejectionReason, Is.EqualTo(DamageRejectionReason.InvalidAmount));
            Assert.That(zero.State.Current, Is.EqualTo(10));

            var negative = registry.ApplyDamage(new DamageRequest(known, -3));
            Assert.That(negative.Accepted, Is.False);
            Assert.That(negative.RejectionReason, Is.EqualTo(DamageRejectionReason.InvalidAmount));
            Assert.That(negative.State.Current, Is.EqualTo(10));
        }

        [Test]
        public void ApplyDamage_OrdersNonLethalThenLethalAndClampsAtZero()
        {
            var registry = new VitalityRegistry();
            var character = Id("ordered");
            registry.Register(VitalitySnapshot.Alive(character, 10));

            var first = registry.ApplyDamage(new DamageRequest(character, 3));
            Assert.That(first.Accepted, Is.True);
            Assert.That(first.AppliedAmount, Is.EqualTo(3));
            Assert.That(first.State.Current, Is.EqualTo(7));
            Assert.That(first.DefeatOccurred, Is.False);

            var second = registry.ApplyDamage(new DamageRequest(character, 99));
            Assert.That(second.Accepted, Is.True);
            Assert.That(second.AppliedAmount, Is.EqualTo(7));
            Assert.That(second.State.Current, Is.Zero);
            Assert.That(second.State.IsDefeated, Is.True);
            Assert.That(second.DefeatOccurred, Is.True);
        }

        [Test]
        public void DefeatEvent_IsEmittedExactlyOnceDespiteLateDamage()
        {
            var registry = new VitalityRegistry();
            var character = Id("defeat-once");
            registry.Register(VitalitySnapshot.Alive(character, 4));
            var defeats = 0;
            DefeatEvent observed = default;
            registry.Defeated += evt =>
            {
                defeats++;
                observed = evt;
            };

            var lethal = registry.ApplyDamage(new DamageRequest(character, 4));
            var late = registry.ApplyDamage(new DamageRequest(character, 1));

            Assert.That(lethal.DefeatOccurred, Is.True);
            Assert.That(defeats, Is.EqualTo(1));
            Assert.That(observed.CharacterId, Is.EqualTo(character));
            Assert.That(observed.State.IsDefeated, Is.True);
            Assert.That(late.Accepted, Is.False);
            Assert.That(late.RejectionReason, Is.EqualTo(DamageRejectionReason.AlreadyDefeated));
            Assert.That(late.DefeatOccurred, Is.False);
            Assert.That(defeats, Is.EqualTo(1));
        }

        [Test]
        public void IndependentHazardConsumer_UsesVitalityWithoutCombat()
        {
            IVitalityService registry = new VitalityRegistry();
            var character = Id("hazard");
            registry.Register(VitalitySnapshot.Alive(character, 6));
            var hazard = new IndependentHazard(registry);

            var result = hazard.Apply(character, 2);

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.State.Current, Is.EqualTo(4));
        }

        [Test]
        public void CaptureRestore_RoundTripsAliveAndDefeatedStateWithStableIdentity()
        {
            var source = new VitalityRegistry();
            var alive = Id("alive");
            var defeated = Id("defeated");
            source.Register(new VitalitySnapshot(alive, 7, 10, false));
            source.Register(new VitalitySnapshot(defeated, 0, 5, true));

            var captured = source.Capture();
            var restored = new VitalityRegistry();
            var result = restored.Restore(captured);

            Assert.That(result.Accepted, Is.True);
            Assert.That(restored.TryGet(alive, out var aliveState), Is.True);
            Assert.That(restored.TryGet(defeated, out var defeatedState), Is.True);
            Assert.That(aliveState, Is.EqualTo(new VitalitySnapshot(alive, 7, 10, false)));
            Assert.That(defeatedState, Is.EqualTo(new VitalitySnapshot(defeated, 0, 5, true)));
            Assert.That(aliveState.CharacterId.Value, Is.EqualTo(alive.Value));
            Assert.That(defeatedState.CharacterId.Value, Is.EqualTo(defeated.Value));
        }

        [Test]
        public void Restore_RejectsDuplicateIdentityWithoutMutatingExistingState()
        {
            var registry = new VitalityRegistry();
            var existing = Id("existing");
            var duplicate = Id("duplicate");
            registry.Register(VitalitySnapshot.Alive(existing, 8));

            var result = registry.Restore(new[]
            {
                VitalitySnapshot.Alive(duplicate, 3),
                VitalitySnapshot.Alive(duplicate, 4)
            });

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.RejectionReason, Is.EqualTo(VitalityRestoreRejectionReason.DuplicateCharacter));
            Assert.That(registry.TryGet(existing, out var state), Is.True);
            Assert.That(state.Current, Is.EqualTo(8));
        }

        [Test]
        public void VitalityRuntime_DoesNotReferenceOutcomeOrCombatRuntime()
        {
            var references = typeof(VitalityRegistry).Assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray();
            CollectionAssert.DoesNotContain(references, "Game.Combat.Runtime");
            CollectionAssert.DoesNotContain(references, "Game.Outcomes.Runtime");
        }

        private sealed class IndependentHazard
        {
            private readonly IVitalityService _vitality;

            public IndependentHazard(IVitalityService vitality)
            {
                _vitality = vitality;
            }

            public DamageResult Apply(CharacterId target, int amount) =>
                _vitality.ApplyDamage(new DamageRequest(target, amount));
        }
    }
}
