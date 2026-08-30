# Tasks

## Architecture / canonical production path
- [x] Resume `fixes/agent-5`, follow current `AGENTS.md` / feature/common SceneIssues rules, keep this assignment isolated, and use only `ci-test/fixes/agent-5` for targeted CI.
- [x] Consolidate structural composition on existing `FeatureDefinition` + typed `SlotSpec` + `ShapeOp.CallSlot` + `FeatureCatalogue`; no second structural solver.
- [x] Keep stable socket identity, semantic role/tags, facing, integer transforms, clearance, cardinality/capacity, support probes, required/optional behavior, support-loss invalidation, decoration handoff, and graph diagnostics data-driven/inspectable.
- [x] Make deterministic child selection generation-order independent; enforce compatibility, orientation, clearance/overlap, spacing, support, capacity, recursion/depth, child, primitive, voxel, and spatial budgets.
- [x] Preserve one semantic structure while child pieces remain independently bounded/streamable authoritative voxel/collision/destruction/storage content.
- [x] Keep decoration handoff as an adapter into existing decoration sockets; no structural micro-detail subsystem.
- [x] Include socket/composed graph identity in deterministic hashing and expose accepted/rejected diagnostics plus final `GraphHash`.

## Authoritative realization / regression
- [x] Expand accepted children to physical placements before per-region rasterization; discover descendants outside root logical region.
- [x] Charge structural roots/children against resumable region scanning and existing deterministic budgets without weakening global/device limits.
- [x] Regress seed determinism/variation, generation-order independence, required/optional behavior, incompatible semantics, orientation, clearance, support, capacity, recursion/depth, child/primitive/voxel/spatial budget failures, inspection metadata, and graph hashing.
- [x] Regress exact conservative voxel-cost boundary (16,777,216 accepted; one over rejected).
- [x] Regress authoritative cross-region rasterization/provenance and decoration-handoff adaptation.
- [x] Prove reuse with independent fixture/consumer coverage and focused PlayMode coverage over the four production gallery proof catalogues.
- [x] Add focused final-refinement castle traversal regression using the same showcase traversal audit as the built-player harness; expose that showcase helper publicly rather than duplicate motor logic.
- [x] Keep planner-only tests on a small 4096-brick world; after `33331734570` proved full refinement exhausts that fixture, run only the full-refinement regression at the production gallery capacity 262144.

## Required proving cases
- [x] Bridge graph: terrain anchors, repeated independently bounded span/support pieces, road/traversal continuation sockets, incompatible/orientation rejection, multi-region authoritative geometry, continuous traversable deck.
- [x] Castle graph: >=2 wall runs, >=2 towers, gatehouse/opening, continuation sockets, correct joins, incompatible roof/facade/bridge rejection, traversable entrance.
- [x] Cliff graph: two elevations, terrain-supported anchors, platforms/building plus traversable vertical connection, unsupported rejection.
- [x] Facade/roof graph: facade + roof attachment and two style variants through the same semantic contracts; meso-scale sockets only.
- [x] Production `CharacterMotor` traverses bridge, gate, and cliff/vertical connection without tolerance changes.
- [x] Integrate all four proof cases into the exact built `WorldbuildingGalleryShowcase` and existing audit harness.

## Presentation / art quality
- [x] Use authoritative voxels, not GameObject-only meshes or a parallel solver, for construction hierarchy, materials, grounding, and environment context.
- [x] Partition presentation into bounded river/bank/pier, castle, cliff, and facade catalogues after `33323976945` correctly rejected a monolithic presentation footprint.
- [x] Correct split-piece bounds and preserve the canonical 32-voxel castle passage; `33330327732` proved the former refinement base blocked route 1.
- [x] After the second visual failure (`33331734570`), isolate the visual root cause before another fix: evidence framing used base proof bounds while refined presentation extends outside them; bridge/cliff cameras were too low/inline; facade depth was occluded; gate ornament read flat/oversized.
- [x] Apply bounded composition readability polish required by that diagnosis: stronger gate portal hierarchy without blocking traversal, stepped cliff connection/support read, and facade relief moved to the visible front envelope.
- [ ] Bridge-wide visibly reads as a monumental crossing over substantial gorge/river between grounded masses.
- [ ] Bridge close view is above/along the traversable deck and clearly shows deck edge, rail/truss cadence, abutment/pier contact, and continuation seam.
- [ ] Bridge architecture reads beyond slab/support blockout with abutment massing, pier hierarchy, span cadence/cross-bracing, edge detail, and grounding.
- [ ] Castle-wide/gate read as finished stylized voxel fortification with tower/gatehouse silhouette, wall-walk/cornice hierarchy, gate depth, intentional materials, and no floating/placeholder slabs.
- [ ] Cliff-wide unmistakably communicates a supported multi-level settlement and steep level change; no floating foreground slab.
- [ ] Cliff close view shows a believable walkable vertical connection with grounded posts/rails/supports and enough elevation/parallax to prove the rise.
- [ ] Civic/ornate facade views isolate each variant at useful scale, avoid unrelated occlusion/cutoffs, and clearly show facade/roof/balcony/dormer/material hierarchy.
- [ ] Reframe all eight structural audit cameras around the authored presentation envelope so evidence demonstrates the intended structure rather than under-deck, far-void, or unrelated content.
- [ ] Inspect every final full-resolution structural frame and classify each `production-quality`; any lesser classification keeps the issue open.

## Exact-SHA validation evidence
- [x] `33314706183`: focused PlayMode, three traversals, negative contracts, and eight frames passed before visual rework.
- [x] `33323693205`: fix final-visual compile `Debug` ambiguity without behavior change.
- [x] `33323976945`: fix presentation `VoxelBudgetExceeded` by partitioning content rather than raising budgets.
- [x] `33324919718` source `3fa2e905…`: focused PlayMode + exact built-player audit green; three traversals/negative contracts pass; eight frames emitted. Aggregate proof: 20 children, 51 primitives, 24,606,640 conservative voxels, 15 regions, 40 instances, 16,564,128 writes; authoring 1710.726 ms incl. 914.307 ms presentation; 71 resident regions; 2558.19 MB reported allocation; 15 render-proxy regions.
- [x] `33330327732`: focused test passed; exact player exposed route-1 refined-gate obstruction after 1,500 steps.
- [x] `33331508390`: focused class compile failed only because reused showcase traversal helper was internal.
- [x] `33331734570`: focused class compiled; planner test passed; full-refinement test failed only at 4096-brick fixture capacity. Exact player independently passed all three traversals/negative contracts, castle route 1 in 77 steps with 1.317 m remaining, and emitted eight frames; visual review still rejected framing/readability.
- [ ] After camera/readability fixes, run one final exact-SHA PlayMode + exact-scene built-player request on the same CI transport.
- [ ] Final focused class is green and final built-player `CharacterMotor` traverses bridge, gate, vertical connection; required negative contracts pass.
- [ ] Record final measured cost and inspect all durable source frames.

## Reusability / blast radius / closure
- [x] Generic solver contains no bridge/castle/cliff special cases; different families consume the same typed contracts.
- [x] No global composition/region/device budget or `CharacterMotor` tolerance is weakened.
- [x] Merge current master workflow guidance before final attempt; latest integrated master is `ebdc2e4f63ef73153cd4e0ff5c62efe604f35470`.
- [ ] Review final feature diff and measured cost; verify only assignment-required structural contracts/runtime, focused tests/adapters, gallery proof/presentation/audit, and this SceneIssue differ from master.
- [ ] Complete `issue.json` `resolutionSummary`, `regressionTest`, and `fixCommit` only after all final exact-SHA mechanical/player/visual gates are green.
- [ ] Move only this assignment directly `open -> closed`, set `status=fixed` and `resolvedUtc`, after every acceptance/task above is complete.
- [ ] Fetch latest `origin/master`, merge if advanced, revalidate affected work as needed, push feature branch, then non-force push that exact head to `origin/master`; retry if master advances.
