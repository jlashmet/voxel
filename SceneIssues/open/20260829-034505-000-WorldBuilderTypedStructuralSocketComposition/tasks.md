# Tasks

## Architecture / canonical production path
- [x] Resume `fixes/agent-5` from current `origin/master`; keep this assignment isolated and use only `ci-test/fixes/agent-5` for targeted CI.
- [x] Read `AGENTS.md`, the available `SceneIssues/README.md`, issue metadata, plan, implementation, tests, and exact built-player harness. (`SceneIssues/feature-readme.md` is absent in the repository.)
- [x] Consolidate structural composition on the existing `FeatureDefinition` + typed `SlotSpec` + `ShapeOp.CallSlot` + `FeatureCatalogue` path; leave no second structural solver.
- [x] Keep stable socket identity, role/tags, cardinal facing, integer transforms, clearance, capacity/cardinality, support probes, required/optional semantics, support-loss invalidation, and decoration handoff data-driven and inspectable.
- [x] Make deterministic child selection independent of generation order and enforce semantic compatibility, orientation, clearance/overlap, spacing, support, capacity, recursion/depth, child-count, primitive/voxel cost, and spatial-extent budgets.
- [x] Preserve one semantic structure while accepted child pieces remain independently bounded/streamable authoritative voxel/collision/destruction/storage content.
- [x] Keep decoration handoff as an adapter into existing decoration sockets; do not turn structural sockets into a micro-detail placement system.
- [x] Include socket metadata/composed graph identity in deterministic catalogue/world hashing and expose accepted/rejected attachment diagnostics plus final `GraphHash`.

## Authoritative realization / regression
- [x] Expand accepted structural children to physical placements before per-region rasterization and discover descendants outside the root logical region.
- [x] Charge structural roots/children against resumable region scanning and existing deterministic budgets without weakening device/global limits.
- [x] Regress identical-seed graph identity, alternate-seed bounded variation, generation-order independence, required/optional behavior, incompatible semantics, orientation, clearance, support, capacity, recursion/depth, child/primitive/voxel/spatial budget failures, inspection metadata, and graph hashing.
- [x] Regress the exact conservative voxel-cost boundary (16,777,216 accepted; one over rejected).
- [x] Regress authoritative cross-region child rasterization/provenance and decoration-handoff adaptation.
- [x] Add focused PlayMode coverage over the four production gallery proof catalogues and invalid attachments.

## Required proving cases
- [x] Bridge structural graph: terrain anchors, repeated independently bounded span/support pieces, road/traversal continuation sockets, incompatible/orientation rejection, multi-region authoritative geometry, continuous traversable deck.
- [x] Castle structural graph: >=2 wall runs, >=2 towers, gatehouse/opening, generic continuation sockets, correctly oriented joins, incompatible roof/facade/bridge rejection, traversable entrance.
- [x] Cliff structural graph: two elevations, terrain-supported anchors, platforms/building plus traversable vertical connection, unsupported candidate rejection.
- [x] Facade/roof structural graph: facade + roof attachment, two style variants through the same semantic contracts, meso-scale sockets only.
- [x] Production `CharacterMotor` regression traverses bridge, gate, and cliff/vertical connection without changing motor tolerance.
- [x] Integrate all four proof cases into the exact built `WorldbuildingGalleryShowcase` and existing audit harness.

## Presentation / art quality
- [x] Add an authoritative-voxel presentation pass (not GameObject-only meshes or a parallel solver) for construction hierarchy, materials, grounding, and environment context.
- [x] Split river/banks/piers, castle sections, cliff sections, and facade variants into independent bounded catalogues after run `33323976945` correctly rejected a monolithic presentation footprint with `VoxelBudgetExceeded`.
- [x] Correct split-piece local bounds: castle buttresses stay inside declared footprints and pier height covers the actual terrain-derived cap.
- [ ] Bridge-wide must visibly read as a monumental crossing over a substantial gorge/river between grounded masses; run `33324919718` still reads as a long slab over shallow/flat terrain with large empty green background.
- [ ] Bridge close/seam framing must be above/along the traversable deck and clearly show deck edge, rail/truss rhythm, abutment/pier contact, and continuation seam; run `33324919718` camera is under the bridge with a large empty render field.
- [ ] Bridge architecture must move beyond slab/support blockout: strengthen abutment massing, pier hierarchy, span cadence/cross-bracing, edge/parapet detail, and visible grounding while preserving the typed structural graph and budgets.
- [ ] Castle-wide/gate views must read as a finished stylized voxel fortification: stronger tower/gatehouse silhouette, wall-walk/cornice hierarchy, depth around gate and wall bays, intentional materials, and no floating/flat placeholder slabs.
- [ ] Cliff-wide must unmistakably communicate a steep supported multi-level settlement rather than a mostly flat walkway; eliminate the apparently floating foreground slab and make terrain/platform support/vertical rise legible.
- [ ] Cliff close view must show a believable walkable vertical connection with posts/rails/supports grounded at both landings and enough camera elevation/parallax to prove the level change.
- [ ] Civic and ornate facade views must isolate each variant at useful player-height scale, avoid unrelated intruding geometry/terrain cutoffs, improve readable material contrast, and show facade/roof/balcony/dormer hierarchy as finished architecture rather than dark box masses.
- [ ] Reframe structural audit cameras relative to generated proof geometry/presentation bounds so every evidence frame demonstrates the intended structure instead of under-deck, far-void, or unrelated-content views.
- [ ] Inspect every final full-resolution structural frame and classify each `production-quality`; any lesser classification keeps the issue open.

## Exact-SHA validation evidence
- [x] Mechanical checkpoint `33314706183` proved focused PlayMode, all three traversals, negative contracts, and eight frame emission before the visual rework.
- [x] Diagnose/fix final-visual compile run `33323693205` (`Debug` ambiguity) without changing composition behavior.
- [x] Diagnose/fix run `33323976945` built-player `VoxelBudgetExceeded` by partitioning presentation content rather than raising budgets.
- [x] Run `33324919718` from exact source `3fa2e905e2d8d65504b34634b7ecf4cb9a68a0c3`: focused PlayMode and exact built-player audit both green; all three traversals and negative contracts pass; eight structural frames emitted.
- [x] Measure run `33324919718`: bridge/castle planning 0.011/0.004 ms; proof aggregate 20 children, 51 primitives, 24,606,640 conservative voxels, 15 regions, 40 instances, 16,564,128 voxel writes; authoring 1710.726 ms including 914.307 ms presentation; 71 resident regions; 2558.19 MB reported allocation; 15 render-proxy regions.
- [x] Confirm each split presentation catalogue stays below existing per-composition voxel/footprint ceilings in the green built-player logs; no global budget was raised.
- [ ] After visual fixes, rerun one final exact-SHA PlayMode + exact-scene built-player request on the same CI transport and inspect all durable source frames.
- [ ] Final built-player production `CharacterMotor` traverses bridge, gate, and vertical connection and all required negative contracts still pass.

## Reusability / blast radius / closure
- [x] Generic solver contains no bridge/castle/cliff special cases; different structure families consume the same typed contracts.
- [x] Feature-only diff is confined to structural contracts/runtime, focused tests/adapters, gallery proof/audit presentation, and this SceneIssue; no unrelated assignment or workflow change is on `fixes/agent-5`.
- [x] No global composition/region/device budget or `CharacterMotor` tolerance is weakened for the showcase.
- [ ] Review final feature diff and final measured cost after the last visual fix.
- [ ] Complete `issue.json` pending metadata only after final exact-SHA mechanical + built-player + visual gates are green.
- [ ] Move only this assignment `open -> pending` in a separate bookkeeping commit.
- [ ] Move `pending -> closed`, set `status=fixed` and `resolvedUtc`, only after every acceptance/task above is complete.
- [ ] Fetch latest `origin/master`, merge into `fixes/agent-5` if advanced, push feature branch, then push that exact head to `origin/master` non-force; retry if master advances.
