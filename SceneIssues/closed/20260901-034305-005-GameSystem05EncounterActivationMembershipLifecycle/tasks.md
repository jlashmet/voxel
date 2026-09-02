# 05 Encounter activation, membership & lifecycle — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.Encounters.Api` / `Game.Encounters.Runtime`
**Execution rule:** encounters own temporary situation lifecycle; they do not own world generation, character life, combat rules, cutscenes, or game outcome.

## API / state model

- [x] **T05-001 — Inventory existing encounter-like flows.** Kentridge forest bandits previously combined proximity activation, private resolution state, temporary membership, Combat bootstrap and settlement; no reusable Encounter registry existed.
- [x] **T05-002 — Establish asmdefs and API-only dependencies.** `Game.Encounters.Api` references only Characters.Api; Runtime references Encounters.Api + Characters.Api and is engine-neutral. No Unity/Combat/WorldBuilder Runtime dependency is present.
- [x] **T05-003 — Define `EncounterId` and definition/config.** Stable ordinal semantic identity plus semantic kind and reusable Combat policy only.
- [x] **T05-004 — Define lifecycle states/transitions.** Inactive/Active/Resolving/Resolved/Cleaned are explicit; Runtime rejects illegal/conflicting transitions without scene-specific phases.
- [x] **T05-005 — Define membership snapshot and commands.** Join/leave use stable CharacterId, deterministic ordering and explicit Persistent/EncounterOwned ownership.
- [x] **T05-006 — Define activation and resolution contracts.** Activation carries semantic cause/realization id; resolution carries reusable Completed/Failed/Abandoned plus reason without game-outcome meaning.
- [x] **T05-007 — Define lifecycle events/snapshot.** Deterministic snapshots expose current truth/revision; transition events and semantic facts are available to downstream composition/replication.

## Runtime

- [x] **T05-010 — Implement encounter registry/lifecycle.** Unique ids, legal transitions, idempotent duplicate activation/same resolution/cleanup and explicit conflicting failures are enforced.
- [x] **T05-011 — Implement stable membership.** Existing Characters are validated, unknown/defeated joins fail normally, duplicate joins/leaves are idempotent where equivalent, and membership is CharacterId-sorted.
- [x] **T05-012 — Implement explicit participant ownership.** Cleanup emits removal facts only for EncounterOwned characters; persistent characters survive. Kentridge composition consumes these facts and retains Character removal authority.
- [x] **T05-013 — Separate activation from realization.** Runtime stores semantic realization/binding data supplied by composition and contains no WorldBuilder placement logic.
- [x] **T05-014 — Integrate Combat through API.** Combat-required activation emits a semantic `EncounterCombatRequest`; composition maps it to Combat, then returns semantic resolution. Encounters has no Combat Runtime dependency.
- [x] **T05-015 — Support non-combat encounters.** Independent Hightown market dispute resolves through the same registry with no Combat request.
- [x] **T05-016 — Emit semantic resolution/cleanup facts.** Resolution, activation and cleanup are observable as semantic facts without callbacks into Story/Progression internals.
- [x] **T05-017 — Add snapshot capture/restore and replication projection seams.** Capture/restore preserves current lifecycle/membership/realization/revision and deliberately does not replay activation facts or queued Combat requests.

## Verification

- [x] **T05-020 — Lifecycle transition tests.** Covers duplicate activation, invalid transitions, repeated/conflicting resolution, cleanup and post-resolution activation rejection.
- [x] **T05-021 — Membership tests.** Covers persistent/temporary ownership, deterministic ordering, duplicate joins/leaves, missing/defeated CharacterIds and cleanup ownership.
- [x] **T05-022 — Combat encounter integration fixture.** Semantic activation -> stable membership -> semantic Combat request -> Combat resolution -> cleanup is covered headlessly and production Kentridge follows the same seam.
- [x] **T05-023 — Non-combat encounter fixture.** Same module resolves an authored Hightown social dispute without Combat.
- [x] **T05-024 — Restore test.** Restores active membership/current state while proving no activation fact or Combat request replay.
- [x] **T05-025 — Independent authored fixture and automatic module validation.** Independent Hightown fixture is implemented; exact-SHA request `410a0f13afc38672b59a2df0fc7ecbe7e925836f` validated feature source `adc80ccb7d0d28f2c230f49beec6de17ee528e8a` in run `33490738386`: focused EditMode tests, automatic module validation, and standalone SceneIssue replay all passed.

## Cleanup / close

- [x] **T05-030 — Remove scene-trigger authority and duplicate encounter state.** Kentridge proximity reports semantic activation only; private `_encounterResolved` state is removed and lifecycle reads derive from the registry.
- [x] **T05-031 — Dependency audit.** Encounters has no WorldBuilder/Combat/Story/Progression Runtime dependency and no game-outcome/final-boss semantics; Kentridge-only realization policy remains composition.
- [x] **T05-032 — Close with ownership proof.** EncounterRegistry is the single lifecycle/membership owner; production combat and independent non-combat consumers share the same contracts/runtime.
