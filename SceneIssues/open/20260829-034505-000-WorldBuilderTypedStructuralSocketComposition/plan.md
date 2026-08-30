# Plan

## Observed behavior / acceptance
- Canonical structural composition is the existing VoxelEngine path: `FeatureDefinition` + pooled `SlotSpec` + `ShapeOp.CallSlot` + `FeatureCatalogue` + `FeatureRegionBuild`. A second WorldBuilder solver would duplicate ownership.
- The branch expands `CallSlot` declarations into deterministic bounded child `StructuralInstance`s, hashes structural generation metadata into catalogue identity, validates structural call graphs, and feeds accepted descendants back through normal per-region voxel evaluation/rasterization.
- Focused regressions cover same-seed graph identity, required/optional semantics, semantic/orientation rejection, slot clearance, 3D repeated spacing, terrain/structural support, capacity, runtime depth, child/primitive/voxel/spatial budgets, bounded alternate-seed variation, rejected-alternative hash stability, cross-region generation-order independence, authoritative descendant voxels, and the exact conservative voxel-budget boundary.
- Authoring validation now covers the complete shared typed-socket contract before runtime use: stable IDs, semantic roles/tags, cardinal facing, integer bounds/transforms, clearance, capacity/cardinality, support probes, handoff consistency, cycles, call-depth, and bytecode slot references.
- Accepted attachment decisions expose support-loss invalidation, support probe, and engine-neutral decoration-handoff metadata; `Game.Structures.Runtime.StructuralDecorationHandoffAdapter` maps those flags to the existing decoration-space/socket system without taking over micro-detail placement.
- Structural composition simulation limits are reconciled with the authoritative device matrix as identical cross-tier limits. The 16,777,216-voxel composition cost is explicitly a conservative footprint-volume planning ceiling, not an actual voxel-write count and not a relaxation of per-region/per-instance raster budgets.
- `WorldbuildingGalleryShowcase` already constructs the production `CharacterMotor`, snaps it to authoritative voxel ground, and routes normal plus `AutoWalk` movement through `_motor.Step(...)`. Traversal proof must reuse this path rather than introduce a test-only mover.
- `WorldbuildingGalleryShowcase` normally restores a bake; typed structural proving content therefore must have a bounded stale-bake compatibility path as well as the live-generate path so built-player validation cannot silently exercise an old gallery image.
- `fixes/agent-5` remains based on current `master` for this attempt; refresh again before each later substantive gate and final integration.

## Hypotheses / discriminators / results
1. **Complete the predecessor path** — supported by catalogue ownership, definition-id rebasing, bytecode, region generation, and the focused production regressions above.
2. **Add a parallel structural solver** — rejected because it would duplicate canonical generation state and create save/network determinism risk.
3. **Inline child primitives into root evaluation** — falsified by footprint/streaming boundaries; descendants must remain independently bounded physical placements.
4. **Generic planner/contract gaps are the primary remaining blocker** — falsified after audit: shared authoring validation, composition budgets, voxel-cost boundary behavior, support-loss/decor metadata, decoration adaptation, negative regressions, allocation bounds, and accepted-graph hashing are covered. Remaining work is the four real production proving cases, exact-scene traversal/visual validation, and measured blast/cost evidence.

## Selected remaining fix
- Author four deterministic showcase proving cases (bridge, castle, cliff settlement, facade/roof variants) through the same production catalogue/voxel path; do not create scene-only structural geometry or another solver.
- Integrate the proving catalogue into both gallery startup modes: generate it directly for `ShowcaseStartupSource.Generate`, and use a bounded presence-check/repair pass after bake restore so a stale bake cannot omit the new structures.
- Reuse the exact scene's existing `CharacterMotor` path for bridge, gate, and vertical traversal evidence.
- Extend the existing gallery audit/player harness for durable frames and cost/traversal evidence; do not add a parallel validation harness.
- Record bridge/castle planning and bounded composition/raster/render proxies using the production harness, then audit final blast radius.
- Keep `tasks.md` authoritative: do not submit final CI or move the assignment until every remaining proving-case, built-app, measurement, and closure checkbox is complete.

## Gates / blast radius
- Keep changes scoped to structural catalogue/planner/region integration, existing Game.Structures handoff, focused tests, showcase harness/content, and this assignment metadata. Do not weaken global budgets.
- Before each substantive remaining attempt, refresh against current `origin/master` and resolve only in-scope conflicts.
- Measure bridge/castle planning time, child/primitive/voxel cost, region span, bounded memory model, and render proxy.
- Validate the exact built `WorldbuildingGalleryShowcase`, inspect durable player-height/wide frames and production-`CharacterMotor` traversal, then submit exactly one final exact-SHA request via `ci-test/fixes/agent-5`.
- Only after green exact-SHA CI and built-app gates: complete pending metadata/bookkeeping, move open -> pending, then pending -> closed, merge current master, and non-force push the exact feature head to master.
