# 01 Production combat integration — tasks

**Plan:** [plan.md](plan.md)
**Ownership:** existing `Game.Combat.Api` / `Game.Combat.Runtime` plus thin composition adapters.
**Execution:** work top-to-bottom unless a dependency is explicitly blocked. Do not create a second combat runtime.

## Baseline and contracts

- [x] **T01-001 — Inventory current combat ownership.** Baseline showed Combat-owned HP and Kentridge-local bootstrap; evidence is recorded in `plan.md`.
- [x] **T01-002 — Lock the module boundary.** `Game.Combat.Api` consumes engine-free Characters/Encounters APIs; `Game.Combat.Runtime` consumes Combat/Input/Vitality/Characters/Encounters engine-free APIs. Production scene policy remains in Kentridge composition.
- [x] **T01-003 — Define character-backed participant binding.** `CombatParticipant.FromCharacter(CharacterId, CombatTeam)` preserves production identity; `CombatCharacterBindingTests` provides an independent fixture.
- [x] **T01-004 — Define combat-start request/result.** `CombatStartRequest`/`CombatStartResult` are keyed by real `EncounterId` and carry already-mapped Combat participants only.
- [x] **T01-005 — Define combat-resolution fact.** `CombatResolved` carries only `EncounterId`, `CombatSessionId`, and winning Combat team; scene/game-result policy is not embedded in shared Combat.

## Runtime migration

- [x] **T01-010 — Replace prototype health authority.** Current production `CombatService` requires injected `IVitalityService`; the parameterless HP fallback is absent. Kentridge composes a real `VitalityRegistry` and injects it.
- [x] **T01-011 — Route damage through Vitality.** Combat routes accepted attacks through the landed `CombatVitalityAdapter`/`IVitalityService.ApplyDamage` and reads HP/alive state from Vitality.
- [x] **T01-012 — Preserve combat-only state.** Combat retains positioning, turns, tactical execution, team/winner and resolution state while Vitality owns actor life truth. Existing Chain orchestration remains untouched.
- [x] **T01-013 — Integrate Encounter ownership.** `EncounterCombatCoordinator` starts from semantic `CombatStartRequest`, retains owning `EncounterId`, and emits one terminal `CombatResolved`; independent tests exercise the real Encounter registry.
- [x] **T01-014 — Migrate semantic input.** `CombatInputController` consumes `IPlayerInputReader`/semantic snapshots; Kentridge owns only context/composition policy and contains no raw key/button combat polling.
- [x] **T01-015 — Remove legacy production combat construction.** Kentridge now creates `VitalityRegistry` and injects it into `CombatService`, registers real CharacterIds, and maps Encounter members through `CombatParticipant.FromCharacter`. Repository search finds no `new CombatService()` parameterless construction on current master.

## Verification

- [x] **T01-020 — Add participant/vitality regression tests.** Independent `IVitalityService` fixture proves preserved pre-existing Vitality, API-routed damage, external defeat observation, and lethal winner settlement.
- [x] **T01-021 — Add resolution/idempotency tests.** `EncounterCombatIntegrationTests` proves one terminal coordinator fact and idempotent Encounter resolution behavior.
- [x] **T01-022 — Add encounter mapping tests.** Independent fixture maps real `EncounterCombatRequest` membership through CharacterIds and preserves Encounter identity through Combat start/result.
- [x] **T01-023 — Add independent reuse fixture.** Non-Kentridge Character binding, Encounter registry integration, independent Vitality service, and landed production Vitality tests demonstrate reusable boundaries outside the Kentridge composition.
- [x] **T01-024 — Run module-owned tests baseline.** Previous exact-SHA run `33800856291` passed affected module tests and renderer-restored Kentridge baseline. A fresh final gate is being submitted for the production cutover head.
- [ ] **T01-025 — Run assembled integration proof.** Production Kentridge is now Vitality-backed; final checkbox waits for fresh exact-SHA built-player evidence on the current feature head.

## Cleanup and close

- [x] **T01-030 — Search for bypasses.** Current master/feature has no parameterless `new CombatService()` production construction; Kentridge uses `new CombatService(_vitality)` with real CharacterId/Vitality registration.
- [ ] **T01-031 — Check blast radius.** Final post-cutover exact-SHA automatic module + Kentridge player run pending.
- [ ] **T01-032 — Close only with one authority.** Code audit shows Combat alive/HP decisions consume Vitality exclusively, Encounters owns lifecycle/resolution application, and Combat owns combat orchestration/result. Final checkbox waits for exact-SHA validation and final post-CI audit.

## Active dependency / CI evidence

- System 02 dependency is no longer blocked: master `81ffa4bbc76c3feb6e0bde2376065b4144f3f10a` contains `Assets/Game/Vitality/Runtime/VitalityRegistry.cs`, Combat/Vitality integration, Kentridge production migration, and System 02 closure.
- Agent-3 merged that master as `b53a2abff95475e6030da475706b3a8478d90ef9`, retaining landed production Combat/Vitality behavior and restoring agent-3 Encounter semantic contracts/coordinator/reuse tests.
- Agent-3 tests were moved under the landed `Assets/Game/Combat/Tests/EditMode` assembly and its references were unioned with Encounters and Vitality Runtime to avoid a duplicate `Game.Combat.Tests` assembly.
- Previous exact request `74eae6b52d81462edbe250c13af48801298dacac` / run `33800856291` is baseline evidence only and does not satisfy final post-cutover acceptance.
- `ci-test/fixes/agent-3` remains the only authorized targeted-CI transport. Never replace queued/running work.
