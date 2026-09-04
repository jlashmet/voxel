# 01 Production combat integration — tasks

**Plan:** [plan.md](plan.md)
**Ownership:** existing `Game.Combat.Api` / `Game.Combat.Runtime` plus thin composition adapters.
**Execution:** work top-to-bottom unless a dependency is explicitly blocked. Do not create a second combat runtime.

## Baseline and contracts

- [x] **T01-001 — Inventory current combat ownership.** Baseline showed Combat-owned HP and Kentridge-local bootstrap; evidence is recorded in `plan.md`.
- [x] **T01-002 — Lock the module boundary.** `Game.Combat.Api` consumes engine-free Characters/Encounters APIs; `Game.Combat.Runtime` consumes semantic Input/Vitality/Characters/Encounters APIs. Production scene policy remains in Kentridge composition.
- [x] **T01-003 — Define character-backed participant binding.** `CombatParticipant.FromCharacter(CharacterId, CombatTeam)` preserves production identity; `CombatCharacterBindingTests` proves the binding independently.
- [x] **T01-004 — Define combat-start request/result.** `CombatStartRequest`/`CombatStartResult` are keyed by real `EncounterId` and carry already-mapped Combat participants only.
- [x] **T01-005 — Define combat-resolution fact.** `CombatResolved` carries only `EncounterId`, `CombatSessionId`, and winning Combat team; scene/game-result policy is not embedded in shared Combat.

## Runtime migration

- [x] **T01-010 — Replace prototype health authority.** Production `CombatService` requires injected `IVitalityService`; the parameterless HP fallback is absent. Kentridge composes a real `VitalityRegistry` and injects it.
- [x] **T01-011 — Route damage through Vitality.** Combat routes attacks through `IVitalityService.ApplyDamage` and reads HP/alive state from Vitality.
- [x] **T01-012 — Preserve combat-only state.** Combat retains positioning, turns, tactical execution, team/winner and terminal result while Vitality owns actor life truth.
- [x] **T01-013 — Integrate Encounter ownership.** `EncounterCombatCoordinator` retains the owning `EncounterId` and emits one terminal `CombatResolved`; independent tests exercise the real Encounter registry and idempotent terminal application.
- [x] **T01-014 — Migrate semantic input.** `CombatInputController` consumes `IPlayerInputReader`/semantic snapshots; final audit found no raw Combat key/button polling.
- [x] **T01-015 — Remove legacy production combat construction.** Kentridge injects `VitalityRegistry`, registers real CharacterIds, and maps Encounter members through `CombatParticipant.FromCharacter`; final repository audit found no parameterless `new CombatService()` on current master.

## Verification

- [x] **T01-020 — Add participant/vitality regression tests.** Independent Vitality fixture proves preserved state, API-routed damage, external defeat observation, and lethal winner settlement.
- [x] **T01-021 — Add resolution/idempotency tests.** `EncounterCombatIntegrationTests` proves one terminal coordinator fact and idempotent Encounter resolution.
- [x] **T01-022 — Add encounter mapping tests.** Independent fixture maps real Encounter membership through CharacterIds and preserves Encounter identity through Combat start/result.
- [x] **T01-023 — Add independent reuse fixture.** Non-Kentridge Character binding, Encounter registry integration, independent Vitality service, and production Vitality tests demonstrate reusable boundaries.
- [x] **T01-024 — Run module-owned tests.** Final exact-SHA run `33812677873` passed all required persistent EditMode assemblies: `Game.CharacterAI.Tests`, `Game.Combat.Tests` (11 passed), `Game.Continuity.Tests`, and `Game.GameplayReplication.Tests`.
- [x] **T01-025 — Run assembled integration proof.** Final exact-SHA run `33812677873` built `KentridgePlayableSlice`, completed the 80-second player scenario with zero assertion failures, produced seven durable captures, and shut down cleanly.

## Cleanup and close

- [x] **T01-030 — Search for bypasses.** No parameterless `new CombatService()` remains on current master; production Kentridge uses injected Vitality and real CharacterIds.
- [x] **T01-031 — Check blast radius.** Final exact-SHA automatic module validation and canonical Kentridge player gate passed without weakening unrelated budgets or acceptance.
- [x] **T01-032 — Close only with one authority.** Final audit confirms Vitality owns health/alive/defeat truth; Encounters owns participation/lifecycle/terminal resolution; Combat owns combat orchestration/result; Input remains semantic. Character defeat marking in composition is downstream lifecycle projection and is not consulted by Combat as life authority.

## Final CI evidence

- Current master at validation/closure: `81ffa4bbc76c3feb6e0bde2376065b4144f3f10a`.
- Verified feature SHA: `4789867eb2aefc2eae96cc0b5ad75236b6bc0a82`.
- Final exact request commit: `c4c90b298d98adf5494982cf1eee6a39ecc48302`, parented directly to the verified feature SHA on `ci-test/fixes/agent-3`.
- Final run: `33812677873` — success.
- Previous post-cutover run `33811046206` exposed the stale test-only parameterless constructor; fix commit `7db3fa76599ca7ed4b9e68b3db27e73fc588f4fe` migrated that fixture to real Vitality injection before the successful final gate.
- `ci-test/fixes/agent-3` remained the only targeted-CI transport.
