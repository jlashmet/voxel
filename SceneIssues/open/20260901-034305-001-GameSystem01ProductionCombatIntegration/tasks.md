# 01 Production combat integration — tasks

**Plan:** [plan.md](plan.md)
**Ownership:** existing `Game.Combat.Api` / `Game.Combat.Runtime` plus thin composition adapters.
**Execution:** work top-to-bottom unless a dependency is explicitly blocked. Do not create a second combat runtime.

## Baseline and contracts

- [x] **T01-001 — Inventory current combat ownership.** `CombatService` owns authoritative `_hitPoints` (6 HP, 2 damage); `CombatParticipant` owns team; `KentridgeForestBanditEncounter` locally constructs Input/Combat services, creates participant identities, spawns bandits, starts combat, and settles encounter state. Evidence recorded in `plan.md`.
- [ ] **T01-002 — Lock the module boundary.** **BLOCKED on production composition/prerequisite APIs.** Current `Game.Composition.Kentridge.Playable.asmdef` directly references `Game.Combat.Runtime` and `Game.Input.Runtime`; removing those references before production factories/composition arrive would break the only concrete implementations rather than migrate ownership. Boundary audit also checked `Assets/Game/Composition/CombatEnvironmentRuntime`: that path is the older `MountingForce.CombatPrototype`/environment experiment and is not a `Game.Combat.Runtime` production consumer. Existing `CombatAuthorityMigrationTests` already asserts that the authoritative Combat assembly remains engine/device independent; `CombatInputModuleBoundaryTests` proves semantic `IPlayerInputReader` input. Re-evaluate immediately after prerequisites land and remove the Kentridge production Runtime coupling through the real composition seam.
- [ ] **T01-003 — Define character-backed participant binding.** **BLOCKED:** production `CharacterId` / Characters API is absent on current master. Do not invent a shadow Character contract.
- [ ] **T01-004 — Define combat-start request/result.** **BLOCKED:** production `Game.Encounters.Api` contract is absent on current master; current Kentridge encounter is scene-local composition only.
- [ ] **T01-005 — Define combat-resolution fact.** **BLOCKED:** the Encounters consumer contract is absent, so the minimum result cannot yet be validated against its real consumer without guessing.

## Runtime migration

- [ ] **T01-010 — Replace prototype health authority.** **BLOCKED:** production `Game.Vitality.Api` is absent. Current production Combat authority is documented in `plan.md`.
- [ ] **T01-011 — Route damage through Vitality.** **BLOCKED:** production `Game.Vitality.Api` is absent.
- [ ] **T01-012 — Preserve combat-only state.** Blocked behind T01-010/T01-011 migration, but preservation inventory is complete: round/readiness (`ChainRoundReadinessCoordinator`), committed tactical intents/enemy phase (`ChainEnemyTacticalAI`), reaction ownership (`ChainReactionReservationCoordinator`), planning/history (`ChainExecutionPlan`), and board/motion state remain Combat concerns. `CombatAuthorityMigrationTests` already exercises cascade/reaction ownership, deterministic plan replay, deterministic enemy planning, and assembly isolation. These `MountingForce.CombatPrototype` internals are blast-radius fixtures, not targets for opportunistic life-state migration; final checkbox waits for the production Vitality migration to prove they were not moved/regressed.
- [ ] **T01-013 — Integrate Encounter ownership.** **BLOCKED:** production `Game.Encounters.Api` is absent.
- [ ] **T01-014 — Migrate semantic input.** Current `CombatInputController` already reads `IPlayerInputReader`/`PlayerInputSnapshot` rather than raw keys, and `CombatInputModuleBoundaryTests` drives it with a synthetic semantic reader. Production composition still constructs `Game.Input.Runtime` locally in Kentridge, so final migration is blocked on the production composition seam that replaces scene-local construction.
- [ ] **T01-015 — Remove scene-local combat construction.** **BLOCKED:** replacement production Characters/Vitality/Encounters composition is not yet available; deleting current construction would remove gameplay without a production owner.

## Verification

- [ ] **T01-020 — Add participant/vitality regression tests.** Blocked on `Game.Vitality.Api` and Characters contracts. Existing tests cover Combat-owned HP only; they do not prove Character/Vitality ownership.
- [ ] **T01-021 — Add resolution/idempotency tests.** Blocked on the production resolution contract from T01-005. `KentridgeCombatEncounterTests` currently proves a settled proximity battle does not restart while the player remains nearby, but it does not prove semantic `CombatResolved` consumption/idempotency through Encounters.
- [ ] **T01-022 — Add encounter mapping tests.** Blocked on `Game.Encounters.Api`; current `KentridgeCombatEncounterTests` verifies the scene-local encounter path only.
- [ ] **T01-023 — Add independent reuse fixture.** Blocked until the production API seams exist; fixture must consume those real seams, not local substitutes.
- [ ] **T01-024 — Run module-owned EditMode/PlayMode tests.** Current `Assets/Game/Combat` tree has no `*.module-validation.json` manifest or module-local Validation directory; existing coverage is top-level `Assets/Tests` PlayMode. After the production diff exists, add/use the module-owned validation declaration required by current repository standards and rely on automatic discovery; do not manually register individual tests.
- [ ] **T01-025 — Run assembled integration proof.** Confirm Kentridge uses the production path; full built-player acceptance remains owned by system 24.

## Cleanup and close

- [ ] **T01-030 — Search for bypasses.** Baseline audit complete at feature head based on master `71e5b6b146cb7dd3b7da0305d0ab42bcc9cea22e`: production `CombatRuntime.cs` depends on `Game.Input.Api` and contains no Unity/raw key polling; `KentridgeForestBanditEncounter` is the confirmed production bypass, directly constructing `InputContextService`, `UnityPlayerInputReader`, `CombatService`, and `CombatInputController`; `Game.Composition.Kentridge.Playable.asmdef` directly references `Game.Combat.Runtime` and `Game.Input.Runtime`. `Assets/Game/Composition/CombatEnvironmentRuntime` is the older `MountingForce.CombatPrototype` experiment, not production `Game.Combat.Runtime` authority. Final checkbox remains open because the confirmed Kentridge bypasses cannot be eliminated until the production prerequisite composition seam lands.
- [ ] **T01-031 — Check blast radius.** Baseline verification map is concrete but final execution remains blocked on the actual migration. Existing production-facing PlayMode coverage includes: `CombatAuthorityMigrationTests` (assembly isolation, reaction ownership, deterministic chain/tactical state), `CombatInputModuleBoundaryTests` (semantic device-neutral input), and `KentridgeCombatEncounterTests` (deterministic headless battle, exact-scene forward progress, terminal cleanup/input-context release, no immediate restart). The older `Assets/Tests/CombatPrototype/PlayMode` suite additionally covers activation, enemy AI, execution plans, impact/mechanics, and demos. After Vitality/Encounter migration, run these plus the new T01-020/021/022 regressions and compare behavior/performance before checking this task.
- [ ] **T01-032 — Close only with one authority.** Confirm Vitality owns production life state, Encounters owns encounter lifecycle, Combat owns combat resolution, and no duplicate production path remains.

## Active blocker evidence

- Baseline/master re-checked at `71e5b6b146cb7dd3b7da0305d0ab42bcc9cea22e`; `fixes/agent-3` remains assignment-doc-only ahead of that baseline before this blocker correction.
- Present: `Assets/Game/Input/Api` and `Assets/Game/Input/Runtime`.
- Prerequisite SceneIssues are present on master: `20260901-034305-002-GameSystem02ActorVitalityDamageDefeat`, `20260901-034305-003-GameSystem03GameplayCharacterRuntime`, and `20260901-034305-005-GameSystem05EncounterActivationMembershipLifecycle`.
- Still absent from master: production `Assets/Game/Characters`, `Assets/Game/Vitality`, and a production `Game.Encounters.Api` implementation required by this integration.
- Current concrete Kentridge seam: `Assets/Game/Composition/Kentridge/Playable/KentridgeForestBanditEncounter.cs` plus `Game.Composition.Kentridge.Playable.asmdef`.
- Known Combat-internal preservation surface: `ChainRoundReadinessCoordinator`, `ChainEnemyTacticalAI`, `ChainReactionReservationCoordinator`, `ChainExecutionPlan`, and `ChainCombatBoard` in the older `MountingForce.CombatPrototype` namespace.
- Existing production-facing verification surface: `Assets/Tests/PlayMode/CombatAuthorityMigrationTests.cs`, `CombatInputModuleBoundaryTests.cs`, and `KentridgeCombatEncounterTests.cs`.
- Current Combat module has no module-validation manifest; create the validation declaration with the real production migration rather than a docs-only placeholder.
- `ci-test/fixes/agent-3` is idle; its latest run (`33445053759`, SHA `072f9d2ae473f18049adae1c757656e05dd457c7`) completed successfully for older work and is not evidence for the current feature head. Do not replace it until there is an exact production/test SHA worth validating.
- Acceptance remains unchanged; continue by merging new `origin/master` prerequisite work when it becomes available, then resume at T01-002.
