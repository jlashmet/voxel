# 01 Production combat integration — tasks

**Plan:** [plan.md](plan.md)
**Ownership:** existing `Game.Combat.Api` / `Game.Combat.Runtime` plus thin composition adapters.
**Execution:** work top-to-bottom unless a dependency is explicitly blocked. Do not create a second combat runtime.

## Baseline and contracts

- [x] **T01-001 — Inventory current combat ownership.** `CombatService` owns authoritative `_hitPoints` (6 HP, 2 damage); `CombatParticipant` owns team; `KentridgeForestBanditEncounter` locally constructs Input/Combat services, creates participant identities, spawns bandits, starts combat, and settles encounter state. Evidence recorded in `plan.md`.
- [ ] **T01-002 — Lock the module boundary.** **BLOCKED on production composition/prerequisite APIs.** Current `Game.Composition.Kentridge.Playable.asmdef` directly references `Game.Combat.Runtime` and `Game.Input.Runtime`; removing those references before production factories/composition arrive would break the only concrete implementations rather than migrate ownership. Re-evaluate immediately after prerequisites land.
- [ ] **T01-003 — Define character-backed participant binding.** **BLOCKED:** production `CharacterId` / Characters API is absent on current master. Do not invent a shadow Character contract.
- [ ] **T01-004 — Define combat-start request/result.** **BLOCKED:** production `Game.Encounters.Api` contract is absent on current master; current Kentridge encounter is scene-local composition only.
- [ ] **T01-005 — Define combat-resolution fact.** **BLOCKED:** the Encounters consumer contract is absent, so the minimum result cannot yet be validated against its real consumer without guessing.

## Runtime migration

- [ ] **T01-010 — Replace prototype health authority.** **BLOCKED:** production `Game.Vitality.Api` is absent. Current Combat authority is documented in `plan.md`.
- [ ] **T01-011 — Route damage through Vitality.** **BLOCKED:** production `Game.Vitality.Api` is absent.
- [ ] **T01-012 — Preserve combat-only state.** Blocked behind T01-010/T01-011 migration; current round/readiness/tactical/chain state remains in Combat.
- [ ] **T01-013 — Integrate Encounter ownership.** **BLOCKED:** production `Game.Encounters.Api` is absent.
- [ ] **T01-014 — Migrate semantic input.** Current `CombatInputController` already reads `IPlayerInputReader`/`PlayerInputSnapshot` rather than raw keys, but production composition still constructs `Game.Input.Runtime` locally in Kentridge. Final migration is blocked on the production composition seam that replaces scene-local construction.
- [ ] **T01-015 — Remove scene-local combat construction.** **BLOCKED:** replacement production Characters/Vitality/Encounters composition is not yet available; deleting current construction would remove gameplay without a production owner.

## Verification

- [ ] **T01-020 — Add participant/vitality regression tests.** Blocked on `Game.Vitality.Api` and Characters contracts.
- [ ] **T01-021 — Add resolution/idempotency tests.** Blocked on the production resolution contract from T01-005.
- [ ] **T01-022 — Add encounter mapping tests.** Blocked on `Game.Encounters.Api`.
- [ ] **T01-023 — Add independent reuse fixture.** Blocked until the production API seams exist; fixture must consume those real seams, not local substitutes.
- [ ] **T01-024 — Run module-owned EditMode/PlayMode tests.** Rely on automatic module validation discovery; do not manually register individual tests.
- [ ] **T01-025 — Run assembled integration proof.** Confirm Kentridge uses the production path; full built-player acceptance remains owned by system 24.

## Cleanup and close

- [ ] **T01-030 — Search for bypasses.** Repository-wide search for combat-owned health, raw combat key polling, scene-local CombatService creation, and external `Game.Combat.Runtime` references; eliminate or document justified internal uses.
- [ ] **T01-031 — Check blast radius.** Verify headless combat, tactical AI, chain reactions, and existing combat tests retain behavior/performance.
- [ ] **T01-032 — Close only with one authority.** Confirm Vitality owns life state, Encounters owns encounter lifecycle, Combat owns combat resolution, and no duplicate production path remains.

## Active blocker evidence

- Baseline/master inspected at `71e5b6b146cb7dd3b7da0305d0ab42bcc9cea22e` before documentation commit.
- Present: `Assets/Game/Input/Api` and `Assets/Game/Input/Runtime`.
- Absent: production `Assets/Game/Characters`, `Assets/Game/Vitality`, and a production `Game.Encounters.Api` module on the current baseline.
- Current concrete Kentridge seam: `Assets/Game/Composition/Kentridge/Playable/KentridgeForestBanditEncounter.cs` plus `Game.Composition.Kentridge.Playable.asmdef`.
- Acceptance remains unchanged; continue by merging new `origin/master` prerequisite work when it becomes available, then resume at T01-002.
