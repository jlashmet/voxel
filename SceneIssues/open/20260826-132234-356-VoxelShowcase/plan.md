# Plan

## Captured evidence
One capture, two marked Dirt/grass contacts, exact saved camera pose at 1928×836. Direct inspection of the latest real-player artifact from workflow `33233041050` still shows both failures: the upper contact is stair-stepped and the lower/right contact contains a metre-scale axis-aligned Dirt/grass corner.

Correct camera reconstruction places useful authored-surface probes near `(X≈922,Z≈295)` and `(X≈957,Z≈306)` voxels around `Y≈220` for seed `0x5EED1234`. These are probes, not assumed owners.

## Minimal reproduction after repeated failed fixes
Experiment 021 compares retained forced-bake/player artifacts from source variants that materially changed organic route stamps and generated plot caps. Square routes (`636f6120…`), cylinder routes (`57006268…`), precedence/plot changes (`36eb66c5…`), the 1.2m rounded cap (`ed933466…`), and the stadium cap (`29786792…`) are byte-identical on rendered ground in both original circles. The stadium-vs-rounded diff changes only sky/background pixels.

This corrects earlier visual interpretations that the lower circle improved or that changing MayorHouse cap ownership moved the upper contact. Those claims are falsified by full-resolution pixel comparison.

## Competing hypotheses / evidence
1. **Square organic-route stamps own the lower mark** — falsified. Source `636f6120…` demonstrably emits square route boxes, current source emits cylinders, but exact built-player ground/circle pixels are identical.
2. **Organic-route placement owns the upper mark** — falsified by exact-seed overlap checks and the artifact comparison above.
3. **Generated-house plot cap owns the upper mark** — falsified as the rendered owner. Rectangular, 1.2m rounded, and stadium cap variants produce identical ground pixels in the mark.
4. **Plot/route numeric precedence** — falsified; generation follows combined rule order rather than numeric precedence sorting.
5. **Stale bake / streaming** — falsified. Replays force fresh `ShowcaseWorld.bytes` bakes and reach stable residency for all 199 generated regions.
6. **Base terrain high/low material contour** — falsified. Canonical `GameTerrainMaterials` maps both low and high terrain surfaces to Grass, so generic terrain cannot create this Dirt/grass seam.
7. **Wrong regression seam / later combined writer** — supported. `VoxelShowcase` consumes `KentridgeCombinedVoxelCatalogueCanonical`; plot surfaces enter through `KentridgeVerticalPlacementAdapter.BuildPlotSurfaces(...)` and later catalogues can overwrite the same columns. Existing tests only inspect isolated analytic primitives.

## Next behavioral regression
Before another production geometry edit, add a combined-world ownership regression for the exact showcase seed. It must evaluate/rasterize the ordered production Kentridge catalogue at the captured probe columns and assert the **final stored voxel/material ownership**, identifying which late primitive actually wins at each Dirt/grass contact. The test should fail on the current visual behavior for a reason tied to the marked regions, not merely assert one catalogue's local bounds.

Only after that final owner is proven should production geometry/material code change.

## Blast radius / cost guardrails
Do not broaden terrain or renderer behavior. Target only the proven Kentridge writer(s) responsible for the two captured contacts. Preserve settlement placement, building structural support, walkable circulation, and bounded bake cost; no per-frame work should be introduced because `VoxelShowcase` consumes the prebaked world.

## Remaining gates
1. Combined-world behavioral regression proves both captured owners and fails before the fix.
2. Implement the smallest owner-level fix and check blast radius/cost.
3. Run the final targeted exact-SHA PlayMode request on the persistent `ci-test/fixes/agent-8` transport only.
4. Require a non-cancelled green workflow, forced fresh showcase bake, real-player build, immutable camera replay, and direct full-resolution verification that **both original circles** contain no metre-scale rectangular/stair-step Dirt/grass contacts.
5. Only then complete pending metadata, close the issue, merge current `origin/master`, and non-force promote the exact feature head.
