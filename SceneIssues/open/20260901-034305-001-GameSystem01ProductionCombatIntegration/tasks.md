# 01 Production combat integration — tasks

**Plan:** [plan.md](plan.md)
**Ownership:** existing `Game.Combat.Api` / `Game.Combat.Runtime` plus thin composition adapters.
**Execution:** work top-to-bottom unless a dependency is explicitly blocked. Do not create a second combat runtime.

## Baseline and contracts

- [x] **T01-001 — Inventory current combat ownership.** `CombatService` owns authoritative `_hitPoints` (6 HP, 2 damage); `CombatParticipant` owns team; `KentridgeForestBanditEncounter` locally constructs Input/Combat services, creates participant identities, spawns bandits, starts combat, and settles encounter state. Evidence recorded in `plan.md`.
- [ ] **T01-002 — Lock the module boundary.** **BLOCKED on final production composition/Vitality migration.** `Game.Combat.Api` now consumes only engine-free Characters/Encounters APIs and `Game.Combat.Runtime` remains engine-free, but current `Game.Composition.Kentridge.Playable.asmdef` still directly references `Game.Combat.Runtime` and `Game.Input.Runtime`. Removing those references before the production Kentridge composition replacement exists would break the only concrete implementations rather than migrate ownership. `Assets/Game/Composition/CombatEnvironmentRuntime` remains the older `MountingForce.CombatPrototype` experiment and is not a production `Game.Combat.Runtime` consumer.
- [x] **T01-003 — Define character-backed participant binding.** `Game.Combat.Api` references engine-free `Game.Characters.Api`; `CombatParticipant.FromCharacter(CharacterId, CombatTeam)` preserves the production `CharacterId`, derives the combat participant identity from its serialized value, and carries only Combat team semantics. `CombatCharacterBindingTests` proves the binding with a non-Kentridge fixture.
- [x] **T01-004 — Define combat-start request/result.** Real `Game.Encounters.Api.EncounterId` now keys `CombatStartRequest`/`CombatStartResult`; the request carries only already-mapped Combat participants, so Encounter role-to-team policy stays in composition rather than shared Combat.
- [x] **T01-005 — Define combat-resolution fact.** `CombatResolved` carries only `EncounterId`, `CombatSessionId`, and `CombatTeam`; it deliberately does not embed `EncounterResolution`, cleanup, campaign, or victory policy. `CombatEncounterContractTests` validates the contract against the real Encounters consumer types.

## Runtime migration

- [ ] **T01-010 — Replace prototype health authority.** **BLOCKED:** production `Game.Vitality.Api` is absent. Current production Combat authority is documented in `plan.md`.
- [ ] **T01-011 — Route damage through Vitality.** **BLOCKED:** production `Game.Vitality.Api` is absent.
- [ ] **T01-012 — Preserve combat-only state.** Blocked behind T01-010/T01-011 migration, but preservation inventory is complete: round/readiness (`ChainRoundReadinessCoordinator`), committed tactical intents/enemy phase (`ChainEnemyTacticalAI`), reaction ownership (`ChainReactionReservationCoordinator`), planning/history (`ChainExecutionPlan`), and board/motion state remain Combat concerns. `CombatAuthorityMigrationTests` already exercises cascade/reaction ownership, deterministic plan replay, deterministic enemy planning, and assembly isolation. These `MountingForce.CombatPrototype` internals are blast-radius fixtures, not targets for opportunistic life-state migration.
- [x] **T01-013 — Integrate Encounter ownership.** `EncounterCombatCoordinator` is a thin engine-free adapter over the existing `CombatService`: it starts a session from `CombatStartRequest`, remembers the owning `EncounterId`, and emits exactly one `CombatResolved` fact after authoritative Combat completion. `EncounterCombatIntegrationTests` uses the real `EncounterRegistry`: Encounters registers/owns membership and activation, queues `EncounterCombatRequest`, fixture composition maps semantic roles to Combat teams, Combat resolves, and Encounters alone applies the terminal `EncounterResolution`. Kentridge scene wiring/removal of local bootstrap remains T01-015.
- [ ] **T01-014 — Migrate semantic input.** Current `CombatInputController` already reads `IPlayerInputReader`/`PlayerInputSnapshot` rather than raw keys, and `CombatInputModuleBoundaryTests` drives it with a synthetic semantic reader. Production composition still constructs `Game.Input.Runtime` locally in Kentridge, so final migration is blocked on the production composition seam that replaces scene-local construction.
- [ ] **T01-015 — Remove scene-local combat construction.** **BLOCKED on the final production composition/Vitality seam.** Encounters now has a production lifecycle owner, but Kentridge still locally constructs Combat/Input services and current Combat still owns life state. Replacing only part of that bootstrap before Vitality lands would leave a knowingly mixed production authority path.

## Verification

- [ ] **T01-020 — Add participant/vitality regression tests.** **BLOCKED on `Game.Vitality.Api`.** Character-backed participant identity already has focused coverage in `CombatCharacterBindingTests`; remaining acceptance is authoritative Vitality behavior.
- [x] **T01-021 — Add resolution/idempotency tests.** `EncounterCombatIntegrationTests` proves `EncounterCombatCoordinator.TryTakeResolved` emits one terminal fact per session under repeated polling and that the real `EncounterRegistry.ApplyCombatResolved` accepts an identical repeat without another revision mutation.
- [x] **T01-022 — Add encounter mapping tests.** `EncounterCombatIntegrationTests` proves real `EncounterCombatRequest` membership maps through production CharacterIds into Combat participants and preserves the same `EncounterId` through start and terminal result; role-to-team and winner-to-EncounterResolution mappings remain explicit composition policy.
- [ ] **T01-023 — Add independent reuse fixture.** Partial proof now includes non-Kentridge `CombatCharacterBindingTests`, `CombatEncounterContractTests`, and a real-registry `EncounterCombatIntegrationTests` fixture. Final checkbox remains open until Vitality is part of the same reusable production seam rather than Combat-owned HP.
- [ ] **T01-024 — Run module-owned EditMode/PlayMode tests.** Current `Assets/Game/Combat` tree has no `*.module-validation.json` manifest or module-local Validation directory. After the full production migration exists, add/use the module-owned validation declaration required by repository standards and rely on automatic discovery; do not manually register individual tests.
- [ ] **T01-025 — Run assembled integration proof.** Confirm Kentridge uses the production path; full built-player acceptance remains owned by system 24.

## Cleanup and close

- [ ] **T01-030 — Search for bypasses.** Baseline audit complete: production `CombatRuntime.cs` contains no Unity/raw key polling; `KentridgeForestBanditEncounter` remains the confirmed production bypass, directly constructing `InputContextService`, `UnityPlayerInputReader`, `CombatService`, and `CombatInputController`; `Game.Composition.Kentridge.Playable.asmdef` directly references `Game.Combat.Runtime` and `Game.Input.Runtime`. `Assets/Game/Composition/CombatEnvironmentRuntime` is the older `MountingForce.CombatPrototype` experiment. Final checkbox remains open until the Kentridge bypass is removed.
- [ ] **T01-031 — Check blast radius.** Verification surface now includes `CombatAuthorityMigrationTests`, `CombatInputModuleBoundaryTests`, `CombatCharacterBindingTests`, `CombatEncounterContractTests`, `EncounterCombatIntegrationTests`, and `KentridgeCombatEncounterTests`; the older `Assets/Tests/CombatPrototype/PlayMode` suite remains blast-radius coverage. Final execution waits for Vitality/Kentridge migration.
- [ ] **T01-032 — Close only with one authority.** Confirm Vitality owns production life state, Encounters owns encounter lifecycle, Combat owns combat resolution, and no duplicate production path remains.

## Active blocker evidence

- Feature merged master `b274014ae201153c816c981a1092ad8b0d0a7539` (`close encounter lifecycle SceneIssue`) at merge commit `1f32ec912d0026afabed51e04ffdc4df8db504f7` before the Encounter contract/runtime integration work.
- Present on master/feature: production Input, Characters, and Encounters. `Game.Encounters.Api` owns `EncounterId`, membership, activation, queued `EncounterCombatRequest`, and `ApplyCombatResolved`; it depends on Characters but not Combat, preserving the intended dependency direction.
- Still absent from current master: production `Assets/Game/Vitality` / `Game.Vitality.Api`. Agent-3 must not invent a substitute life-state contract.
- Current concrete Kentridge seam remains `Assets/Game/Composition/Kentridge/Playable/KentridgeForestBanditEncounter.cs` plus `Game.Composition.Kentridge.Playable.asmdef`.
- Known Combat-internal preservation surface: `ChainRoundReadinessCoordinator`, `ChainEnemyTacticalAI`, `ChainReactionReservationCoordinator`, `ChainExecutionPlan`, and `ChainCombatBoard` in the older `MountingForce.CombatPrototype` namespace.
- `ci-test/fixes/agent-3` remains the only authorized targeted-CI transport. Never replace a queued/running request.
- Acceptance remains unchanged; the next blocked production authority tasks are T01-010/T01-011 pending real Vitality.
