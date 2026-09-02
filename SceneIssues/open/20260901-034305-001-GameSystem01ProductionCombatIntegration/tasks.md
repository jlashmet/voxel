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
- [ ] **T01-015 — Remove scene-local combat construction.** **BLOCKED on publication/injection of a real Vitality Runtime into production composition.** Current `master` now has convention-owned `Game.Vitality.Api` and `Game.Vitality.Tests`, but no `Assets/Game/Vitality/Runtime`; agent-3 will not invent or copy System 02 Runtime ownership.

## Verification

- [x] **T01-020 — Add participant/vitality regression tests.** `CombatVitalityIntegrationTests` uses an independent `IVitalityService` fixture to prove Combat preserves pre-existing Vitality, routes attack damage through the API, observes external Vitality defeat rather than shadow state, and still settles a lethal combat winner.
- [x] **T01-021 — Add resolution/idempotency tests.** `EncounterCombatIntegrationTests` proves `EncounterCombatCoordinator.TryTakeResolved` emits one terminal fact per session under repeated polling and the real `EncounterRegistry.ApplyCombatResolved` accepts an identical repeat without another revision mutation.
- [x] **T01-022 — Add encounter mapping tests.** `EncounterCombatIntegrationTests` proves real `EncounterCombatRequest` membership maps through production CharacterIds into Combat participants and preserves the same `EncounterId` through start and terminal result.
- [ ] **T01-023 — Add independent reuse fixture.** Reuse proof includes non-Kentridge Character binding, Encounter registry integration, and independent `IVitalityService` Combat tests. Final checkbox remains open until a real Vitality Runtime is present in assembled production composition.
- [x] **T01-024 — Run module-owned EditMode/PlayMode tests.** Exact-SHA feature `82138a7bd45d923f55750bea1aa17f1a0f914b0f` passed targeted CI run `33639183537`: automatic module validation completed successfully after the master merge, proving the old Vitality API fallback blocker is gone. The workflow skipped standalone replay, so assembled production proof remains T01-025.
- [ ] **T01-025 — Run assembled integration proof.** Earlier Kentridge player gate is green, but this checkbox remains open until Kentridge uses the final Vitality-backed production Combat path.

## Cleanup and close

- [ ] **T01-030 — Search for bypasses.** Baseline audit complete: `KentridgeForestBanditEncounter` remains the production bypass constructing parameterless Combat/Input runtime. Final checkbox remains open until that path injects Vitality and legacy character HP authority is no longer used in production.
- [ ] **T01-031 — Check blast radius.** Verification surface includes module-owned `Game.Combat.Tests`, dependent CharacterAI/Characters/Encounters tests, convention-owned `Game.Vitality.Tests`, and automatic Kentridge player validation.
- [ ] **T01-032 — Close only with one authority.** Confirm Vitality owns production life state, Encounters owns encounter lifecycle, Combat owns combat resolution, and no duplicate production path remains.

## Active dependency / CI evidence

- `fixes/agent-3` is merged through master `b1b69290a59278b0e7caba798641c76a9866aa5c` and was behind master by 0 commits at the last comparison before this documentation-only update.
- Current master contains `Assets/Game/Vitality/Api` and `Assets/Game/Vitality/Tests`; the old planner broad-fallback blocker is resolved. Master still has no `Assets/Game/Vitality/Runtime`, so production Kentridge injection remains externally blocked.
- During the master sync, agent-3 preserved master Vitality Unity GUIDs and master `IVitalityQuery`/revision fields, then added only the damage/service contract Combat needs. Master’s newer `ICombatService` semantic surface was also preserved.
- `Game.Combat.Runtime` directly references `Game.Characters.Api` because Vitality signatures expose `CharacterId`; this fixed exact-SHA run `33520829134` compiler errors.
- Exact-SHA run `33525068619` then passed CharacterAI, Characters, Combat, CharacterEquipment, and Encounters before unrelated Materials fallback failures.
- Exact-SHA run `33639183537` on feature `82138a7bd45d923f55750bea1aa17f1a0f914b0f` completed successfully: automatic module validation passed, screenshot/result/final-status steps passed, and standalone replay was skipped by the planner.
- `ci-test/fixes/agent-3` remains the only authorized targeted-CI transport. Never replace a queued/running request.
