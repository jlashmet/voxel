# Experiment 040 — empty presentation projection is not a box

## Concrete source discriminator
The canonical game material catalogue explicitly assigns `(1,0,1)` to its Empty row (0). Earlier notes calling every pink pixel a Unity error-shader pixel were not a proven attribution.

`WorldRoadNetworkVoxelCatalogue` lowers each bounded road piece to `EmitTerrainCorridor`. The resulting `PrimitiveMode.TerrainCorridor` is not Fill or FillIfEmpty. `FarFeaturePresentationAdapter.GeometryFor` therefore returns null for this operation-only bake; `MaterialFor` returns 0. Query nevertheless publishes the instance. `ProceduralFarFeatureRenderer.GetMesh` interprets missing geometry as a solid fallback box, scaled to the operation's full bounds, and GetMaterial reads row 0. Carve-only and paint/detail-only bakes have the same missing-geometry path. The adapter also chooses StyleKey from a different primitive predicate than its geometry/material.

This establishes a specific code-path defect and a reproducible input, not yet attribution of every pink/gray pixel in the production capture. The queued diagnostic must still identify the actual rendered owner before another product correction. Do not recolor Empty, replace shaders again, disable far rendering globally, or turn terrain-operation bounds into purported road geometry.

## Minimal production-code regression
`Composition/Tests/EditMode/FarFeatureEmptyProjectionTests` calls the actual production adapter with one operation-only bake. Five mode cases must return no render instance, two positive fill controls must preserve geometry/material/bounds, and four mixed cases require style to come from the same projected positive primitive as material. The current source is expected to fail the nine negative/mixed cases; these C# tests have NOT been executed locally or in CI yet. No duplicate Python model is substituted for execution.

The owning Composition projection is pure metadata/math and requires no scene-specific writer or renderer, so its focused invariant belongs in its headless EditMode assembly. The existing production FarWorld validation scene and exact VoxelShowcase captures remain mandatory for any subsequent presentation correction.

## Exact request remains immutable
Source `affc45d54e08362ed6c7515a537bfb386eca4590`, request `019f5562d8b9d2575de0024d71ccbdb55dca028f`, run `34006671692` is still queued at the latest check. This regression, the per-scenario evidence preservation, and normal-baker manifest emission are later feature work, not part of that request. Do not replace it. The product adapter is deliberately unchanged pending its draw-owner evidence.
