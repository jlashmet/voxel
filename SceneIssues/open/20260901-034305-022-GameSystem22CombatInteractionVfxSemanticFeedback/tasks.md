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

- [ ] **T22-020 — Cue mapping/unknown cue tests.** Deterministic config lookup and safe missing mapping.
- [ ] **T22-021 — Dedupe test.** Predicted + confirmed semantic event yields one visible effect.
- [ ] **T22-022 — Persistent reconstruction test.** Current state treatment recreates after reconnect while historical one-shots stay absent.
- [ ] **T22-023 — Authoritative destruction separation test.** Removing VFX cannot change voxel/world mutation result; cosmetic debris cannot create gameplay collisions/damage.
- [ ] **T22-024 — Headless regression.** Gameplay/domain tests pass with Vfx module absent.
- [ ] **T22-025 — Module-local built-player visual validation through shared harness.** Validate real production semantic event -> visible cue mapping.
- [ ] **T22-026 — Production visual-finish defect found in built-player evidence.** Replace flat square/blockout particle presentation with production-readable soft/streaked semantic effects, then inspect new built-player captures directly. Added because the exact-SHA artifact from run `33879743540` was behaviorally green but visually only prototype/blockout quality.
- [ ] **T22-027 — Isolate persistent-aura visual root cause before further production changes.** After two materially different visual passes, use representative collider-free host geometry in the module-local validation fixture to distinguish an incoherent production aura from an evidence scene that was previously binding effects to invisible empty transforms. Do not tune production particles again until this discriminating repro is captured.

## Cleanup / close

- [x] **T22-030 — Remove prefab/VFX identities from gameplay contracts.** Repository/API audit found no gameplay prefab/VFX identity to remove; VFX cue identity remains in `Game.Vfx.Api` only.
- [x] **T22-031 — Remove VFX-owned gameplay mutation/duplicate effect paths.** Repository audit found no existing ParticleSystem path coupled to `ApplyDamage` or voxel authority; new runtime presenter owns no gameplay physics/world writes.
- [ ] **T22-032 — Close with isolation proof.** Authoritative results are identical with VFX enabled or absent and reconnect produces no historical replay.
