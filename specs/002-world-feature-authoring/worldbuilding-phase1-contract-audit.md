# Phase 1 Structure Contract Audit

This note records the WB028 decision for the shared deterministic authoring foundation.

## Verdict

No new `FeatureCatalogue`, `ShapeProgram`, `ShapeOp`, or `PrimitiveShape` contract is required for the shared components defined by WB014-WB027.

The existing deterministic operation surface already covers the required geometry and composition:

- rectangular/foundation/floor/wall/landing volumes: `EmitBox` / `PrimitiveShape.Box`
- round towers, columns, chimneys, and similar vertical forms: `EmitCylinder`
- gable/shed/arch profiles: `EmitPrism`
- stairs and sloped pieces: existing bounded stair authoring plus `EmitRamp`
- tapered tower/spire families: `EmitFrustum`
- rounded/radial accents where needed: `EmitRoundedBox`, `EmitEllipsoid`, `EmitAnnulus`, and `EmitArcWedge`
- rooms, doorways, windows, passages, and other voids: the same primitives with `PrimitiveMode.Carve`
- repeated facade/battlement/column work: bounded `Repeat`
- optional bounded variants: `IfRange` and deterministic `DrawRange`
- component placement: `PushTransform` / `PopTransform` and `CallSlot`
- terrain adaptation: `SampleGround`
- integer derived dimensions: `Arithmetic`
- external attachment points: `SetAnchor`

`IStructureAuthoringSession` also already exposes the higher-level deterministic helpers needed by compatibility authorers (`HollowBox`, `Cylinder`, `Gable`, `Crenellate`, `Arch`, `Stairs`, `SpiralStair`, and `Carve`).

## Constraint

Future archetypes must continue compiling onto these bounded integer operations. A later task may extend the engine only if a concrete authored component cannot be represented without either duplicating geometry algorithms outside the shape pipeline or violating deterministic/bounded generation. Unsupported aesthetic families should remain explicit extension hooks rather than speculative opcodes.
