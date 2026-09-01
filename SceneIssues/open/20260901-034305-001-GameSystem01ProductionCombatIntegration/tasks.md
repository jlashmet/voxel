# 01 Production combat integration — tasks

**Plan:** [plan.md](plan.md)
**Ownership:** existing `Game.Combat.Api` / `Game.Combat.Runtime` plus thin composition adapters.
**Execution:** work top-to-bottom unless a dependency is explicitly blocked. Do not create a second combat runtime.

## Baseline and contracts

- [x] **T01-001 — Inventory current combat ownership.** `CombatService` historically owned authoritative `_hitPoints` (6 HP, 2 damage); `CombatParticipant` owns team; `KentridgeForestBanditEncounter` locally constructs Input/Combat services, creates participant identities, spawns bandits, starts combat, and settles encounter state. Evidence recorded in `plan.md`.
- [ ] **T01-002 — Lock the module boundary.** **BLOCKED on final production composition/Vitality migration.** `Game.Combat.Api` consumes engine-free Characters/Encounters APIs and `Game.Combat.Runtime` remains engine-free; Runtime now also consumes only engine-free `Game.Vitality.Api`. Current `Game.Composition.Kentridge.Playable.asmdef` still directly references `Game.Combat.Runtime` and `Game.Input.Runtime`, so final boundary cleanup waits for the production composition replacement.
- [x] **T01-003 — Define character-backed participant binding.** `Game.Combat.Api` references engine-free `Game.Characters.Api`; `CombatParticipant.FromCharacter(CharacterId, CombatTeam)` preserves the production `CharacterId`, derives the combat participant identity from its serialized value, and carries only Combat team semantics. `CombatCharacterBindingTests` proves the binding with a non-Kentridge fixture.
- [x] **T01-004 — Define combat-start request/result.** Real `Game.Encounters.Api.EncounterId` keys `CombatStartRequest`/`CombatStartResult`; the request carries only already-mapped Combat participants, so Encounter role-to-team policy stays in composition rather than shared Combat.
- [x] **T01-005 — Define combat-resolution fact.** `CombatResolved` carries only `EncounterId`, `CombatSessionId`, and `CombatTeam`; it deliberately does not embed `EncounterResolution`, cleanup, campaign, or victory policy. `CombatEncounterContractTests` validates the contract against the real Encounters consumer types.

## Runtime migration

- [ ] **T01-010 — Replace prototype health authority.** **PARTIAL, final production seam pending.** Per explicit coordinator/user direction, agent-3 copied the already-defined System 02 `Game.Vitality.Api` contract verbatim from `fixes/agent-9` (same contents and Unity GUIDs) to break the publication-order deadlock without taking ownership of Vitality Runtime. `CombatService(IVitalityService)` now reads character-backed current/alive state directly from Vitality and preserves pre-existing actor state. The parameterless legacy HP fallback remains only for existing non-migrated production/tests until Kentridge can inject the real Vitality Runtime; this checkbox stays open until that fallback is removed from production use.
- [ ] **T01-011 — Route damage through Vitality.** **PARTIAL, final production seam pending.** In the Vitality-backed constructor path, accepted attacks call `IVitalityService.ApplyDamage(DamageRequest)` and do not mutate a Combat character-health store. Legacy parameterless combat still exists for the not-yet-migrated Kentridge bootstrap, so final completion waits for production composition/runtime publication.
- [ ] **T01-012 — Preserve combat-only state.** Vitality-backed migration changes only life-state reads/damage. Round/readiness (`ChainRoundReadinessCoordinator`), committed tactical intents/enemy phase (`ChainEnemyTacticalAI`), reaction ownership (`ChainReactionReservationCoordinator`), planning/history (`ChainExecutionPlan`), and board/motion state remain Combat concerns. Final checkbox waits for the production migration/blast-radius run.
- [x] **T01-013 — Integrate Encounter ownership.** `EncounterCombatCoordinator` is a thin engine-free adapter over the existing `CombatService`: it starts a session from `CombatStartRequest`, remembers the owning `EncounterId`, and emits exactly one `CombatResolved` fact after authoritative Combat completion. `EncounterCombatIntegrationTests` uses the real `EncounterRegistry` and keeps winner-to-EncounterResolution policy in composition.
- [ ] **T01-014 — Migrate semantic input.** Current `CombatInputController` already reads `IPlayerInputReader`/`PlayerInputSnapshot` rather than raw keys. Production composition still constructs `Game.Input.Runtime` locally in Kentridge, so final migration waits for the production composition seam.
- [ ] **T01-015 — Remove scene-local combat construction.** **BLOCKED on publication/injection of the real Vitality Runtime into production composition.** Kentridge still locally constructs parameterless `CombatService`/Input runtime. Agent-3 intentionally did not copy System 02 Runtime into this assignment.

## Verification

- [x] **T01-020 — Add participant/vitality regression tests.** `CombatVitalityIntegrationTests` uses an independent `IVitalityService` fixture to prove Combat preserves pre-existing Vitality, routes attack damage through the API, observes external Vitality defeat rather than shadow state, and still settles a lethal combat winner. Exact-SHA CI is still required below.
- [x] **T01-021 — Add resolution/idempotency tests.** `EncounterCombatIntegrationTests` proves `EncounterCombatCoordinator.TryTakeResolved` emits one terminal fact per session under repeated polling and the real `EncounterRegistry.ApplyCombatResolved` accepts an identical repeat without another revision mutation.
- [x] **T01-022 — Add encounter mapping tests.** `EncounterCombatIntegrationTests` proves real `EncounterCombatRequest` membership maps through production CharacterIds into Combat participants and preserves the same `EncounterId` through start and terminal result.
- [ ] **T01-023 — Add independent reuse fixture.** Reuse proof now includes non-Kentridge Character binding, Encounter contract/registry integration, and independent `IVitalityService` Combat tests. Final checkbox remains open until the real Vitality Runtime is present in assembled production composition rather than only the API seam/fake consumer.
- [x] **T01-024 — Run module-owned EditMode/PlayMode tests.** Earlier exact feature SHA `746de4cae082cc456c000153048166c0e4f967e3` passed targeted CI run `33503348443`, validating structural `Game.Combat.Tests`, dependent CharacterAI tests, and automatic Kentridge player integration. A new exact-SHA gate is required for the Vitality API/Combat production changes now on the branch.
- [ ] **T01-025 — Run assembled integration proof.** Earlier Kentridge player gate is green, but this checkbox remains open until Kentridge uses the final Vitality-backed production Combat path.

## Cleanup and close

- [ ] **T01-030 — Search for bypasses.** Baseline audit complete: `KentridgeForestBanditEncounter` remains the production bypass constructing parameterless Combat/Input runtime. Final checkbox remains open until that path injects Vitality and legacy character HP authority is no longer used in production.
- [ ] **T01-031 — Check blast radius.** Verification surface includes module-owned `Game.Combat.Tests` including new `CombatVitalityIntegrationTests`, existing `CombatAuthorityMigrationTests`, `CombatInputModuleBoundaryTests`, `KentridgeCombatEncounterTests`, and older CombatPrototype coverage. Final execution waits for the assembled migration.
- [ ] **T01-032 — Close only with one authority.** Confirm Vitality owns production life state, Encounters owns encounter lifecycle, Combat owns combat resolution, and no duplicate production path remains.

## Active dependency evidence

- Feature is merged through master `1b6d5db96ea150bd0cb573bfaff7e220f19afbeb`; master itself still does not contain `Assets/Game/Vitality`.
- System 02 is not actually unimplemented: `fixes/agent-9` contains completed `Game.Vitality.Api`/Runtime/tests and its task log identifies a publication-order deadlock with System 01. Agent-3 was explicitly directed to inspect those tasks and add the minimum API contract so Combat could be written against it.
- Agent-3 copied only `Assets/Game/Vitality/Api/Game.Vitality.Api.asmdef`, its meta, `VitalityContracts.cs`, and its meta verbatim from `fixes/agent-9`; no System 02 Runtime, tests, or SceneIssue files were modified or copied.
- `Game.Combat.Runtime` now references `Game.Vitality.Api`; `CombatService(IVitalityService)` is the reusable character-backed path. The parameterless legacy path remains temporarily for unmigrated composition and prevents claiming T01-010/T01-011 complete prematurely.
- Current concrete Kentridge seam remains `Assets/Game/Composition/Kentridge/Playable/KentridgeForestBanditEncounter.cs` plus `Game.Composition.Kentridge.Playable.asmdef`.
- Exact feature SHA `746de4cae082cc456c000153048166c0e4f967e3` has prior green `ci/single-test` status from run `33503348443`; it predates this Vitality API/Combat migration and is not the final gate.
- `ci-test/fixes/agent-3` remains the only authorized targeted-CI transport. Never replace a queued/running request.
