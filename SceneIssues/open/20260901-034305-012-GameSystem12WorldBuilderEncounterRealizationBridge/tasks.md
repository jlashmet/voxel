# 12 WorldBuilder encounter realization bridge — tasks

**Plan:** [plan.md](plan.md)
**Ownership:** `Game.Composition.EncounterRealization`; composition-only, no ceremonial Api/Runtime pair.
**Execution rule:** translate existing WorldBuilder realization facts into Encounter/Character semantic bindings. Never recompute generated placement in encounter code.

## Baseline / contract discovery

- [ ] **T12-001 — Inventory encounter placement inputs.** Find Kentridge/other encounter scripts that read generated sites, NPCs, spawn positions, areas or recompute placement independently of WorldBuilder.
- [ ] **T12-002 — Inventory reusable WorldBuilder queries.** Record which needed facts already exist in `Game.WorldBuilder.Api` and which are only exposed through Runtime/composition internals.
- [ ] **T12-003 — Define minimal realization record.** Specify stable site/object/NPC ids plus only positions/areas/spawn-capable facts actually required by `Encounters.Api`/`Characters.Api`.
- [ ] **T12-004 — Add WorldBuilder API gaps only with demonstrated reuse.** For each missing query, prove another consumer where practical or keep the adapter local rather than broadening the shared API speculatively.
- [ ] **T12-005 — Establish composition asmdef.** It may reference WorldBuilder.Api, Encounters.Api and Characters.Api only; no Runtime dependency across module boundaries.

## Implementation

- [ ] **T12-010 — Build pure realization adapter/composer.** Transform authored semantic intent + realized WorldBuilder output into encounter binding data deterministically.
- [ ] **T12-011 — Bind authored sites.** Resolve stable site/area semantics from generated output without hardcoded coordinates in shared bridge code.
- [ ] **T12-012 — Bind authored NPC/character intents.** Resolve campaign/world character identities to CharacterId creation/binding inputs without owning Characters lifecycle.
- [ ] **T12-013 — Bind spawn-capable points/areas.** Reuse WorldBuilder-selected/generated positions and preserve deterministic selection semantics.
- [ ] **T12-014 — Move named policy outward.** Kentridge-specific sites/encounters/NPC choices remain under Kentridge/campaign composition; shared bridge contains no named place policy.
- [ ] **T12-015 — Remove duplicate placement calculations.** Encounter runtime/scene scripts consume the adapter output instead of running parallel terrain/site placement logic.
- [ ] **T12-016 — Define failure behavior.** Missing/ambiguous realization returns semantic composition/startup failure; do not silently substitute primitives/coordinates.

## Verification

- [ ] **T12-020 — Two independent authored fixtures.** Resolve two different encounter/site definitions through the same bridge.
- [ ] **T12-021 — Placement-reuse regression.** Assert encounter binding uses the exact generated semantic placement rather than a separately calculated approximation.
- [ ] **T12-022 — Missing-realization test.** Absent required generated fact fails deterministically with useful semantic diagnostics.
- [ ] **T12-023 — Dependency-boundary test/review.** No bridge reference to `WorldBuilder.Runtime`, `Encounters.Runtime`, or `Characters.Runtime`.
- [ ] **T12-024 — Module-local visual/player validation only if needed.** If realization is visually material, add optional standalone validation using the shared harness; do not invent a feature-specific harness.
- [ ] **T12-025 — Kentridge assembled proof.** Verify its encounter realizes through the bridge; broader route remains system 24 acceptance.

## Cleanup / close

- [ ] **T12-030 — Repository-wide placement-duplication search.** Remove encounter-specific terrain/site/NPC placement calculations superseded by WorldBuilder output.
- [ ] **T12-031 — Scope audit.** No world generation, encounter lifecycle, named Kentridge policy or second API/Runtime introduced.
- [ ] **T12-032 — Close with reuse proof.** Two authored consumers share the adapter and both use WorldBuilder as the single placement owner.
