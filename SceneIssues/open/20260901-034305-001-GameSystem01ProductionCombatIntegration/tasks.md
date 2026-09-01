# 01 Production combat integration — tasks

**Plan:** [plan.md](plan.md)
**Ownership:** existing `Game.Combat.Api` / `Game.Combat.Runtime` plus thin composition adapters.
**Execution:** work top-to-bottom unless a dependency is explicitly blocked. Do not create a second combat runtime.

## Baseline and contracts

- [ ] **T01-001 — Inventory current combat ownership.** Locate combat participant, health/alive, team, input, session, and Kentridge/bootstrap code; record every authoritative health store and scene-local combat service that must be migrated.
- [ ] **T01-002 — Lock the module boundary.** Verify external assemblies consume `Game.Combat.Api` only; identify and remove any new cross-module dependency on `Game.Combat.Runtime` before implementation proceeds.
- [ ] **T01-003 — Define character-backed participant binding.** Add/adjust API contracts that map `CharacterId` to combat participant/team semantics without exposing Character runtime objects.
- [ ] **T01-004 — Define combat-start request/result.** Make encounter-to-combat activation semantic, deterministic, and independent of scene triggers.
- [ ] **T01-005 — Define combat-resolution fact.** Expose the minimum result Encounters/Story need; explicitly exclude game-victory/final-boss semantics.

## Runtime migration

- [ ] **T01-010 — Replace prototype health authority.** Adapt combat participants to read/write vitality through `Game.Vitality.Api`; remove combat-owned authoritative health once parity is proven.
- [ ] **T01-011 — Route damage through Vitality.** Ensure accepted combat hits request damage from Vitality and Combat observes resulting alive/defeated truth instead of maintaining a second copy.
- [ ] **T01-012 — Preserve combat-only state.** Keep round/readiness/tactical/chain-combat state in Combat and verify none is accidentally moved into Characters or Vitality.
- [ ] **T01-013 — Integrate Encounter ownership.** Have `Game.Encounters.Api` request combat participation and consume `CombatResolved`; Combat must not decide encounter cleanup or campaign outcome.
- [ ] **T01-014 — Migrate semantic input.** Replace raw key/button knowledge and local input-context services with `Game.Input.Api` actions/context supplied through composition.
- [ ] **T01-015 — Remove scene-local combat construction.** Replace Kentridge/prototype `new CombatService` or equivalent alternate ownership with production composition; retain Kentridge policy only in composition.

## Verification

- [ ] **T01-020 — Add participant/vitality regression tests.** Prove a Character-backed participant takes damage, defeats exactly once through Vitality, and Combat observes the result.
- [ ] **T01-021 — Add resolution/idempotency tests.** Repeated or late combat-resolution processing must not emit duplicate semantic completion.
- [ ] **T01-022 — Add encounter mapping tests.** Prove encounter activation -> combat -> combat resolution -> encounter consumption without direct Runtime-to-Runtime references.
- [ ] **T01-023 — Add independent reuse fixture.** Compose Combat + Characters + Vitality + Encounters outside Kentridge and prove the same API path works.
- [ ] **T01-024 — Run module-owned EditMode/PlayMode tests.** Rely on automatic module validation discovery; do not manually register individual tests.
- [ ] **T01-025 — Run assembled integration proof.** Confirm Kentridge uses the production path; full built-player acceptance remains owned by system 24.

## Cleanup and close

- [ ] **T01-030 — Search for bypasses.** Repository-wide search for combat-owned health, raw combat key polling, scene-local CombatService creation, and external `Game.Combat.Runtime` references; eliminate or document justified internal uses.
- [ ] **T01-031 — Check blast radius.** Verify headless combat, tactical AI, chain reactions, and existing combat tests retain behavior/performance.
- [ ] **T01-032 — Close only with one authority.** Confirm Vitality owns life state, Encounters owns encounter lifecycle, Combat owns combat resolution, and no duplicate production path remains.
