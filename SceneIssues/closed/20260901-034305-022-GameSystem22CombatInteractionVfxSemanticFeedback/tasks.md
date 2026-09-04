# 22 Combat / interaction VFX & semantic feedback — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.Vfx.Api` / `Game.Vfx.Runtime`
**Execution rule:** VFX presents confirmed/predicted semantic effects; authoritative damage/world mutation remains in owning gameplay/world modules.

## API / cue model

- [x] **T22-001 — Inventory current gameplay VFX.** No gameplay ParticleSystem/prefab-spawn VFX module or gameplay contract carrying prefab/VFX identities exists on the assigned baseline. Canonical semantic sources are Vitality revision/defeat, sequenced WorldInteractionFact, Outcomes resolution identity and voxel AlterationEvent tick/player/sequence; no duplicate scene-local gameplay effect path requires migration.
- [x] **T22-002 — Establish asmdefs.** `Game.Vfx.Api` is Unity-free and `Game.Vfx.Runtime` owns Unity realization/mapping.
- [x] **T22-003 — Define semantic `VfxCueRef`.** Stable presentation cue identity is independent of prefab/resource name.
- [x] **T22-004 — Define semantic origin/context.** CharacterId/WorldObjectId/world point are used only where needed, with stable `VfxEventId` one-shot identity for dedupe.
- [x] **T22-005 — Define persistent treatment descriptor.** `VfxPersistentTreatmentDescriptor` models only reconstructable current presentation state.
- [x] **T22-006 — Define missing/mapping failure behavior.** Missing mapping/binding emit presentation diagnostics and never become authoritative gameplay failures.

## Runtime / integration

- [x] **T22-010 — Implement cue-to-Unity-effect mapping.** `VfxCueCatalog` and `SemanticVfxPresenter` keep local Unity realization, pooling and lifetimes inside Vfx.Runtime.
- [x] **T22-011 — Subscribe to authoritative result events.** `SemanticVfxFeedbackAdapter` consumes confirmed Vitality defeat/outcome events and semantic damage/interaction/combat/world-alteration results through stable adapters/sinks.
- [x] **T22-012 — Separate cosmetic destruction from authoritative mutation.** Confirmed alteration produces cosmetic debris only after the world event; presenter owns no collider, damage callback or world write.
- [x] **T22-013 — Resolve semantic origins through presentation bindings.** Missing visual bindings safely skip presentation without invalidating semantic processing.
- [x] **T22-014 — Implement prediction/confirmation dedupe.** Stable `VfxEventId` reconciliation causes predicted + confirmed representations of the same semantic event to play once.
- [x] **T22-015 — Reconstruct persistent treatments from current state.** Current defeated Vitality state rebuilds its treatment; historical one-shots are not replayed.
- [x] **T22-016 — Remove duplicate scene-local semantic effect spawners after parity.** Baseline/repository audit found no gameplay scene-local VFX spawner requiring migration; environmental decoration remains separate.

## Verification

- [x] **T22-020 — Cue mapping/unknown cue tests.** Exact-SHA run `33900280992` passed `ConfirmedWorldFacts_MapToStableSemanticCueIdentities` and `CueMapping_UnknownCueIsPresentationOnlyFailure` in `Game.Vfx.Tests.SemanticVfxTests`.
- [x] **T22-021 — Dedupe test.** Exact-SHA run `33900280992` passed `PredictedThenConfirmed_SameSemanticEventPlaysOneEffect`; module player logged predicted=`Played`, confirmed=`Deduplicated`, plays=`1`.
- [x] **T22-022 — Persistent reconstruction test.** Exact-SHA run `33900280992` passed `PersistentRebuild_UsesCurrentVitalityWithoutHistoricalOneShots` and `PersistentReconcile_RemovesStaleVisualWhenBindingDisappears`; player reconnect logged persistent=`1` with historical plays unchanged (`4` -> `4`).
- [x] **T22-023 — Authoritative destruction separation test.** Exact-SHA run `33900280992` passed `CosmeticDebrisPresenter_HasNoGameplayPhysicsComponents` and `VitalityAuthority_IsIdenticalWithVfxPresentOrAbsent`; module player logged `gameplayPhysics=0` before and after cosmetic debris.
- [x] **T22-024 — Headless regression.** Exact-SHA run `33900280992` passed `HeadlessVitality_RemainsUsableWithoutCreatingVfxRuntimeObjects` and the VFX-present/absent authority-equivalence regression.
- [x] **T22-025 — Module-local built-player visual validation through shared harness.** Exact-SHA run `33900280992` passed repository-selected module validation and standalone SceneIssue replay. The real production presenter logged stable semantic hit, defeat, interaction, destruction and reconnect behavior; artifact `9948049312` contains the module-player captures inspected directly.
- [x] **T22-026 — Production visual-finish defect found in built-player evidence.** The original flat square/blockout defect from run `33879743540` is resolved. Direct inspection of final run `33900280992` shows a deliberate gold impact starburst, streaked earth/debris burst and host-relative red defeated treatment; prior exact run `33898724727` directly captured the refined compact cyan interaction spark on the same production presenter path.
- [x] **T22-027 — Isolate persistent-aura visual root cause before further production changes.** Final exact-SHA run `33900280992` uses representative collider-free host geometry. Direct reconnect captures show the red persistent treatment anchored to/following the visible character silhouette, proving the prior blank-sky point-cloud diagnosis was confounded by the invisible-host evidence fixture; no third speculative production tuning pass was warranted.

## Cleanup / close

- [x] **T22-030 — Remove prefab/VFX identities from gameplay contracts.** Repository/API audit found no gameplay prefab/VFX identity to remove; VFX cue identity remains in `Game.Vfx.Api` only.
- [x] **T22-031 — Remove VFX-owned gameplay mutation/duplicate effect paths.** Repository audit found no existing ParticleSystem path coupled to `ApplyDamage` or voxel authority; new runtime presenter owns no gameplay physics/world writes.
- [x] **T22-032 — Close with isolation proof.** Exact-SHA run `33900280992` passed `VitalityAuthority_IsIdenticalWithVfxPresentOrAbsent`; built-player reconnect restored only one current persistent treatment and left historical one-shot play count unchanged.
