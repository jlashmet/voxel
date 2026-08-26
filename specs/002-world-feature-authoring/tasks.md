# Tasks: World Feature Authoring

**Feature Directory**: `002-world-feature-authoring`
**Plan**: [plan.md](./plan.md)
**Created**: 2026-08-07

## Task Format

`- [ ] [TaskID] [P?] [Story?] Description with file path`

- `[P]` — parallelizable: different files, no dependency on incomplete work
- `[US#]` — user story label, required in user story phases only

## User Story Derivation

`spec.md` records one primary story plus ten acceptance scenarios rather than prioritised
P1/P2/P3 stories, so they are mapped here. Priority follows dependency order and risk retired,
not user value alone.

| Story | Priority | Source | Milestone |
|---|---|---|---|
| **US1** — A definition becomes voxels where you put it | P1 | Scenarios 6, 10 · SC-014 | M1 |
| **US2** — Features appear worldwide, identically, seam-free | P2 | Scenarios 1, 2, 4 · SC-001/003/005 | M2 |
| **US3** — Structures sit on the ground | P3 | Scenario 3 · SC-007 | M3 |
| **US4** — Castles: composition and validation | P4 | Scenarios 2, 6 · SC-010 | M4 |
| **US5** — Caves and water | P5 | Scenario 5 · SC-008 | M5 |
| **US6** — Identity, ownership, protection | P6 | Scenarios 8, 9 · SC-011/012/013 | M6 |
| **US7** — Visible from a distance | P7 | SC-006 | M7 |
| **US8** — Authoring is a designer's job | P8 | Scenario 10 · SC-002 | M8 |

**US1 is the real MVP and it is worth protecting.** It delivers the whole authoring→voxel path
for a single explicitly placed feature, which is enough to answer the question the plan's risk 2
raises: is parametric-only authoring good enough to build seven more milestones on? Do not skip
ahead to placement before someone has looked at a generated house and said yes.

**Tests are included** because the constitution requires continuous cross-hardware parity rather
than milestone checks, and because the plan's exit criteria are themselves stated as tests
(sub-volume equality, order independence). The order-independence harness in Phase 2 is not
optional scaffolding — it is the single check that catches most of what this design can get wrong.

**All numeric targets** come from [device-matrix.md](../001-destructible-voxel-engine/device-matrix.md),
which is authoritative. T009 puts this feature's numbers there before any of them are used.

---

## Build status (2026-08-07)

**T001–T039 are compiled and tested.** 66 EditMode tests and 32 PlayMode tests pass, including
every test written for this feature. Verified through `tools/unity-run.sh`; peak 3.5 GB, 13 s.

The load-bearing results:

- `RasterisingInPiecesEqualsRasterisingWhole` — a cottage rasterised whole is voxel-identical to
  the same cottage rasterised as eight disjoint octants. This is FR-008, and it is what lets a
  castle spanning four regions be generated a region at a time.
- `RegionGenerationIsIndependentOfOrder` — a 3×3 region block generated in 16 shuffled orders
  produces byte-identical worlds.
- `TerrainDoesNotRepeatBetweenRegions` and `NegativeCoordinatesDoNotMirrorTheWorld` — the tiling
  and mirroring failures the old sampler had.

One pre-existing test was corrected rather than satisfied. `TerrainHasBedrockBelowSurface` scanned
bricks from the region's vertical centre *upward* while claiming to check below the surface; it
passed only because the old generator put its base height at exactly that centre, so it was
asserting the surface's altitude rather than that ground is solid. It is now
`TerrainHasSolidGroundBelowTheSurface`, which finds the surface and checks beneath it, plus
`DeepGroundIsBedrock`.

**T040 remains unmarked.** It is the judgement gate — look at a generated cottage and decide
whether parametric output is good enough to build seven more milestones on — and no test can
answer it.

## Phase 1: Setup

- [X] T001 Create `Assets/VoxelEngine/Core/Features/` and confirm it is covered by the existing `VoxelEngine.Core` assembly in `Assets/VoxelEngine/Core/VoxelEngine.Core.asmdef`
- [X] T002 [P] Extend the float-ban analyzer rule to `Core/Features` and `Core/Terrain` in the analyzer configuration under `Assets/VoxelEngine/Core/`
- [X] T003 [P] Create the authoring tools assembly at `Assets/VoxelEngine/Tools/Features/VoxelEngine.Tools.Features.asmdef`, editor-only, referencing `VoxelEngine.Core`
- [X] T004 [P] Create the test assembly at `Assets/Tests/Features/VoxelEngine.Tests.Features.asmdef` referencing `VoxelEngine.Core` and `VoxelEngine.Net`
- [X] T005 [P] Create the sample catalogue location `Assets/StreamingAssets/Catalogues/` with a README stating that catalogues are world identity and must not be edited mid-session
- [X] T006 [P] Add `specs/002-world-feature-authoring/architecture-notes.md` as a stub for reasoning that outgrows the plan

---

## Phase 2: Foundational (blocking prerequisites)

**Nothing in Phase 3 onward may start until this phase completes.** Two items here are gates
rather than conveniences: T009 closes Constitution gate VI, and T010–T012 replace a terrain
sampler that is currently wrong in a way that would silently corrupt every placement decision.

- [X] T007 Record this feature's budgets in `specs/001-destructible-voxel-engine/device-matrix.md` as a "World features" section, marked as simulation parameters identical across all tiers (Constitution IV)
- [X] T008 [P] Mirror those budgets as compile-time constants in `Assets/VoxelEngine/Core/Features/FeatureBudget.cs`, with a comment pointing at device-matrix.md as the source
- [X] T009 Verify Constitution gate VI is closed by confirming no number in `plan.md` or `research.md` R-008 lacks a counterpart in device-matrix.md

**Terrain must become a pure, world-continuous function.** `TerrainGenerator.SampleSurfaceHeight`
currently reduces its inputs modulo the region edge, so it produces identical terrain in every
region. Placement rules read ground height and slope; building on that sampler would place every
village in the same relative spot in every region.

- [X] T010 Replace the region-tiling noise in `Assets/VoxelEngine/Core/Terrain/TerrainGenerator.cs` with world-continuous integer value noise that is a pure function of world coordinates
- [X] T011 [P] Add `Assets/VoxelEngine/Core/Terrain/TerrainSampler.cs` exposing `HeightAt(int x, int z)` and `SlopeAt(int x, int z)` as the only terrain surface generation may read
- [X] T012 [P] Add a parity test in `Assets/Tests/Parity/TerrainContinuityTests.cs` asserting height is continuous across region boundaries and identical when sampled from either side

**Catalogue data structures** — blittable, Burst-compatible, no managed references.

- [X] T013 [P] Define `Primitive` (shape, bounds, material, mode, order) in `Assets/VoxelEngine/Core/Features/Primitive.cs`
- [X] T014 [P] Define `ParameterSpec` and `ParameterSet` in `Assets/VoxelEngine/Core/Features/ParameterSpec.cs`
- [X] T015 [P] Define `AnchorSpec`, `ResolvedAnchor`, and `SlotSpec` in `Assets/VoxelEngine/Core/Features/AnchorSpec.cs`
- [X] T016 [P] Define `FeatureDefinition` per data-model.md in `Assets/VoxelEngine/Core/Features/FeatureDefinition.cs`
- [X] T017 [P] Define `PlacementRule` and `ExplicitPlacement` in `Assets/VoxelEngine/Core/Features/PlacementRule.cs`
- [X] T018 Define `FeatureCatalogue` as an immutable blob with `Version` and `CatalogueHash` in `Assets/VoxelEngine/Core/Features/FeatureCatalogue.cs`
- [X] T019 Implement catalogue loading and hashing in `Assets/VoxelEngine/Core/Features/CatalogueLoader.cs`, refusing to load a version the evaluator does not implement
- [X] T020 [P] Add integer hash helpers shared with `DeterministicRandom` in `Assets/VoxelEngine/Core/Features/FeatureHash.cs`

**The harness that catches almost everything.**

- [X] T021 Build the order-independence harness in `Assets/Tests/Parity/GenerationOrderHarness.cs`: generate a region block in N shuffled orders and compare the resulting brickmaps byte for byte
- [X] T022 [P] Add a sub-volume equality helper to `Assets/Tests/Features/SubVolumeEquality.cs` comparing whole-volume rasterisation against a tiling of disjoint sub-volumes
- [X] T023 [P] Add a catalogue test fixture with one hand-written definition in `Assets/Tests/Features/Fixtures/CottageFixture.cs`

---

## Phase 3: US1 — A definition becomes voxels where you put it (P1)

**Goal**: a parametric definition, placed at an explicit coordinate, generates correct voxels —
and generates identically whether rasterised whole or in pieces.

**Independent test**: place the cottage fixture at a fixed coordinate; assert the world contains a
cottage, that eight disjoint sub-volume rasterisations equal one whole rasterisation, and that
changing a parameter changes the result deterministically.

- [X] T024 [P] [US1] Define the opcode enum per contracts/shape-program.md in `Assets/VoxelEngine/Core/Features/ShapeOps.cs`
- [X] T025 [US1] Implement the shape program register file and execution loop in `Assets/VoxelEngine/Core/Features/ShapeProgram.cs`
- [X] T026 [P] [US1] Implement `EMIT_BOX` and `EMIT_RAMP` in `Assets/VoxelEngine/Core/Features/Emitters/BoxEmitter.cs`
- [X] T027 [P] [US1] Implement `EMIT_CYLINDER` with integer circle rasterisation in `Assets/VoxelEngine/Core/Features/Emitters/CylinderEmitter.cs`
- [X] T028 [P] [US1] Implement `EMIT_PRISM` with gable, shed, and arch profiles in `Assets/VoxelEngine/Core/Features/Emitters/PrismEmitter.cs`
- [X] T029 [P] [US1] Implement `EMIT_CAPSULE_CHAIN` in `Assets/VoxelEngine/Core/Features/Emitters/CapsuleChainEmitter.cs`
- [X] T030 [US1] Implement structured control flow — `REPEAT`, `IF_RANGE`, `PUSH_TRANSFORM`, `POP_TRANSFORM` — with statically computable trip counts in `Assets/VoxelEngine/Core/Features/ShapeProgram.cs`
- [X] T031 [US1] Implement `DRAW_RANGE` seeded integer parameter draws honouring `Quantum` in `Assets/VoxelEngine/Core/Features/ParameterDraw.cs`
- [X] T032 [US1] Implement `SET_ANCHOR` recording into the resolved anchor list in `Assets/VoxelEngine/Core/Features/ShapeProgram.cs`
- [X] T033 [US1] Implement the primitive rasteriser with exact sub-volume clipping in `Assets/VoxelEngine/Core/Features/PrimitiveRasteriser.cs`, writing through the existing voxel write path
- [X] T034 [US1] Enforce the per-region primitive cap with a loud failure rather than truncation in `Assets/VoxelEngine/Core/Features/PrimitiveRasteriser.cs` (FR-036)
- [X] T035 [US1] Apply explicit placements during region generation in `Assets/VoxelEngine/Core/Features/FeatureGeneration.cs`
- [X] T036 [P] [US1] Test: sub-volume equality for the cottage fixture in `Assets/Tests/Features/SubVolumeEqualityTests.cs`
- [X] T037 [P] [US1] Test: parameter draws are identical across runs and platforms in `Assets/Tests/Parity/ParameterDrawParityTests.cs`
- [X] T038 [P] [US1] Test: every emitted primitive lies inside the declared footprint in `Assets/Tests/Features/FootprintContainmentTests.cs`
- [X] T039 [P] [US1] Test: the evaluator contains no float path, asserted by analyzer output in `Assets/Tests/Features/NoFloatInGenerationTests.cs`
- [ ] T040 [US1] **Look at it.** Generate the cottage in the showcase scene and judge whether parametric output is good enough to build on (plan risk 2). Record the verdict in `specs/002-world-feature-authoring/architecture-notes.md`

---

## Phase 4: US2 — Features appear worldwide, identically, seam-free (P2)

**Goal**: placement rules scatter features across the world; every client and every generation
order produces the same world.

**Independent test**: generate a 3×3 region block in 100 shuffled orders and compare byte for
byte; walk a castle spanning four regions and find no seam.

- [ ] T041 [US2] Implement `CandidatesInCell` as a pure function of `(seed, definitionId, cellCoord)` in `Assets/VoxelEngine/Core/Features/PlacementLattice.cs`
- [ ] T042 [US2] Implement position jitter within a cell and cardinal orientation selection in `Assets/VoxelEngine/Core/Features/PlacementLattice.cs`
- [ ] T043 [P] [US2] Implement derived identity `hash(definitionId, cellCoord, attempt)` in `Assets/VoxelEngine/Core/Features/InstanceId.cs`
- [ ] T044 [P] [US2] Implement integer accept probability out of 65536 in `Assets/VoxelEngine/Core/Features/PlacementLattice.cs`
- [ ] T045 [P] [US2] Implement altitude and slope filters reading `TerrainSampler` in `Assets/VoxelEngine/Core/Features/PlacementFilters.cs`
- [ ] T046 [P] [US2] Implement minimum spacing and clustering within a cell in `Assets/VoxelEngine/Core/Features/PlacementFilters.cs`
- [ ] T047 [P] [US2] Implement exclusion masks, including protected zones, in `Assets/VoxelEngine/Core/Features/PlacementFilters.cs`
- [ ] T048 [US2] Implement the bounded neighbourhood scan, sized per definition rather than by the catalogue maximum, in `Assets/VoxelEngine/Core/Features/CandidateScan.cs`
- [ ] T049 [US2] Implement `(Precedence, InstanceId)` total ordering and overlap resolution in `Assets/VoxelEngine/Core/Features/CandidateScan.cs`
- [ ] T050 [US2] Enforce the per-region candidate cap with a report rather than truncation in `Assets/VoxelEngine/Core/Features/CandidateScan.cs`
- [ ] T051 [US2] Integrate candidate scanning and rasterisation into region generation in `Assets/VoxelEngine/Core/Features/FeatureGeneration.cs`
- [ ] T052 [US2] Slice feature generation into the existing time-budgeted region generation so a region resumes mid-feature in `Assets/VoxelEngine/Streaming/RegionLoader.cs`
- [ ] T053 [P] [US2] Test: 100 shuffled generation orders produce byte-identical worlds, using the Phase 2 harness, in `Assets/Tests/Parity/PlacementOrderTests.cs`
- [ ] T054 [P] [US2] Test: a feature spanning four regions has no seam or duplicated content in `Assets/Tests/Features/RegionSeamTests.cs`
- [ ] T055 [P] [US2] Test: identity is stable across eviction and regeneration in `Assets/Tests/PlayMode/InstanceIdentityStabilityTests.cs`
- [ ] T056 [P] [US2] Test: player alterations survive eviction and override regenerated feature voxels in `Assets/Tests/PlayMode/FeatureAlterationPersistenceTests.cs` (scenario 4, SC-005)
- [ ] T057 [P] [US2] Test: candidate and primitive caps report rather than truncate in `Assets/Tests/Features/BudgetOverflowTests.cs`
- [ ] T058 [US2] Measure feature generation cost per region against the T007 budget and record the result in `specs/002-world-feature-authoring/architecture-notes.md` (plan risk 3)

---

## Phase 5: US3 — Structures sit on the ground (P3)

**Goal**: a house on a slope meets the terrain on every side, with no step where a region
boundary crosses it.

**Independent test**: place instances across slopes up to the declared maximum and assert no
floating corner, no burial past ground-floor openings, and no discontinuity at region borders.

- [ ] T059 [US3] Implement base plane rules — lowest, mean, highest, fixed — sampling the whole footprint regardless of the slice being generated, in `Assets/VoxelEngine/Core/Features/TerrainAdaptation.cs`
- [ ] T060 [US3] Emit foundation fill prisms from the base plane down to the terrain surface in `Assets/VoxelEngine/Core/Features/TerrainAdaptation.cs`
- [ ] T061 [US3] Emit carve prisms above the base plane so terrain does not bury the structure in `Assets/VoxelEngine/Core/Features/TerrainAdaptation.cs`
- [ ] T062 [P] [US3] Implement `SAMPLE_GROUND` as the only world-reading opcode in `Assets/VoxelEngine/Core/Features/ShapeProgram.cs`
- [ ] T063 [P] [US3] Reject candidates whose ground exceeds the definition's `MaxSlope` in `Assets/VoxelEngine/Core/Features/PlacementFilters.cs`
- [ ] T064 [P] [US3] Test: no instance floats or is buried past its openings across a slope sweep in `Assets/Tests/Features/TerrainAdaptationTests.cs` (SC-007)
- [ ] T065 [P] [US3] Test: base plane is identical when derived from any region touching the instance in `Assets/Tests/Features/BasePlaneSeamTests.cs` (SC-003)
- [ ] T066 [US3] **Look at it.** Judge adaptation quality on real terrain; automated tests pass on ugly results (plan risk 4)

---

## Phase 6: US4 — Castles: composition and validation (P4)

**Goal**: a castle expressed as keep, walls, towers, and gatehouse generates correctly across four
regions, and a broken definition is reported before it reaches the world.

**Independent test**: load a deliberately broken catalogue and confirm every FR-009 failure mode
is named in the report; generate the castle fixture and traverse it.

- [ ] T067 [US4] Implement `CALL_SLOT` evaluation of composed definitions in `Assets/VoxelEngine/Core/Features/ShapeProgram.cs`
- [ ] T068 [P] [US4] Implement slot placement — count, spacing, orientation within the parent footprint — in `Assets/VoxelEngine/Core/Features/SlotResolution.cs`
- [ ] T069 [P] [US4] Implement acyclic slot graph validation in `Assets/VoxelEngine/Core/Features/CatalogueValidation.cs`
- [ ] T070 [P] [US4] Implement static footprint proof over the parameter space corners in `Assets/VoxelEngine/Core/Features/CatalogueValidation.cs`
- [ ] T071 [P] [US4] Implement degenerate-combination detection in `Assets/VoxelEngine/Core/Features/CatalogueValidation.cs`
- [ ] T072 [P] [US4] Implement material existence and palette resolution checks in `Assets/VoxelEngine/Core/Features/CatalogueValidation.cs`
- [ ] T073 [P] [US4] Implement statically computable maximum primitive count per definition in `Assets/VoxelEngine/Core/Features/CatalogueValidation.cs`
- [ ] T074 [US4] Implement the designer-readable validation report in `Assets/VoxelEngine/Core/Features/ValidationReport.cs`
- [ ] T075 [P] [US4] Add the castle fixture composed of keep, walls, towers, and gatehouse in `Assets/Tests/Features/Fixtures/CastleFixture.cs`
- [ ] T076 [P] [US4] Test: the castle generates identically across four regions in any order in `Assets/Tests/Parity/CompositionOrderTests.cs`
- [ ] T077 [P] [US4] Test: every FR-009 failure mode produces a named report entry in `Assets/Tests/Features/CatalogueValidationTests.cs` (SC-010)

---

## Phase 7: US5 — Caves and water (P5)

**Goal**: cave systems whose passages meet at cell boundaries without negotiation, and static
water that behaves as a material rather than a simulation.

**Independent test**: walk every surface opening into the cave system and out again; remove water
and confirm it does not refill.

- [ ] T078 [US5] Implement canonical portal hashing over ordered cell pairs in `Assets/VoxelEngine/Core/Terrain/CaveLattice.cs`
- [ ] T079 [US5] Implement within-cell tunnel chains spanning that cell's portals and chambers in `Assets/VoxelEngine/Core/Terrain/CaveLattice.cs`
- [ ] T080 [P] [US5] Implement chamber generation as carve primitives in `Assets/VoxelEngine/Core/Terrain/CaveLattice.cs`
- [ ] T081 [P] [US5] Reconcile surface openings with terrain height so both agree in `Assets/VoxelEngine/Core/Terrain/CaveLattice.cs`
- [ ] T082 [P] [US5] Jitter portal position within a face and vary portal probability with depth to break grid alignment in `Assets/VoxelEngine/Core/Terrain/CaveLattice.cs` (plan risk 5)
- [ ] T083 [P] [US5] Register the water material with a destruction class that does not spread in `Assets/VoxelEngine/Core/Storage/MaterialPalette.cs`
- [ ] T084 [P] [US5] Implement water volume emission as ordinary fill primitives in `Assets/VoxelEngine/Core/Features/Emitters/WaterEmitter.cs`
- [ ] T085 [P] [US5] Test: adjacent cells derive identical portals from either side in `Assets/Tests/Features/CavePortalAgreementTests.cs`
- [ ] T086 [P] [US5] Test: every surface opening is reachable from inside the cave in `Assets/Tests/Features/CaveTraversabilityTests.cs` (SC-008)
- [ ] T087 [P] [US5] Test: water is destructible and does not refill in `Assets/Tests/Features/StaticWaterTests.cs` (FR-023)

---

## Phase 8: US6 — Identity, ownership, protection (P6)

**Goal**: instances are addressable, ownable, and protectable, with the server as authority and
storage bounded by interaction.

**Independent test**: claim and protect an instance, attempt to destroy it from another client,
observe rejection with a reason; have a third client join and receive current state.

- [ ] T088 [US6] Implement the server-side instance state map keyed by derived id in `Assets/VoxelEngine/Net/Server/InstanceState.cs`
- [ ] T089 [P] [US6] Implement first-touch allocation so untouched instances cost nothing in `Assets/VoxelEngine/Net/Server/InstanceState.cs` (Constitution V)
- [ ] T090 [P] [US6] Implement claim, release, protect, and unprotect transitions in `Assets/VoxelEngine/Net/Server/InstanceState.cs`
- [ ] T091 [US6] Enforce protection on the single existing mutation path in `Assets/VoxelEngine/Net/Server/Validation.cs`, returning a rejection reason (FR-030)
- [ ] T092 [P] [US6] Implement anchor resolution by instance id in `Assets/VoxelEngine/Core/Features/InstanceId.cs` (FR-027)
- [ ] T093 [P] [US6] Add the instance state replication message in `Assets/VoxelEngine/Net/Protocol/InstanceStateMessage.cs`
- [ ] T094 [P] [US6] Include instance state in the late-join snapshot in `Assets/VoxelEngine/Net/Server/LateJoin.cs` (SC-012)
- [ ] T095 [P] [US6] Surface rejection reasons to the player in `Assets/VoxelEngine/Net/Client/RejectionFeedback.cs`
- [ ] T096 [P] [US6] Test: protected instances reject alterations in 100% of attempts with a reason in `Assets/Tests/PlayMode/ProtectedInstanceTests.cs` (SC-013)
- [ ] T097 [P] [US6] Test: anchors resolve identically on every client and after eviction in `Assets/Tests/Parity/AnchorResolutionTests.cs` (SC-011)
- [ ] T098 [P] [US6] Test: instance state memory scales with touched instances, not world size, in `Assets/Tests/PlayMode/InstanceStateMemoryTests.cs`

---

## Phase 9: US7 — Visible from a distance (P7)

**Goal**: a castle on a ridge is identifiable at maximum view distance.

**Independent test**: render-diff a distant structure against a frame with feature far-field
disabled, as the existing far-field test does.

- [ ] T099 [US7] Implement coarse primitive collection for the far field in `Assets/VoxelEngine/Rendering/FarFieldFeatures.cs`
- [ ] T100 [US7] Rasterise coarse primitives in the far-field path of `Assets/VoxelEngine/Rendering/Shaders/BrickRaymarch.compute`
- [ ] T101 [P] [US7] Ensure features contribute to region occupancy mips in `Assets/VoxelEngine/Core/Occupancy/MipBuilder.cs`
- [ ] T102 [P] [US7] Allow far-field feature detail to tier by device class while candidates do not, in `Assets/VoxelEngine/Tiering/DeviceTierBudget.cs` (Constitution IV)
- [ ] T103 [P] [US7] Test: a distant structure is identifiable at maximum view distance in `Assets/Tests/PlayMode/DistantFeatureVisibilityTests.cs` (SC-006)
- [ ] T104 [P] [US7] Test: no collision query consults far-field primitives in `Assets/Tests/Features/FarFieldIsolationTests.cs` (Constitution II)

---

## Phase 10: US8 — Authoring is a designer's job (P8)

**Goal**: a designer adds a feature type and sees it in the world in under 30 minutes, without
engineering help.

**Independent test**: hand the tooling and contracts/catalogue-format.md to someone who has not
built it, and time them.

- [ ] T105 [US8] Implement the catalogue text format parser per contracts/catalogue-format.md in `Assets/VoxelEngine/Tools/Features/CatalogueCompiler.cs`
- [ ] T106 [US8] Implement the isolated preview window with live parameter sweeps in `Assets/VoxelEngine/Tools/Features/FeaturePreview.cs` (FR-038)
- [ ] T107 [P] [US8] Show water volumes and their support in the preview so authors can see where static water will float (FR-024) in `Assets/VoxelEngine/Tools/Features/FeaturePreview.cs`
- [ ] T108 [P] [US8] Implement the placement inspector answering why a feature was or was not placed at a location in `Assets/VoxelEngine/Tools/Features/PlacementInspector.cs` (FR-039)
- [ ] T109 [P] [US8] Surface validation reports in the editor with file and line references in `Assets/VoxelEngine/Tools/Features/ValidationWindow.cs`
- [ ] T110 [P] [US8] Write the authoring guide in `specs/002-world-feature-authoring/quickstart.md`, extending it with a worked cottage example
- [ ] T111 [US8] Measure SC-002: time an unfamiliar author adding a feature type; record the result and what slowed them in `specs/002-world-feature-authoring/architecture-notes.md`
- [ ] T112 [P] [US8] Test: one definition yields at least ten visually distinguishable instances by parameters alone in `Assets/Tests/Features/ParameterVarietyTests.cs` (SC-014)

---

## Final Phase: Polish & Cross-Cutting

- [ ] T113 Reconcile measured generation cost with device-matrix.md and amend the numbers there if measurement disagrees with T007
- [ ] T114 [P] Add a long-session memory flatness soak covering instance state and the brick pool in `Assets/Tests/PlayMode/FeatureMemoryStabilityTests.cs` (Constitution V)
- [ ] T115 [P] Add editor-lifecycle leak checks for any native or GPU resource introduced by this feature in `Assets/Tests/EditMode/FeatureResourceLifetimeTests.cs`
- [ ] T116 [P] Add cross-hardware parity to the continuous test set rather than a milestone check, in `Assets/Tests/Parity/` (Constitution I)
- [ ] T117 [P] Record the design reasoning that outgrew plan.md in `specs/002-world-feature-authoring/architecture-notes.md`
- [ ] T118 Update `AGENTS.md` standing constraints if any invariant here proves load-bearing enough to belong there

---

## Dependencies

```text
Phase 1 Setup
    │
    ▼
Phase 2 Foundational ─── T007 (budgets, gate VI) ── blocks everything
                     └── T010-T012 (terrain sampler) ── blocks US2, US3, US5
    │
    ▼
US1 (P1) ── shape programs, rasteriser ── blocks US2, US3, US4, US5, US7
    │
    ▼
US2 (P2) ── placement, identity ── blocks US3, US4, US6, US7
    │
    ├──► US3 (P3) terrain adaptation ──► US4 (P4) composition
    │                                        │
    ├──► US5 (P5) caves and water ◄──────────┘ (needs carve + composition)
    │
    ├──► US6 (P6) identity state ── needs US2 identity only
    │
    └──► US7 (P7) far field ── needs US2 candidates
                    │
                    ▼
                US8 (P8) tooling ── needs US1 minimum; better after US4
```

**US6 and US7 are genuinely parallel** once US2 lands — they share no files. US3 and US5 both
touch terrain but different files.

**T040 and T066 are judgement gates, not code.** They exist because two of the plan's risks are
"the tests pass and it looks wrong". Skipping them saves an hour and costs a milestone.

---

## Parallel Execution Examples

**Phase 2, after T007–T009**: T010–T012 (terrain), T013–T017 (data structures), and T020–T023
(hashing and harness) are three independent tracks.

**Phase 3, after T025**: the four emitters T026–T029 are one file each and fully parallel.

**Phase 4, after T041–T043**: filters T044–T047 are independent of each other; tests T053–T057
parallelise once T051 lands.

**Phase 6**: validation checks T069–T073 are one concern each in the same file — parallel in
principle, serialised by the file in practice. Consider splitting `CatalogueValidation.cs` per
check if more than one person works here.

**Phases 8 and 9**: US6 and US7 can run concurrently by different people.

---

## Implementation Strategy

**MVP is US1 alone.** It proves the authoring→primitive→voxel path and answers whether parametric
authoring is acceptable. It ships nothing to players, and that is fine: this is engine work, and
the alternative is discovering the answer after seven more milestones.

**Then US2.** Placement is where this design's central claim — region-local generation with no
communication — either holds or does not. T053 is the test that decides it. If shuffled-order
generation diverges, stop and fix it before building anything on top; a divergence here is the
silent cross-hardware failure the constitution exists to prevent.

**US3 through US5 are incremental world quality.** Each is independently testable and each makes
the world visibly better.

**US6 and US7 are parallel and additive.** Neither blocks the other.

**US8 should move earlier than its number suggests.** Plan risk 2 says parametric authoring may
prove too rigid, and the only way to find out is to have a designer use it. Bringing T106
(preview) forward to just after US1 converts an eight-milestone bet into a one-milestone
experiment. The task order here is dependency order, not a recommendation to leave tooling last.
