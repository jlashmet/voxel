# 12 WorldBuilder encounter realization bridge — tasks

**Plan:** [plan.md](plan.md)
**Ownership:** `Game.Composition.EncounterRealization`; composition-only, no ceremonial Api/Runtime pair.
**Execution rule:** translate existing WorldBuilder realization facts into Encounter/Character semantic bindings. Never recompute generated placement in encounter code.

## Baseline / contract discovery

- [x] **T12-001 — Inventory encounter placement inputs.** Find Kentridge/other encounter scripts that read generated sites, NPCs, spawn positions, areas or recompute placement independently of WorldBuilder.
  - Evidence: baseline `KentridgeForestBanditEncounter` reconstructed its forest anchor from Kentridge/Hightown coordinates plus a `RegionThemeMap` scan, then added three local bandit spawn offsets and a hardcoded realization id. This was the demonstrated duplicate placement path in scope.
- [x] **T12-002 — Inventory reusable WorldBuilder queries.** Record which needed facts already exist in `Game.WorldBuilder.Api` and which are only exposed through Runtime/composition internals.
  - Evidence: API exposes semantic `SiteRef`, `NpcRef`, `ResolvedSiteId`, site-role bindings, NPC-to-realized-site assignments, and `TopDownWorldLayout` node positions. Physical backend anchoring remains composition policy.
- [x] **T12-003 — Define minimal realization record.** Specify stable site/object/NPC ids plus only positions/areas/spawn-capable facts actually required by `Encounters.Api`/`Characters.Api`.
  - Evidence: `EncounterRealizationSpec` binds `EncounterDefinition`, authored `SiteRef`, `ResolvedSiteId`, and character intents; realization carries the exact site anchor and EncounterParticipant/CharacterVector3 bindings. `EncounterSpawnPointRef` adds only semantic encounter-local spawn identity, never coordinates.
- [x] **T12-004 — Add WorldBuilder API gaps only with demonstrated reuse.** For each missing query, prove another consumer where practical or keep the adapter local rather than broadening the shared API speculatively.
  - Evidence: no WorldBuilder API was widened. `IEncounterRealizationFacts` is a composition boundary for exact post-generation positions, and Kentridge adapts its exact selected `TopDownWorldLayout` locally.
- [x] **T12-005 — Establish composition asmdef.** It may reference WorldBuilder.Api, Encounters.Api and Characters.Api only; no Runtime dependency across module boundaries.
  - Evidence: `Game.Composition.EncounterRealization.asmdef` references only those three API assemblies and is engine-free.

## Implementation

- [x] **T12-010 — Build pure realization adapter/composer.** Transform authored semantic intent + realized WorldBuilder output into encounter binding data deterministically.
  - Evidence: `EncounterRealizationComposer.Compose` is a pure API-only transform with copied immutable-facing output collections and deterministic failure paths.
- [x] **T12-011 — Bind authored sites.** Resolve stable site/area semantics from generated output without hardcoded coordinates in shared bridge code.
  - Evidence: authored `SiteRef` and generated `ResolvedSiteId` flow through the spec; exact anchor comes only from `IEncounterRealizationFacts`.
- [x] **T12-012 — Bind authored NPC/character intents.** Resolve campaign/world character identities to CharacterId creation/binding inputs without owning Characters lifecycle.
  - Evidence: `EncounterCharacterIntent` accepts an existing `CharacterId` plus optional `NpcRef` or semantic spawn-point ref; composer produces `EncounterParticipant` plus exact position and does not register/own Character lifecycle.
- [x] **T12-013 — Bind spawn-capable points/areas.** Reuse WorldBuilder-selected/generated positions and preserve deterministic selection semantics.
  - Evidence: semantic `EncounterSpawnPointRef` is resolved only through `IEncounterRealizationFacts.TryGetSpawnAnchor`. Kentridge's local adapter receives the exact macro layout instance selected for backend realization, derives the forest area's physical anchor from its realized grid position/root/cell size, and exposes three named encounter formation slots from that realized area.
- [x] **T12-014 — Move named policy outward.** Kentridge-specific sites/encounters/NPC choices remain under Kentridge/campaign composition; shared bridge contains no named place policy.
  - Evidence: shared module contains no Kentridge/place/encounter names. `KentridgeForestEncounterRealization` owns the forest site role and three authored bandit formation slots.
- [x] **T12-015 — Remove duplicate placement calculations.** Encounter runtime/scene scripts consume the adapter output instead of running parallel terrain/site placement logic.
  - Evidence: `KentridgeForestBanditEncounter` no longer scans `RegionThemeMap`, computes a forest-entry inset, stores the old hardcoded realization id, or adds local x/z spawn offsets. `Start()` composes bridge output after WorldBuilder layout selection and `SpawnBandits()` consumes `EncounterCharacterBinding.Position` directly.
- [x] **T12-016 — Define failure behavior.** Missing/ambiguous realization returns semantic composition/startup failure; do not silently substitute primitives/coordinates.
  - Evidence: `MissingSiteRealization`, `MissingCharacterRealization`, `MissingSpawnRealization`, and `DuplicateCharacter` return deterministic semantic diagnostics; Kentridge fails startup if its required forest node was not realized.

## Discovered required work

- [x] **T12-017 — Resolve WorldBuilder campaign factory symbol collision.** Exact-SHA revalidation must compile the Kentridge bridge by explicitly binding the existing `Game.WorldBuilder.Api.Campaign` factory instead of allowing the enclosing `Game.Composition.Campaign` namespace to win name resolution.
  - Evidence: exact-SHA workflow `33815602358` failed at `KentridgeForestEncounterRealization.cs:89` with CS0234 before tests/player build; `CampaignBlueprint.cs` confirms the intended public factory is `Game.WorldBuilder.Api.Campaign.Create`. `KentridgeForestEncounterRealization` now aliases that factory as `WorldBuilderCampaign`, keeping the fix local and API-neutral.
- [ ] **T12-018 — Reconcile feature branch with authoritative current master.** Rebuild the System12 feature tree on current `origin/master` without carrying stale/reverted unrelated System09 or workflow content, then exact-SHA revalidate the reconciled source before closure.
  - Evidence: after workflow `33821720482` passed source `3b9e2b80a7e1ee15d8270cfef206a292fa220546`, `origin/master` advanced to `04c43482768548f96db6f18234f1709a25b0d983`. Compare shows the prior history-only merge preserved the pre-master feature tree and would revert unrelated master content if promoted; this must be corrected before the final PR.

## Verification

- [ ] **T12-020 — Two independent authored fixtures.** Resolve two different encounter/site definitions through the same bridge.
  - Regression added (`alpha` / `beta` authored fixtures); pending CI execution.
- [ ] **T12-021 — Placement-reuse regression.** Assert encounter binding uses the exact generated semantic placement rather than a separately calculated approximation.
  - Regressions supply deliberately different exact site/NPC/spawn vectors and a second Kentridge macro layout; pending CI execution.
- [ ] **T12-022 — Missing-realization test.** Absent required generated fact fails deterministically with useful semantic diagnostics.
  - Missing site, missing spawn, and missing Kentridge forest-node regressions added; pending CI execution.
- [ ] **T12-023 — Dependency-boundary test/review.** No bridge reference to `WorldBuilder.Runtime`, `Encounters.Runtime`, or `Characters.Runtime`.
  - Assembly regression added and asmdef reviewed; pending CI execution.
- [ ] **T12-024 — Module-local visual/player validation only if needed.** If realization is visually material, add optional standalone validation using the shared harness; do not invent a feature-specific harness.
  - The Kentridge encounter's physical placement is player-visible, so use existing automatic/module and SceneIssue player validation rather than adding a bespoke harness.
- [ ] **T12-025 — Kentridge assembled proof.** Verify its encounter realizes through the bridge; broader route remains system 24 acceptance.
  - `KentridgeForestEncounterRealizationTests` added to prove exact macro-layout reuse and failure behavior; assembled player proof pending CI.
  - Validation attempt: exact source `7601f3b7328a79e56d70db9807df07ee4fd4c137`, transport `d6a4093c5c56e634df0b6841691bf1fc15da918a`, workflow `33813168179` completed failure before test execution because Unity reported CS0118 at `KentridgeForestBanditEncounter.cs:53` (`EncounterRealization` namespace/type collision). The assignment-scoped compile cause was isolated and fixed with a Kentridge-local forwarding type bridge; revalidation required on the new source SHA.

## Cleanup / close

- [x] **T12-030 — Repository-wide placement-duplication search.** Remove encounter-specific terrain/site/NPC placement calculations superseded by WorldBuilder output.
  - Evidence: feature audit confirms the migrated encounter contains no `RegionThemeMap.ForKentridgeHightown`, `ForestEntryInsetDm`, old `kentridge-pine-forest-ambush` realization id, or the former `5.4m` spawn-offset calculation. Ground-contact raycasts remain presentation/terrain contact, not site selection.
- [x] **T12-031 — Scope audit.** No world generation, encounter lifecycle, named Kentridge policy or second API/Runtime introduced.
  - Evidence: diff adds one composition-only shared bridge, module-local tests, one Kentridge-local adapter, one assembly reference, and the exact macro-layout handoff/migration. No new world generator, encounter lifecycle, or public Runtime dependency was introduced.
- [ ] **T12-032 — Close with reuse proof.** Two authored consumers share the adapter and both use WorldBuilder as the single placement owner.
