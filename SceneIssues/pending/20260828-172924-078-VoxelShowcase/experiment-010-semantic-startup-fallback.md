# Experiment 010 — semantic startup fallback

## Hypothesis
The viewport-scale incorrect grass shelf is caused by the eight-vertex startup fallback itself, not by missing authored water. Replacing that flat generic proxy with a coarse mesh that samples the same analytic height, authored lowered surface/material metadata, and far-structure overrides as authoritative clipmap publication should preserve cold-start coverage without masking water.

## Production change
`VoxelFarTerrain.BuildStartupFallback` now builds nested coarse annular bands for unresolved rings. Each band samples at 4x the authoritative ring spacing, keeps the exact published inner boundary, follows camera recentering, and reuses scratch lists. `SampleStartupFallbackVertex` uses the same `TerrainSampler.HeightAt`, `FarFieldStructureStore.AuthoredTerrainHeightAt`, authored material, positive structure override, and `ResolveFarSurfaceMaterial` contract as published far terrain.

## Behavioral regression
`StartupFallbackPreservesAuthoredWaterHeightAndMaterial` seeds a 51.2 m authored-water region immediately outside the issue camera's synchronous ring-zero north edge, runs the production fallback path, and asserts that an actual fallback vertex uses the lowered water height and water albedo. Existing startup coverage, recenter, and handoff tests remain in place; the former eight-triangle assertion is replaced by a bounded semantic-mesh budget.

## Blast radius / cost
The change is isolated to startup fallback generation in `VoxelFarTerrain`; authoritative async rings, near/far handoff, storage capture, and normal steady-state rendering are unchanged. At the issue camera with the production 96-sample / 5-ring layout, the fallback builds about 2,190 vertices and 10,704 triangle indices, versus 37,636 height samples for synchronously building all four unresolved 97x97 outer grids. The fallback disappears when the outermost authoritative ring publishes, so there is no steady-state sampling cost.

## Gate
Do not promote on regression alone. Final exact-SHA CI must replay `VoxelShowcase` for 60 seconds and the captured frame near the original ~22 s camera must show water/terrain rather than a viewport-scale flat green shelf in all five marked regions.
