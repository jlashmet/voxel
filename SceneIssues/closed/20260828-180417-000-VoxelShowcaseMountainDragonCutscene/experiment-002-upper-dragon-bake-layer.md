# Experiment 002 — upper dragon bake layer

**Hypothesis.** Run `33298125653` failed because the offline startup bake materialises terrain surface layers only. The fixed-altitude 60-voxel dragon placeholder starts near world Y 500 and crosses the 512-voxel region boundary, so its Y=1 region is never resident/captured even though runtime streaming later generates it.

**Discriminator.** The same run completed the production 17-waypoint replay in 57.4 s and visibly rendered the supported placeholder/dialogue after streaming, while focused startup-bake acceptance read air at dragon centre `(-1112,530,200)`. `CaptureBake` already serialises every resident region, and `GenerateRegionBlocking` immediately runs production `FeatureRegionBuild` for requested regions.

**Action / source.** On the post-run feature branch, add a generic bake-only planner for explicit `Structure + FixedAltitude` footprints inside the startup disc, materialise those regions after the terrain disc, and add a focused regression requiring exactly the dragon placeholder's lower/upper layers rather than the mountain's sky/headroom footprint.

**Verdict.** Selected. This changes only offline bake residency. It does not alter mountain geometry, voxel rasterisation semantics, runtime streaming, movement, gallery baking, or the evidence route.

**Next gate.** Exact-parent final PlayMode request must keep the fresh bake under 240 s / 14 GB, reopen Unity, pass Mountain Dragon final acceptance, and repeat the complete built-player replay/captures.