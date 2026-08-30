# Plan

## Observed behavior / acceptance
- Canonical structural composition is the existing VoxelEngine path: `FeatureDefinition` + pooled `SlotSpec` + `ShapeOp.CallSlot` + `FeatureCatalogue` + `FeatureRegionBuild`. A second WorldBuilder solver would duplicate ownership.
- The branch now expands `CallSlot` declarations into deterministic bounded child `StructuralInstance`s, hashes structural generation metadata into catalogue identity, validates structural call graphs, and feeds accepted descendants back through normal per-region voxel evaluation/rasterization. Cross-region child voxel realization and same-seed graph determinism have focused regressions.
- Structural metadata is engine-neutral and preserves the existing Game.Structures decoration system as the fine-detail consumer rather than replacing it.

## Hypotheses / discriminators / results
1. **Complete the predecessor path** — supported by catalogue ownership, definition-id rebasing, bytecode, region generation, and current green-by-construction focused regressions.
2. **Add a parallel structural solver** — rejected because it would duplicate canonical generation state and create save/network determinism risk.
3. **Inline child primitives into root evaluation** — falsified by footprint/streaming boundaries; descendants must remain independently bounded physical placements.
4. **Current planner is contract-complete** — falsified by review: slot-authored reserved clearance is not enforced, repeated spacing only quantizes X, report budgeting has primitive but no explicit voxel-authoring cost, and accepted decisions do not yet expose support-loss/decor-handoff metadata.

## Selected remaining fix
- Close the generic contract gaps in the canonical planner/validator: reserved slot clearance/spacing, explicit voxel cost/budget, inspectable invalidation/decor handoff metadata, and focused required/optional/support/capacity/runtime-budget negatives.
- Add generation-order and bounded-variation regressions.
- Adapt structural decoration handoff into existing `DecorationSpace` / `DecorationSocketKind` without moving micro-detail logic into structural sockets.
- Author four deterministic showcase proving cases (bridge, castle, cliff settlement, facade/roof variants) through the same production catalogue/voxel path. Resolve and reuse the actual player-world controller serialized in `WorldbuildingGalleryShowcase` for traversal evidence.

## Gates / blast radius
- Keep changes scoped to structural catalogue/planner/region integration, existing Game.Structures handoff, focused tests, showcase harness, and assignment metadata. Do not weaken global budgets.
- Measure bridge/castle planning time, child/primitive/voxel cost, region span, bounded memory model, and render proxy.
- Validate the exact built `WorldbuildingGalleryShowcase`, inspect durable player-height/wide frames and traversal, then submit exactly one final exact-SHA request via `ci-test/fixes/agent-5`. Only after green gates: pending metadata/bookkeeping, close, merge current master, and non-force push the exact feature head to master.
