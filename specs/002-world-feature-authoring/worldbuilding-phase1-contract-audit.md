# Phase 1 Structure Contract Audit

This note records the WB028 decision for the shared deterministic authoring foundation.

## Verdict

No new `FeatureCatalogue`, `ShapeProgram`, `ShapeOp`, or `PrimitiveShape` contract is required for the shared components defined by WB014-WB027.

The existing deterministic operation surface already covers the required geometry and bounded program control:

- rectangular/foundation/floor/wall/landing volumes: `EmitBox` / `PrimitiveShape.Box`
- round towers, columns, chimneys, and similar vertical forms: `EmitCylinder`
- gable/shed/arch profiles: `EmitPrism`
- stairs and sloped pieces: existing bounded stair authoring plus `EmitRamp`
- tapered tower/spire families: `EmitFrustum`
- rounded/radial accents where needed: `EmitRoundedBox`, `EmitEllipsoid`, `EmitAnnulus`, and `EmitArcWedge`
- rooms, doorways, windows, passages, and other voids: the same primitives with `PrimitiveMode.Carve`
- repeated facade/battlement/column work: bounded `Repeat`
- optional bounded variants: `IfRange` and deterministic `DrawRange`
- local component transforms inside a program: `PushTransform` / `PopTransform`
- terrain adaptation: `SampleGround`
- integer derived dimensions: `Arithmetic`
- external attachment points: `SetAnchor`

`IStructureAuthoringSession` also already exposes the higher-level deterministic helpers needed by compatibility authorers (`HollowBox`, `Cylinder`, `Gable`, `Crenellate`, `Arch`, `Stairs`, `SpiralStair`, and `Carve`).

## Slot/composition boundary

`FeatureDefinition` and the catalogue already carry slot metadata, and `ShapeOp.CallSlot` exists in the opcode vocabulary, but the current `ShapeProgram` evaluator intentionally performs no work for `CallSlot`. Phase 1 must therefore **not** claim or depend on runtime slot composition.

The WB014-WB027 shared configs are definition/composition inputs that can be compiled to existing bounded shape operations or consumed by the existing deterministic structure-authoring session. That is sufficient for the current foundation and does not justify extending the engine merely to make `CallSlot` active.

If a later archetype requires catalogue-level child-definition invocation rather than authoring-time composition, that task must implement `CallSlot` deliberately, including child parameter/seed derivation, transform/orientation propagation, primitive-budget accounting, bounds validation, and tests. Until then, the no-op remains a documented inactive contract rather than hidden functionality.

## Constraint

Future archetypes must continue compiling onto these bounded integer operations. A later task may extend the engine only if a concrete authored component cannot be represented without either duplicating geometry algorithms outside the shape pipeline or violating deterministic/bounded generation. Unsupported aesthetic families should remain explicit extension hooks rather than speculative opcodes.
