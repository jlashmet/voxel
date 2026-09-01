# 05 Encounter activation, membership & lifecycle — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.Encounters.Api` / `Game.Encounters.Runtime`
**Execution rule:** encounters own temporary situation lifecycle; they do not own world generation, character life, combat rules, cutscenes, or game outcome.

## API / state model

- [ ] **T05-001 — Inventory existing encounter-like flows.** Find scene triggers, combat bootstraps, authored encounter definitions, temporary spawns, cleanup scripts, and cutscene/combat conflation.
- [ ] **T05-002 — Establish asmdefs and API-only dependencies.** Runtime may depend on Characters.Api and later Combat.Api; API contains no Unity trigger/scene references.
- [ ] **T05-003 — Define `EncounterId` and definition/config.** Stable semantic identity plus only reusable activation/membership/policy configuration.
- [ ] **T05-004 — Define lifecycle states/transitions.** Specify inactive/active/resolving/resolved/cleaned semantics and legal transitions; avoid scene-specific phases.
- [ ] **T05-005 — Define membership snapshot and commands.** Join/leave keyed by CharacterId with explicit persistent/encounter-owned participant semantics.
- [ ] **T05-006 — Define activation and resolution contracts.** Activation is semantic input; resolution includes reason/result needed by Story/Progression but no `GameOutcome` meaning.
- [ ] **T05-007 — Define lifecycle events/snapshot.** Expose deterministic current truth for replication/persistence and transition events for downstream composition.

## Runtime

- [ ] **T05-010 — Implement encounter registry/lifecycle.** Enforce unique EncounterIds, legal transitions, and idempotent activation/resolution processing.
- [ ] **T05-011 — Implement stable membership.** Handle existing Characters, duplicate joins/leaves, removed/defeated characters, and deterministic membership ordering.
- [ ] **T05-012 — Implement explicit participant ownership.** Persistent characters survive encounter cleanup; encounter-created temporary characters follow configured cleanup semantics.
- [ ] **T05-013 — Separate activation from realization.** Runtime accepts semantic realization/binding data from system 12; it never recomputes WorldBuilder placement.
- [ ] **T05-014 — Integrate Combat through API.** Encounter decides whether/when a combat session is requested and consumes `CombatResolved`; no Combat Runtime dependency.
- [ ] **T05-015 — Support non-combat encounters.** Prove an encounter can resolve without starting Combat.
- [ ] **T05-016 — Emit semantic resolution/cleanup facts.** Story/Progression/composition observe facts instead of Runtime callbacks into their internals.
- [ ] **T05-017 — Add snapshot capture/restore and replication projection seams.** Preserve active membership/lifecycle without replaying historical activation events.

## Verification

- [ ] **T05-020 — Lifecycle transition tests.** Cover duplicate activation, invalid transitions, repeated resolution, cleanup and post-resolution requests.
- [ ] **T05-021 — Membership tests.** Persistent and temporary participants, join/leave races, missing CharacterIds and cleanup ownership.
- [ ] **T05-022 — Combat encounter integration fixture.** Semantic activation -> membership -> Combat API -> resolution -> cleanup.
- [ ] **T05-023 — Non-combat encounter fixture.** Same module resolves an authored non-combat situation.
- [ ] **T05-024 — Restore test.** Restore an active encounter with correct membership/current state and no one-shot replay.
- [ ] **T05-025 — Independent authored fixture and automatic module validation.** Use at least one non-Kentridge encounter.

## Cleanup / close

- [ ] **T05-030 — Remove scene-trigger authority and duplicate encounter state.** Scene triggers may report semantic activation only.
- [ ] **T05-031 — Dependency audit.** No WorldBuilder/Combat/Story/Progression Runtime dependency and no game-outcome/final-boss semantics.
- [ ] **T05-032 — Close with ownership proof.** Encounter lifecycle/membership has one authoritative owner and both combat/non-combat reuse are proven.
