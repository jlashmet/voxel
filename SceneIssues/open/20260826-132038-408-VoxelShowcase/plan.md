# Plan — 20260826-132038-408-VoxelShowcase

## Observed defect / acceptance
The single captured pose has no annotation circles. At the visible near/far terrain handoff, the left/near grass is finely textured and dark green while the right/far grass shows pale, oversized blade/flower texture. Acceptance: replay the saved 1928×836 pose with no presentation discontinuity at that handoff.

## Competing hypotheses and evidence
- **Material identity differs.** Disproved: far vertices carry the exact application-owned surface material ID.
- **World/voxel UV scale differs.** Disproved: both paths use world metres / 0.1 m and explicit far `_VoxelSize = 0.1` still failed visually.
- **Presentation publication order differs.** Disproved: earlier publication passed its regression but the saved pose remained broken.
- **Texture-array minification differs.** Disproved: exact-source mip/trilinear regression passed, but replay run `33087477898` still showed the original handoff; the ~21 MiB mip change was reverted.
- **Post-sample material presentation differs.** Selected. Grass is authored `luminanceOnly: true` with detail/chroma/macro variation. `SmoothSurface` applies that policy; `FarTerrain` directly blended raw texture RGB and ignored `_MaterialVariation` / the luminance-only flag.

## Minimal discriminator / regression
`VoxelEngine.Tests.PlayMode.FarTerrainMaterialIdentityTests.FarTerrainHonorsLuminanceOnlyMaterialPresentation` renders the production `VoxelEngine/FarTerrain` shader offscreen with a green authored albedo and white source texture. A luminance-only row must stay strongly green; the old raw-texture path renders nearly neutral. Saved-pose replay remains the final causal rendering gate.

## Selected fix / blast radius / cost
Far terrain now applies the same base-material sequence as `SmoothSurface`: distance texture weighting, luminance/detail/chroma reconstruction, and fine/macro variation. Near-only normal relief, coatings/style overlays, and far aerial perspective remain representation-specific. Cost is one existing row lookup plus scalar/noise math; no new textures, passes, draws, allocations, meshes, or world work.

## Current state / remaining gates
Current master `3cf4487681515d24b514b6e0efabb48333aa252e` is already integrated. Candidate and framebuffer regression are pushed on `fixes/agent-5`; next: exact-SHA focused PlayMode CI, exact-SHA saved-pose replay, inspect `verification-final.png`, then only if clean complete metadata/move to closed and merge the verified head to current master.
