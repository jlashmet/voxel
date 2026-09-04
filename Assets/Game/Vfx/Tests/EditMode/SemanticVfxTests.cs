using System.Collections.Generic;
using Game.Characters.Api;
using Game.Vfx.Api;
using Game.Vfx.Runtime;
using Game.Vitality.Api;
using Game.Vitality.Runtime;
using Game.WorldObjects.Api;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Edits.Api;

namespace Game.Vfx.Tests
{
    public sealed class SemanticVfxTests
    {
        [Test]
        public void CueMapping_UnknownCueIsPresentationOnlyFailure()
        {
            var backend = new RecordingBackend();
            var diagnostics = new RecordingDiagnostics();
            var coordinator = new VfxCueCoordinator(VfxCueCatalog.CreateDefault(), new FixedBindings(), backend, diagnostics);

            VfxSubmitResult result = coordinator.Submit(new VfxCueRequest(
                new VfxCueRef("unknown.semantic.cue"), new VfxEventId("event:unknown:1"),
                VfxSemanticOrigin.WorldPoint(1f, 2f, 3f), VfxCuePhase.Confirmed));

            Assert.That(result, Is.EqualTo(VfxSubmitResult.MissingMapping));
            Assert.That(backend.OneShotCount, Is.Zero);
            Assert.That(diagnostics.Items.Count, Is.EqualTo(1));
            Assert.That(diagnostics.Items[0].Code, Is.EqualTo(VfxDiagnosticCode.MissingCueMapping));
        }

        [Test]
        public void MissingPresentationBinding_DoesNotConsumeSemanticIdentity()
        {
            var backend = new RecordingBackend();
            var bindings = new ToggleBindings();
            var coordinator = new VfxCueCoordinator(VfxCueCatalog.CreateDefault(), bindings, backend);
            var id = new VfxEventId("damage:character:test:1");
            var request = new VfxCueRequest(GameplayVfxCues.Hit, id,
                VfxSemanticOrigin.Character(new CharacterId("character:test")), VfxCuePhase.Confirmed);

            Assert.That(coordinator.Submit(request), Is.EqualTo(VfxSubmitResult.MissingBinding));
            bindings.Available = true;
            Assert.That(coordinator.Submit(request), Is.EqualTo(VfxSubmitResult.Played));
            Assert.That(backend.OneShotCount, Is.EqualTo(1));
        }

        [Test]
        public void PredictedThenConfirmed_SameSemanticEventPlaysOneEffect()
        {
            var backend = new RecordingBackend();
            var coordinator = new VfxCueCoordinator(VfxCueCatalog.CreateDefault(), new FixedBindings(), backend);
            var id = new VfxEventId("damage:character:hero:9");
            var origin = VfxSemanticOrigin.Character(new CharacterId("character:hero"));

            VfxSubmitResult predicted = coordinator.Submit(new VfxCueRequest(GameplayVfxCues.Hit, id, origin, VfxCuePhase.Predicted));
            VfxSubmitResult confirmed = coordinator.Submit(new VfxCueRequest(GameplayVfxCues.Hit, id, origin, VfxCuePhase.Confirmed));

            Assert.That(predicted, Is.EqualTo(VfxSubmitResult.Played));
            Assert.That(confirmed, Is.EqualTo(VfxSubmitResult.Deduplicated));
            Assert.That(backend.OneShotCount, Is.EqualTo(1));
            Assert.That(coordinator.PlayedIdentityCount, Is.EqualTo(1));
        }

        [Test]
        public void PersistentRebuild_UsesCurrentVitalityWithoutHistoricalOneShots()
        {
            var character = new CharacterId("character:defeated");
            var vitality = new VitalityRegistry();
            vitality.Register(VitalitySnapshot.Alive(character, 10));
            DamageResult damage = vitality.ApplyDamage(new DamageRequest(character, 10));
            Assert.That(damage.DefeatOccurred, Is.True);

            var backend = new RecordingBackend();
            var coordinator = new VfxCueCoordinator(VfxCueCatalog.CreateDefault(), new FixedBindings(), backend);
            VfxPersistentStateRebuilder.RebuildFromVitality(vitality, coordinator);

            Assert.That(backend.PersistentCount, Is.EqualTo(1));
            Assert.That(backend.OneShotCount, Is.Zero);
            Assert.That(coordinator.PlayedIdentityCount, Is.Zero);
            Assert.That(coordinator.ActiveTreatmentCount, Is.EqualTo(1));

            var freshBackend = new RecordingBackend();
            var afterReconnect = new VfxCueCoordinator(VfxCueCatalog.CreateDefault(), new FixedBindings(), freshBackend);
            VfxPersistentStateRebuilder.RebuildFromVitality(vitality, afterReconnect);
            Assert.That(freshBackend.PersistentCount, Is.EqualTo(1));
            Assert.That(freshBackend.OneShotCount, Is.Zero, "Reconnect reconstruction must not replay historical defeat/hit one-shots.");
        }

        [Test]
        public void PersistentReconcile_RemovesStaleVisualWhenBindingDisappears()
        {
            var character = new CharacterId("character:binding-loss");
            var backend = new RecordingBackend();
            var bindings = new ToggleBindings { Available = true };
            var coordinator = new VfxCueCoordinator(VfxCueCatalog.CreateDefault(), bindings, backend);
            var current = new[]
            {
                new VfxPersistentTreatmentDescriptor(
                    new VfxTreatmentId("defeated:" + character.Value), GameplayVfxCues.DefeatedTreatment,
                    VfxSemanticOrigin.Character(character))
            };

            coordinator.Reconcile(current);
            Assert.That(backend.PersistentCount, Is.EqualTo(1));
            Assert.That(coordinator.ActiveTreatmentCount, Is.EqualTo(1));

            bindings.Available = false;
            coordinator.Reconcile(current);

            Assert.That(backend.PersistentCount, Is.Zero, "A missing visual binding must not leave a stale persistent effect at the old location.");
            Assert.That(coordinator.ActiveTreatmentCount, Is.Zero);
        }

        [Test]
        public void VitalityAuthority_IsIdenticalWithVfxPresentOrAbsent()
        {
            var character = new CharacterId("character:authority");
            var withoutVfx = new VitalityRegistry();
            withoutVfx.Register(VitalitySnapshot.Alive(character, 20));
            DamageResult baseline = withoutVfx.ApplyDamage(new DamageRequest(character, 7));

            var withVfx = new VitalityRegistry();
            withVfx.Register(VitalitySnapshot.Alive(character, 20));
            var backend = new RecordingBackend();
            var coordinator = new VfxCueCoordinator(VfxCueCatalog.CreateDefault(), new FixedBindings(), backend);
            using (var adapter = new SemanticVfxFeedbackAdapter(coordinator, withVfx))
            {
                DamageResult actual = withVfx.ApplyDamage(new DamageRequest(character, 7));
                adapter.OnDamageConfirmed(actual);
                Assert.That(actual.Accepted, Is.EqualTo(baseline.Accepted));
                Assert.That(actual.AppliedAmount, Is.EqualTo(baseline.AppliedAmount));
                Assert.That(actual.State, Is.EqualTo(baseline.State));
            }
            Assert.That(backend.OneShotCount, Is.EqualTo(1));
        }

        [Test]
        public void ConfirmedWorldFacts_MapToStableSemanticCueIdentities()
        {
            var backend = new RecordingBackend();
            var coordinator = new VfxCueCoordinator(VfxCueCatalog.CreateDefault(), new FixedBindings(), backend);
            using (var adapter = new SemanticVfxFeedbackAdapter(coordinator))
            {
                adapter.Publish(new WorldInteractionFact(44, new CharacterId("character:a"), new WorldObjectId("door:a"), WorldObjectKind.DoorToggle, 1, 3));
                var alteration = new AlterationEvent(AlterationEvent.KindExplosion, 18, new int3(2, 3, 4), 2, 0, 99, 7, 6);
                Assert.That(adapter.OnAlterationCommitted(alteration), Is.EqualTo(VfxSubmitResult.Played));
                Assert.That(adapter.OnAlterationCommitted(alteration), Is.EqualTo(VfxSubmitResult.Deduplicated));
            }
            Assert.That(backend.OneShotCount, Is.EqualTo(2));
        }

        [Test]
        public void CosmeticDebrisPresenter_HasNoGameplayPhysicsComponents()
        {
            var root = new GameObject("vfx-test-presenter");
            try
            {
                var presenter = root.AddComponent<SemanticVfxPresenter>();
                presenter.PlayOneShot(new VfxEffectProfile(VfxEffectStyle.Debris, 1f, 1f, 12), new VfxWorldPoint(0f, 0f, 0f));
                Assert.That(presenter.CountGameplayPhysicsComponents(), Is.Zero,
                    "Cosmetic debris must not own Collider/Rigidbody gameplay physics.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HeadlessVitality_RemainsUsableWithoutCreatingVfxRuntimeObjects()
        {
            var character = new CharacterId("character:headless");
            var vitality = new VitalityRegistry();
            Assert.That(vitality.Register(VitalitySnapshot.Alive(character, 5)), Is.True);
            DamageResult result = vitality.ApplyDamage(new DamageRequest(character, 5));
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.State.IsDefeated, Is.True);
            Assert.That(Object.FindFirstObjectByType<SemanticVfxPresenter>(), Is.Null);
        }

        private sealed class RecordingBackend : IVfxEffectBackend
        {
            private readonly HashSet<VfxTreatmentId> _persistent = new HashSet<VfxTreatmentId>();
            public int OneShotCount { get; private set; }
            public int PersistentCount => _persistent.Count;
            public void PlayOneShot(VfxEffectProfile profile, VfxWorldPoint point) { OneShotCount++; }
            public void ApplyPersistent(VfxTreatmentId treatmentId, VfxEffectProfile profile, VfxWorldPoint point) { _persistent.Add(treatmentId); }
            public void RemovePersistent(VfxTreatmentId treatmentId) { _persistent.Remove(treatmentId); }
        }

        private sealed class FixedBindings : IVfxPresentationBindingResolver
        {
            public bool TryResolve(VfxSemanticOrigin origin, out VfxWorldPoint point) { point = origin.Kind == VfxOriginKind.WorldPoint ? origin.Point : new VfxWorldPoint(1f, 1f, 1f); return true; }
        }

        private sealed class ToggleBindings : IVfxPresentationBindingResolver
        {
            public bool Available { get; set; }
            public bool TryResolve(VfxSemanticOrigin origin, out VfxWorldPoint point) { point = new VfxWorldPoint(2f, 2f, 2f); return Available; }
        }

        private sealed class RecordingDiagnostics : IVfxDiagnosticsSink
        {
            public readonly List<VfxDiagnostic> Items = new List<VfxDiagnostic>();
            public void Report(VfxDiagnostic diagnostic) { Items.Add(diagnostic); }
        }
    }
}
