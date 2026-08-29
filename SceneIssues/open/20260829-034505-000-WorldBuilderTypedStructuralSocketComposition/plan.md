# Plan

## Observed behavior / acceptance
- The canonical VoxelEngine structure catalogue already owns `FeatureDefinition.SlotOffset/SlotCount`, `SlotSpec`, `ShapeOp.CallSlot`, and a packed `Slots` pool. `ShapeProgram.Run` reaches `CallSlot` but does nothing, so production structural composition is an active no-op.
- Legacy `SlotSpec` only names one child definition plus a local box/count/spacing. It cannot express typed compatibility, facing, support, required/optional semantics, or rejection diagnostics.
- `FeatureCatalogueBuilder.ComputeHash` still omits slots although `FeatureCatalogue.Hash` is world identity. `FeatureCatalogueComposer` rebases slot child definition ids, confirming slots are generation data and must participate in identity.
- Current `FeatureRegionBuild` enumerates only top-level explicit placements and enforces each evaluated definition's primitives stay inside that definition footprint. Composed children therefore must be deterministic physical instances discovered before normal region rasterization; inlining child primitives is invalid for multi-region structures.
- Master-added `StructureCompositionConfigs` / `StructureComponent*` APIs provide authoring foundations and external attachment anchors, but no child graph/solver. `ShapeProgramComposition` is bytecode translation only. A recursive repository-tree audit found no active path named `DecorationSocket*`; decoration compatibility must preserve the existing fine-detail/attachment APIs under their actual names rather than create a second socket hierarchy.

## Hypotheses / material results
1. **Complete the predecessor** — supported. Existing catalogue ownership, rebasing, bytecode and region generation provide the canonical seam.
2. **Add a separate WorldBuilder structural solver** — rejected; it would duplicate the existing mechanism.
3. **Inline child primitives during `CallSlot`** — falsified by the footprint/streaming backstop.

## Selected fix
- Generalize existing slots into typed structural sockets with stable ids, role/tags, cardinal transform, clearance, capacity, support/required/invalidation and decoration-handoff metadata.
- Add one bounded deterministic Structures-runtime planner that interprets `CallSlot` into child physical instances and an inspectable accepted/rejected attachment graph. Preserve per-piece footprint/primitive budgets and make region discovery include descendants overlapping the requested region.
- Hash all generation-affecting slot data. Reuse existing component authoring/material/seed/cardinal helpers; do not create a parallel composition API.

## Remaining gates
- Finish fine-detail/decoration handoff inventory and implement slot contract, graph expansion, region realization and regressions.
- Build bridge/castle/cliff/detail showcase proofs, traversal and negative/budget tests; measure blast radius/cost.
- Validate exact built `WorldbuildingGalleryShowcase`, inspect durable frames, then submit exactly one final exact-SHA CI request and complete pending/closed/master workflow.
