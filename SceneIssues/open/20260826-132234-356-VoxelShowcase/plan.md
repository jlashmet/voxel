# Plan

## Captured evidence
One capture, two marked Dirt/grass contacts, exact saved camera pose at 1928×836. The lower marked transition is on organic circulation; the upper mark contains a hard 90-degree grass tongue. Correct camera-ray reconstruction from the issue fixture places the upper marked envelope at approximately `X=91.0..93.8m, Z=28.6..30.4m`.

The latest exact built-player replay is workflow `33226248513` for feature source `ecec9b71119ff2dfbe250c5d8b6d7d994193e11b`. It freshly baked `ShowcaseWorld.bytes`, built `VoxelShowcase`, replayed the saved camera, and reached stable residency, but direct inspection shows the upper rectangle still present. Pixels inside both original marks are unchanged from the previously rejected replay `33225240544`, so that source is not acceptable despite green CI.

## Competing hypotheses / evidence
1. **Square organic-route stamps** — supported for the lower mark. Replacing equal-width square carve/fill stamps with equal-width cylinders changed the lower contact without moving route centres, widths, samples, placements, precedence, or the two-primitive budget. Keep this bounded fix.
2. **Route ownership of the upper mark** — falsified. An exact-seed production regression found no live organic-route placement crossing the corrected upper envelope, and route-only replay left the upper rectangle.
3. **Plot/route precedence** — falsified. Numeric `FeatureDefinition.Precedence` is validated/hashed but `FeatureCatalogueBuilder.Finalise` does not sort by it; runtime generation walks combined rule order. Lowering plot precedence produced no visible change.
4. **Generated foundation pad shrink alone** — falsified as sufficient. The green replay after shrinking the old WideHouse pad to the generated `98×86dm` foundation is byte-identical inside the marks to the rejected prior replay.
5. **The generated pad's visible Moss corner** — supported. MayorHouse is placed at `(910,250)dm` with orientation `2` inside the `132×132dm` WideHouse envelope. After the production `ShapeProgram` half-turn, the generated pad spans `X=927..1024dm, Z=286..371dm`. Its southwest top corner is therefore exactly `(92.7m,28.6m)`, on the corrected upper marked envelope. The prior regression incorrectly tested the unrotated local program and could pass while the built scene remained unchanged.
6. **Stale bake / streaming** — falsified by repeated WorldBuilder-aware forced bakes and stable real-player residency.

## Fix / behavioral regression
Keep the round organic-route stamps. For organic Kentridge **generated houses only**, preserve the exact rectangular Dirt support, elevation, clearance, placement, and three-primitive budget, but change the visible Moss ownership from a rectangular Fill to a rounded `PaintSurface` cap with a `12dm` corner radius. The support is filled through the same top voxel first, so cap exclusion exposes Dirt rather than changing occupancy.

The exact-seed PlayMode regression evaluates MayorHouse through the real `ShapeProgram` and production orientation. It asserts the world-space support remains `927..1024 × 286..371dm` at surface `Y=221dm`, while the rounded Moss cap excludes the captured corner and near-corner cells, retains interior/tangent cells, and leaves the round organic-route behavior intact.

## Blast radius / cost
Only organic Kentridge generated-house surface material ownership changes. Bespoke/non-generated pads and legacy layouts retain the existing rectangular surface. No placement, route, structure, support occupancy, elevation, or clearance changes. Each generated pad remains exactly three primitives; there is no per-frame work because VoxelShowcase consumes the prebaked world. The rounded `PaintSurface` adds bounded bake-time surface reads over the already-bounded foundation footprint while reducing unconditional Moss writes at corners.

Remaining gates: compile/run the exact regression in the final targeted request, force a fresh showcase bake, build the real `VoxelShowcase` player, replay the immutable saved pose, and directly verify that **both original circles** no longer contain metre-scale rectangular/stair-step Dirt/grass contacts. Do not promote on green CI alone.
