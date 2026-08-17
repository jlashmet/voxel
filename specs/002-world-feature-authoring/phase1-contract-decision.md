# Phase 1 Shape-Contract Decision

This note closes **WB028** from `worldbuilding-plan.md`.

## Decision

No `FeatureDefinition`, catalogue, or shape-program opcode extension is required for the Phase 1 shared structure components completed in WB010-WB027.

The existing deterministic authoring surface already covers the required geometry and composition mechanics:

- bounded integer box, cylinder, prism, capsule, ramp, rounded-box, ellipsoid, frustum, annulus, and arc-wedge emission/carving;
- bounded repeat/conditional execution and integer arithmetic;
- deterministic range draws and semantic child-seed derivation;
- transform push/pop;
- terrain sampling;
- `SetAnchor`/resolved-anchor output;
- existing slot declarations and `CallSlot` vocabulary;
- authoring-session helpers for hollow boxes, gables, crenellations, arches, stairs, and spiral stairs.

The Phase 1 component contracts are therefore an authoring-library layer over the existing engine contract. Footprints/foundations, walls, floors, openings, supported roofs, stairs/ramps/landings, towers, columns/colonnades, buttresses, battlements, vertical accents, interior volumes/connections, courtyards/open spaces, and attachment semantics can all be represented without adding authoritative opcodes.

## Deferred extension rule

Runtime slot execution is not being expanded speculatively. If a later archetype or cave task cannot be expressed safely and boundedly with the current deterministic operations, that task must identify the concrete missing operation/contract and add the smallest focused extension then. Unsupported later geometry (for example a shape family not expressible by the current primitives) remains an explicit extension hook rather than being approximated through an unrelated hidden engine path.

This preserves the project rule that the existing `FeatureDefinition -> ShapeProgram -> Primitive -> voxel` pipeline remains authoritative and is extended only when demonstrated necessary.
