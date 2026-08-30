# Plan

## Observed behavior / acceptance
- Canonical structural composition is the existing VoxelEngine path: `FeatureDefinition` + pooled `SlotSpec` + `ShapeOp.CallSlot` + `FeatureCatalogue` + `FeatureRegionBuild`. A second WorldBuilder solver would duplicate ownership.
- The branch expands `CallSlot` declarations into deterministic bounded child `StructuralInstance`s, hashes structural generation metadata into catalogue identity, validates structural call graphs, and feeds accepted descendants back through normal per-region voxel evaluation/rasterization.
- Focused regressions now cover same-seed graph identity, required/optional semantics, semantic/orientation rejection, slot clearance, 3D repeated spacing, terrain/structural support, capacity, runtime depth, child/primitive/voxel/spatial budgets, bounded alternate-seed variation, rejected-alternative hash stability, cross-region generation-order independence, and authoritative descendant voxels.
- Accepted attachment decisions expose support-loss invalidation, support probe, and engine-neutral decoration-handoff metadata; rejected alternatives remain inspectable diagnostics and do not alter accepted `GraphHash` identity.
- `WorldbuildingGalleryShowcase` already constructs the production `CharacterMotor`, snaps it to authoritative voxel ground, and routes normal plus `AutoWalk` movement through `_motor.Step(...)`. Traversal proof must reuse this path rather than introduce a test-only mover.
- `fixes/agent-5` was refreshed with current `master` in merge commit `3a1ad612efc77a41b307b36855c2df0dbcc76cf6` before this continuation attempt.

## Hypotheses / discriminators / results
1. **Complete the predecessor path** — supported by catalogue ownership, definition-id rebasing, bytecode, region generation, and the focused production regressions above.
2. **Add a parallel structural solver** — rejected because it would duplicate canonical generation state and create save/network determinism risk.
3. **Inline child primitives into root evaluation** — falsified by footprint/streaming boundaries; descendants must remain independently bounded physical placements.
4. **Generic planner contract gaps are the primary remaining blocker** — largely falsified by code/test reconciliation: the previously identified clearance, 3D spacing, voxel-cost, support-loss/decor metadata, negative-regression, variation/order, allocation, and graph-hash gaps are already implemented. Remaining work is primarily authoring validation/device-budget reconciliation, Game.Structures decoration handoff integration, the four production proving cases, exact-scene traversal/visual validation, and measured blast/cost evidence.

## Selected remaining fix
- Audit authoring-time `SlotSpec` validation and reconcile structural composition limits with the authoritative device matrix; add only focused production fixes/regressions for concrete uncovered gaps.
- Adapt engine-neutral structural decoration handoff into existing `Game.Structures.Api.DecorationSpace` / `DecorationSocketKind` consumers without moving micro-detail logic into structural sockets.
- Author four deterministic showcase proving cases (bridge, castle, cliff settlement, facade/roof variants) through the same production catalogue/voxel path.
- Reuse the exact scene's existing `CharacterMotor` path for bridge, gate, and vertical traversal evidence.
- Keep `tasks.md` authoritative: do not submit final CI or move the assignment until every remaining proving-case, built-app, measurement, and closure checkbox is complete.

## Gates / blast radius
- Keep changes scoped to structural catalogue/planner/region integration, existing Game.Structures handoff, focused tests, showcase harness/content, and this assignment metadata. Do not weaken global budgets.
- Before each substantive remaining attempt, refresh against current `origin/master` and resolve only in-scope conflicts.
- Measure bridge/castle planning time, child/primitive/voxel cost, region span, bounded memory model, and render proxy.
- Validate the exact built `WorldbuildingGalleryShowcase`, inspect durable player-height/wide frames and production-`CharacterMotor` traversal, then submit exactly one final exact-SHA request via `ci-test/fixes/agent-5`.
- Only after green exact-SHA CI and built-app gates: complete pending metadata/bookkeeping, move open -> pending, then pending -> closed, merge current master, and non-force push the exact feature head to master.