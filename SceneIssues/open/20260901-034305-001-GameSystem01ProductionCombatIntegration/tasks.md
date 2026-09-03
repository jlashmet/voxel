# 01 Production combat integration — tasks

**Plan:** [plan.md](plan.md)
**Ownership:** existing `Game.Combat.Api` / `Game.Combat.Runtime` plus thin composition adapters.
**Execution:** work top-to-bottom unless a dependency is explicitly blocked. Do not create a second combat runtime.

## Baseline and contracts

- [x] **T01-001 — Inventory current combat ownership.** `CombatService` historically owned authoritative `_hitPoints` (6 HP, 2 damage); `CombatParticipant` owns team; `KentridgeForestBanditEncounter` locally constructs Input/Combat services, creates participant identities, spawns bandits, starts combat, and settles encounter state. Evidence recorded in `plan.md`.
- [ ] **T01-002 — Lock the module boundary.** **BLOCKED on final production composition/Vitality Runtime migration.** `Game.Combat.Api` consumes engine-free Characters/Encounters APIs and `Game.Combat.Runtime` remains engine-free; Runtime consumes only engine-free `Game.Vitality.Api`. Current Kentridge production composition still owns direct runtime construction.
- [x] **T01-003 — Define character-backed participant binding.** `Game.Combat.Api` references engine-free `Game.Characters.Api`; `CombatParticipant.FromCharacter(CharacterId, CombatTeam)` preserves the production `CharacterId`, derives the combat participant identity from its serialized value, and carries only Combat team semantics. `CombatCharacterBindingTests` proves the binding with a non-Kentridge fixture.
- [x] **T01-004 — Define combat-start request/result.** Real `Game.Encounters.Api.EncounterId` keys `CombatStartRequest`/`CombatStartResult`; the request carries only already-mapped Combat participants, so Encounter role-to-team policy stays in composition rather than shared Combat.
- [x] **T01-005 — Define combat-resolution fact.** `CombatResolved` carries only `EncounterId`, `CombatSessionId`, and `CombatTeam`; it deliberately does not embed `EncounterResolution`, cleanup, campaign, or victory policy. `CombatEncounterContractTests` validates the contract against the real Encounters consumer types.

## Runtime migration

- [ ] **T01-010 — Replace prototype health authority.** **PARTIAL, final production seam pending.** `CombatService(IVitalityService)` reads character-backed current/alive state directly from Vitality and routes accepted damage through the API. The parameterless legacy HP fallback remains only for existing non-migrated production/tests until Kentridge can inject a real Vitality Runtime.
- [ ] **T01-011 — Route damage through Vitality.** **PARTIAL, final production seam pending.** In the Vitality-backed constructor path, accepted attacks call `IVitalityService.ApplyDamage(DamageRequest)` and do not mutate a Combat character-health store. Legacy parameterless combat remains for the not-yet-migrated Kentridge bootstrap.
- [ ] **T01-012 — Preserve combat-only state.** Vitality-backed migration changes only life-state reads/damage. Round/readiness (`ChainRoundReadinessCoordinator`), committed tactical intents/enemy phase (`ChainEnemyTacticalAI`), reaction ownership (`ChainReactionReservationCoordinator`), planning/history (`ChainExecutionPlan`), and board/motion state remain Combat concerns. Final checkbox waits for production migration/blast-radius validation.
- [x] **T01-013 — Integrate Encounter ownership.** `EncounterCombatCoordinator` is a thin engine-free adapter over the existing `CombatService`: it starts a session from `CombatStartRequest`, remembers the owning `EncounterId`, and emits exactly one `CombatResolved` fact after authoritative Combat completion. `EncounterCombatIntegrationTests` uses the real `EncounterRegistry` and keeps winner-to-EncounterResolution policy in composition.
- [ ] **T01-014 — Migrate semantic input.** Current `CombatInputController` already reads `IPlayerInputReader`/`PlayerInputSnapshot` rather than raw keys. Production composition still constructs input/runtime locally in Kentridge, so final migration waits for the production composition seam.
- [ ] **T01-015 — Remove scene-local combat construction.** **BLOCKED on publication/injection of a real Vitality Runtime into production composition.** Current master has convention-owned `Game.Vitality.Api` and `Game.Vitality.Tests`, but no `Assets/Game/Vitality/Runtime`; agent-3 will not invent, copy, merge, or cherry-pick System 02 Runtime ownership from another assignment branch.

## Verification

- [x] **T01-020 — Add participant/vitality regression tests.** `CombatVitalityIntegrationTests` uses an independent `IVitalityService` fixture to prove Combat preserves pre-existing Vitality, routes attack damage through the API, observes external Vitality defeat rather than shadow state, and still settles a lethal combat winner.
- [x] **T01-021 — Add resolution/idempotency tests.** `EncounterCombatIntegrationTests` proves `EncounterCombatCoordinator.TryTakeResolved` emits one terminal fact per session under repeated polling and the real `EncounterRegistry.ApplyCombatResolved` accepts an identical repeat without another revision mutation.
- [x] **T01-022 — Add encounter mapping tests.** `EncounterCombatIntegrationTests` proves real `EncounterCombatRequest` membership maps through production CharacterIds into Combat participants and preserves the same `EncounterId` through start and terminal result.
- [ ] **T01-023 — Add independent reuse fixture.** Reuse proof includes non-Kentridge Character binding, Encounter registry integration, and independent `IVitalityService` Combat tests. Final checkbox remains open until a real Vitality Runtime is present in assembled production composition.
- [x] **T01-024 — Run module-owned EditMode/PlayMode tests.** Exact-SHA feature `82138a7bd45d923f55750bea1aa17f1a0f914b0f` passed targeted CI run `33639183537`: automatic module validation completed successfully after the prior master merge. Exact request for feature `d8d6bd560e2eb7cd0950f3283ee25806e2d2653a` also passed all selected CharacterAI/Combat/Continuity/GameplayReplication/Vitality Unity tests before the then-unfixed player teardown failure described in T01-031.
- [ ] **T01-025 — Run assembled integration proof.** The prior Kentridge player built, produced seven timed captures, and completed the 80-second scenario with `assertion failures 0`. Final acceptance remains open until Kentridge uses the real Vitality-backed production Combat path and the exact player gate exits cleanly.

## Cleanup and close

- [ ] **T01-030 — Search for bypasses.** Baseline audit complete: `KentridgeForestBanditEncounter` remains the production bypass constructing parameterless Combat/Input runtime. Final checkbox remains open until that path injects Vitality and legacy character HP authority is no longer used in production.
- [ ] **T01-031 — Check blast radius.** **READY FOR FRESH EXACT-SHA VALIDATION.** Previous exact run `33714366352` passed all selected Unity module tests and the Kentridge scenario assertions, then failed during renderer teardown in `GpuSurfaceMirrorCoordinator.DetachPageArena`. Master `f5593cc1236ba3963fc5713a11df35292628e97d` now contains the GPU renderer restoration, including the CPU-only arena-detach lifetime fix, and has been merged into `fixes/agent-3`; submit a fresh exact-SHA request on the existing `ci-test/fixes/agent-3` transport and inspect the completed result.
- [ ] **T01-032 — Close only with one authority.** Confirm Vitality owns production life state, Encounters owns encounter lifecycle, Combat owns combat resolution, and no duplicate production path remains.

## Active dependency / CI evidence

- Current feature branch merged master `f5593cc1236ba3963fc5713a11df35292628e97d`, which contains the GPU renderer restoration that addresses the prior teardown blocker.
- Current master still contains only `Assets/Game/Vitality/Api` and `Assets/Game/Vitality/Tests`; there is no `Assets/Game/Vitality/Runtime`, so production Kentridge injection remains externally blocked.
- System 02 dependency branch `fixes/agent-9` contains `Assets/Game/Vitality/Runtime/VitalityRegistry.cs`; inspection confirms `VitalityRegistry : IVitalityService` provides registration, authoritative damage, defeat events, capture, and restore. This narrows the dependency to normal publication onto master; agent-3 will not consume the unmerged assignment branch directly.
- During earlier master synchronization, agent-3 preserved master Vitality Unity GUIDs and `IVitalityQuery`/revision fields and added only damage/service semantics required by Combat; master’s `ICombatService` semantic surface is retained.
- Exact-SHA run `33639183537` on feature `82138a7bd45d923f55750bea1aa17f1a0f914b0f` completed successfully for automatic module validation; standalone replay was skipped by that planner result.
- Exact request commit `ab648a9966bfb2c7354c2ecdf17f305ab838ddd5` for feature `d8d6bd560e2eb7cd0950f3283ee25806e2d2653a` ran as `33714366352`. Module tests and scenario assertions passed; the overall run failed only after scenario completion during GPU renderer teardown. That product blocker has now received an upstream master fix, so a fresh exact-SHA validation is justified rather than an unchanged retry.
- `ci-test/fixes/agent-3` remains the only authorized targeted-CI transport. Never replace queued/running work.
