# 01 Production combat integration — tasks

**Plan:** [plan.md](plan.md)
**Ownership:** existing `Game.Combat.Api` / `Game.Combat.Runtime` plus thin composition adapters.
**Execution:** work top-to-bottom unless a dependency is explicitly blocked. Do not create a second combat runtime.

## Baseline and contracts

- [ ] **T01-001 — Inventory current combat ownership.** Locate combat participant, health/alive, team, input, session, squad/beat/chain state and Kentridge/bootstrap code; record every authoritative store and scene-local combat service that must be migrated.
- [ ] **T01-002 — Lock the module boundary.** Verify external assemblies consume `Game.Combat.Api` only; identify and remove any new cross-module dependency on `Game.Combat.Runtime` before implementation proceeds.
- [ ] **T01-003 — Define character-backed participant binding.** Add/adjust API contracts that map `CharacterId` to combat participant/team/squad semantics without exposing Character runtime objects.
- [ ] **T01-004 — Define combat-start request/result.** Make encounter-to-combat activation semantic, deterministic, and independent of scene triggers.
- [ ] **T01-005 — Define combat-resolution fact.** Expose the minimum result Encounters/Story need; explicitly exclude game-victory/final-boss semantics.
- [ ] **T01-006 — Define squad/beat contract.** Represent player squad membership, beat identity/phase, and exactly one system-selected active member per participating player without requiring a turn for every character.
- [ ] **T01-007 — Define active/upcoming sequence and action submission.** Expose authoritative current/upcoming active-member sequence plus semantic action choices/submission for the selected member; player chooses the action, not the acting member.
- [ ] **T01-008 — Define event-driven combo opportunities.** Model transient semantic opportunities from movement/launch/fall, projectiles, impacts, guarding, displacement, collisions, spell casting, ally actions and world alteration; do not require a giant status-trigger matrix.
- [ ] **T01-009 — Define combo behavior and bounds.** Support configured interactions that join, redirect or transform/escalate an in-flight event, with deterministic ordering, eligibility and explicit chain/work limits.

## Runtime migration

- [ ] **T01-010 — Replace prototype health authority.** Adapt combat participants to read/write vitality through `Game.Vitality.Api`; remove combat-owned authoritative health once parity is proven.
- [ ] **T01-011 — Route damage through Vitality.** Ensure accepted combat hits request damage from Vitality and Combat observes resulting alive/defeated truth instead of maintaining a second copy.
- [ ] **T01-012 — Preserve combat-only state.** Keep beat/squad/readiness/tactical/combo-chain state in Combat and verify none is accidentally moved into Characters or Vitality.
- [ ] **T01-013 — Integrate Encounter ownership.** Have `Game.Encounters.Api` request combat participation and consume `CombatResolved`; Combat must not decide encounter cleanup or campaign outcome.
- [ ] **T01-014 — Migrate semantic input.** Replace raw key/button knowledge and local input-context services with `Game.Input.Api` actions/context supplied through composition.
- [ ] **T01-015 — Remove scene-local combat construction.** Replace Kentridge/prototype `new CombatService` or equivalent alternate ownership with production composition; retain Kentridge policy only in composition.
- [ ] **T01-016 — Implement authoritative beat coordination.** Deterministically select one active member per player squad, accept at most one deliberate move from each player for the beat, and resolve accepted player moves in the same authoritative beat rather than serial player/character turns.
- [ ] **T01-017 — Keep non-active members participating without extra turns.** Non-active members continue autonomous/basic behavior and may enter configured combo interactions without creating additional player decision turns.
- [ ] **T01-018 — Resolve event-driven chains.** Allow eligible characters/equipment to join, redirect, or transform/escalate in-flight actions/events; statuses may contribute but cannot be the only combo grammar.
- [ ] **T01-019 — Support spatial/cross-player/world interactions.** Combo resolution can use ally actions, trajectories, collisions, positioning and authoritative destructible-world events while preserving owning-system authority.

## Verification

- [ ] **T01-020 — Add participant/vitality regression tests.** Prove a Character-backed participant takes damage, defeats exactly once through Vitality, and Combat observes the result.
- [ ] **T01-021 — Add resolution/idempotency tests.** Repeated or late combat-resolution processing must not emit duplicate semantic completion.
- [ ] **T01-022 — Add encounter mapping tests.** Prove encounter activation -> combat -> combat resolution -> encounter consumption without direct Runtime-to-Runtime references.
- [ ] **T01-023 — Add independent reuse fixture.** Compose Combat + Characters + Vitality + Encounters outside Kentridge and prove the same API path works.
- [ ] **T01-024 — Run module-owned EditMode/PlayMode tests.** Rely on automatic module validation discovery; do not manually register individual tests.
- [ ] **T01-025 — Run assembled integration proof.** Confirm Kentridge uses the production path; full built-player acceptance remains owned by system 24.
- [ ] **T01-026 — Add deterministic beat-sequence regression.** Same squad/config/state produces the same active/upcoming sequence and valid-action eligibility; squad size does not create serial actor turns.
- [ ] **T01-027 — Add simultaneous multiplayer beat regression.** Two or more players submit one move each and authority resolves them as one beat with deterministic conflict/event ordering independent of client arrival order where commands are otherwise equivalent.
- [ ] **T01-028 — Add event-combo regression.** Prove a chain can join and redirect or transform an in-flight movement/projectile/impact event across multiple characters without depending on a status-proc trigger.
- [ ] **T01-029 — Add chain-bound regression.** Cyclic or pathological reaction builds terminate deterministically at the configured work/depth bound without duplicate authoritative effects.

## Cleanup and close

- [ ] **T01-030 — Search for bypasses.** Repository-wide search for combat-owned health, raw combat key polling, scene-local CombatService creation, external `Game.Combat.Runtime` references, alternate turn queues and duplicate reaction authority; eliminate or document justified internal uses.
- [ ] **T01-031 — Check blast radius.** Verify headless combat, tactical AI, event-driven chains and existing combat tests retain behavior/performance at representative 20–30-character party scale.
- [ ] **T01-032 — Close only with one authority.** Confirm Vitality owns life state, Encounters owns encounter lifecycle, Combat owns beat/chain/combat resolution, and no duplicate production path remains.
- [ ] **T01-033 — Prove bounded player decision count.** Representative large-party combat requires one deliberate acting-member move per player per beat, not one player turn per character; combo depth comes from build/event interactions rather than input count.
