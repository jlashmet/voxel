# 02 Actor vitality, damage & defeat — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.Vitality.Api` / `Game.Vitality.Runtime`
**Execution rule:** use the published System 03 `CharacterId` contract. Combat consumes Vitality; Vitality never depends on Combat.

## Foundation

- [x] **T02-001 — Baseline existing health state.** Find every authoritative/prototype health, alive/dead, damage, defeat and reset store; classify each as migrate, presentation-only, or obsolete.
  - Evidence: `plan.md` ownership table records `CombatCore.CombatState`, production `Game.Combat.Runtime.CombatService`, `ChainUnitState`/`ChainCombatBoard`, and the `Assets/CombatPrototype` presentation surface, with explicit migration/adapter ownership boundaries.
- [x] **T02-002 — Create/update module assemblies.** Establish `Assets/Game/Vitality/Api` and `Runtime` asmdefs with Runtime -> own API and `Characters.Api`; assert API has no Unity/Runtime dependency.
  - Evidence: `Game.Vitality.Api.asmdef` references only `Game.Characters.Api` with `noEngineReferences=true`; Runtime references API + Characters with `noEngineReferences=true`. Regression asserts the API assembly does not reference Runtime or UnityEngine.
- [x] **T02-003 — Define vitality state contract.** Add immutable snapshot/state keyed by `CharacterId`, with only semantic current/max/defeated data demonstrated by current gameplay.
  - Evidence: immutable `VitalitySnapshot` contains only `CharacterId`, `Current`, `Maximum`, and `IsDefeated`, with invariant validation.
- [x] **T02-004 — Define damage contract.** Add authoritative damage request/result including stable request identity only where existing command delivery can duplicate requests; represent rejection reasons semantically.
  - Evidence: `DamageRequest` carries target + amount only; `DamageResult` reports accepted/rejection reason, applied amount, authoritative state, and defeat transition. No speculative network/idempotency token was added; System 06 retains replication revision ordering.
- [x] **T02-005 — Define defeat transition/event.** Guarantee one transition event when crossing the terminal threshold; do not conflate defeat with removal, combat resolution, or game outcome.
  - Evidence: semantic `DefeatEvent` and `DamageResult.DefeatOccurred`; runtime emits only on alive -> zero crossing.
- [x] **T02-006 — Add heal/restore contract only if required.** Inspect current content/tests first; do not invent revive/respawn semantics.
  - Evidence: current content demonstrates initialization/reset plus damage/defeat only; no in-session heal/revive operation is present. No public healing/revive command was added. Snapshot restore remains a persistence seam.

## Runtime

- [x] **T02-010 — Implement vitality registry.** Key all authoritative vitality by `CharacterId`; reject unknown/removed characters deterministically.
  - Evidence: `VitalityRegistry` owns one `Dictionary<CharacterId, VitalitySnapshot>`; missing/removed identities return `UnknownCharacter`.
- [x] **T02-011 — Implement deterministic damage application.** Validate input, clamp state, preserve ordering, and return the resulting authoritative state.
  - Evidence: non-positive damage is rejected, accepted damage clamps to remaining vitality, and the stored resulting snapshot is returned.
- [x] **T02-012 — Implement exactly-once defeat transition.** Additional damage to an already defeated character must not re-emit defeat.
  - Evidence: defeated requests return `AlreadyDefeated`; regression counts exactly one defeat event across lethal + late damage.
- [x] **T02-013 — Add capture/restore seam.** Expose API-level snapshot contribution needed by system 16 without referencing Persistence Runtime.
  - Evidence: `IVitalityService.Capture/Restore` uses only API snapshots; capture sorts by `CharacterId`, restore validates duplicates before atomic replacement.
- [x] **T02-014 — Add replication projection seam.** Expose current semantic vitality state/events needed by system 06 without referencing GameplayReplication Runtime.
  - Evidence: API-level `TryGet`, `VitalitySnapshot`, and `Defeated` event expose semantic state/transitions; Runtime has no GameplayReplication dependency.
- [ ] **T02-015 — Migrate combat health.** Adapt existing Combat participant health/alive access to Vitality API; remove duplicate authoritative combat health after behavior parity.
- [x] **T02-016 — Migrate non-combat damage consumers.** Route any demonstrated environmental/world damage through the same API to prove vitality is actor-owned rather than combat-owned.
  - Evidence: repository-wide production searches found no demonstrated environmental/world damage consumer outside Combat/prototype life-state code, so there is no existing non-combat consumer to migrate. No synthetic production consumer was added; T02-022 supplies the independent reuse fixture.

## Verification

- [x] **T02-020 — Add damage boundary tests.** Cover zero/invalid damage, lethal/nonlethal ordering, clamping, unknown CharacterId, and deterministic results.
  - Evidence: `VitalityRegistryTests.ApplyDamage_RejectsUnknownAndInvalidDamageDeterministically` and `ApplyDamage_OrdersNonLethalThenLethalAndClampsAtZero`.
- [x] **T02-021 — Add defeat-event regression.** Prove exactly one defeat event despite repeated/late damage requests.
  - Evidence: `DefeatEvent_IsEmittedExactlyOnceDespiteLateDamage`.
- [x] **T02-022 — Add non-combat reuse fixture.** Damage a character without starting Combat and prove identical vitality semantics.
  - Evidence: test-only `IndependentHazard` depends only on `IVitalityService` and damages a character without any Combat reference.
- [x] **T02-023 — Add snapshot/restore tests.** Alive and defeated state must round-trip exactly with stable CharacterId.
  - Evidence: `CaptureRestore_RoundTripsAliveAndDefeatedStateWithStableIdentity` plus duplicate restore atomicity regression.
- [x] **T02-024 — Verify defeat does not resolve game.** Assert no direct dependency/call to Outcomes or session teardown.
  - Evidence: runtime assembly regression asserts no `Game.Outcomes.Runtime` or `Game.Combat.Runtime` reference; final repository audit remains T02-031/T02-032.
- [ ] **T02-025 — Run automatic module tests and dependent Combat tests.** Do not manually enumerate CI tests.

## Cleanup / close

- [ ] **T02-030 — Repository-wide duplicate-state search.** Remove or demote every remaining authoritative `health`/`isAlive` store outside Vitality where it represents the same character life truth.
- [ ] **T02-031 — Boundary audit.** Confirm no external assembly references `Game.Vitality.Runtime` and no Unity object appears in Vitality API.
- [ ] **T02-032 — Close with ownership proof.** Document that character life state has exactly one owner and combat/game-outcome semantics remain separate.
