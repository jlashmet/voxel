# Plan

## Observed behavior / acceptance
- The canonical VoxelEngine structure catalogue already owns `FeatureDefinition.SlotOffset/SlotCount`, `SlotSpec`, `ShapeOp.CallSlot`, and a packed `Slots` pool. `ShapeProgram.Run` reaches `CallSlot` but does nothing, so production structural composition is an active no-op.
- Legacy `SlotSpec` only names one child definition plus a local box/count/spacing. It cannot express typed compatibility, facing, support, required/optional semantics, or rejection diagnostics.
- `FeatureCatalogueBuilder.ComputeHash` omits slots even though `FeatureCatalogue.Hash` is world identity; enabling composition without fixing this would permit deterministic-content desync.
- `FeatureRegionBuild` enforces every primitive lies inside its own definition footprint. Therefore composed children must remain independent bounded `FeatureInstance`s; flattening bridge/castle children into the root program is invalid and would break streaming.
- Existing per-definition caps remain unchanged (`512` primitives, `1280`-voxel footprint axis). Decoration remains a separate fine-prop concern.

## Hypotheses / material results
1. **Complete the predecessor** — supported. Existing catalogue ownership, rebasing, bytecode and region generation provide the correct canonical seam.
2. **Add a separate WorldBuilder structural solver** — rejected unless a later blocker proves unavoidable; it would duplicate the existing mechanism and violate the feature contract.
3. **Inline child primitives during `CallSlot`** — falsified by the region-build footprint backstop and multi-region requirement.

## Selected fix
- Generalize existing slot metadata into typed structural sockets with stable ids, semantic compatibility, cardinal facing, integer clearance, capacity, support/required flags, invalidation/decor handoff metadata, and bounded candidate selection.
- Make `CallSlot` resolve deterministic child feature instances/attachment edges, not inline geometry. Recursively compose through one bounded planner owned by the existing Structures runtime, with cycle/depth/count/cost/extent failure and inspectable accepted/rejected graph/hash.
- Region discovery must consider composed roots whose bounded descendants overlap the target region, then normal `FeatureRegionBuild` evaluates/rasterizes each child through authoritative voxel primitives.
- Hash all generation-affecting slot/composition data into catalogue identity.

## Remaining gates
- Finish decoration/validation/test inventory; implement contract, graph expansion, region realization and regressions.
- Build bridge/castle/cliff/detail showcase cases, traversal and negative proofs; measure cost/blast radius.
- Validate exact built `WorldbuildingGalleryShowcase`, inspect all durable frames, then submit exactly one final exact-SHA CI request and complete pending/closed/master workflow.
