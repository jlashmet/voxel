# Plan

## Observed behavior / acceptance
- The canonical VoxelEngine structure catalogue already owns structural composition metadata: `FeatureDefinition.SlotOffset/SlotCount`, `SlotSpec`, `ShapeOp.CallSlot`, and `ShapeOps.RegisterSlot`.
- Runtime evidence falsifies the previous inventory: `ShapeProgram.Run` handles `CallSlot` with an empty `break`, so structural child composition is a compiled production no-op.
- `FeatureCatalogueComposer` rebases slot definition ids, but `FeatureCatalogueBuilder.ComputeHash` currently omits the slot pool even though slot data changes world geometry.
- Fine prop placement remains a separate decoration concern; this feature must not replace it.
- Required proving cases are bridge, castle, cliff/vertical chain, and meso facade/roof attachment, all through authoritative voxel geometry and built `WorldbuildingGalleryShowcase` evidence.

## Competing hypotheses / discriminator
1. **Complete the predecessor**: the existing slot bytecode/catalogue path is intended to be canonical and can be extended with typed socket metadata, bounded deterministic selection, diagnostics, and child evaluation. This is supported by current API ownership, catalogue rebasing, and the explicit `CallSlot` opcode.
2. **Supersede it with a new WorldBuilder solver**: this would only be justified if the predecessor cannot express independently bounded children or deterministic constraints without violating region-local generation. The current code has not established that; adding it now would create the competing mechanism the issue forbids.

Next discriminator: inspect `FeatureGeneration`, `FeatureRegionBuild`, slot validation/tests, and decoration placement to locate the correct composition boundary and ensure children can remain independent authoritative feature instances rather than being flattened into one giant footprint.

## Selected direction
- Extend the existing VoxelEngine structural slot contract with data-driven semantic compatibility, facing/clearance/capacity/support/required metadata and deterministic stable identity.
- Implement one bounded production composition path rooted in existing `CallSlot`/slot metadata; route child evaluation/placement through normal feature primitives/voxel generation.
- Include every generation-affecting slot field in catalogue/world identity hashing.
- Produce inspectable deterministic attachment diagnostics/graph and fail closed for required, unsupported, incompatible, cyclic, or over-budget composition.
- Add only the smallest WorldBuilder showcase/semantic adapter needed to request and display composed structures.

## Remaining gates
- Update checklist from predecessor/decoration/runtime audit; implement and add focused behavioral regressions.
- Prove all four cases, traversal, negative constraints, determinism, authoritative voxel output, blast radius and bounded cost.
- Validate exact built scene and inspect all durable frames.
- Submit exactly one final exact-SHA targeted CI request, then perform pending/closed metadata and master merge workflow.
