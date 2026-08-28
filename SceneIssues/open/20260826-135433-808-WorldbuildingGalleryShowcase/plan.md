# Plan — 20260826-135433-808 WorldbuildingGalleryShowcase

## Evidence
- One marked region only: `screenshot-001.png`, center `(0.4802, 0.6678)`, radius `0.0690` (~pixel `(926,558)` at 1928×836). Human review requires the supplied `reference-grass-target.jpg`: a dense continuous pixel-art meadow, irregular layered silhouette, multiple green/noise regions, accent foliage, and no repeated three-blade icons/dark bars.
- Runtime ownership traces semantic `VegetationKind.Grass` through `ProceduralVegetationBatchRenderer`; the old generic tuft source is seven rectangular cards, matching the repeated stamped silhouette in the capture. The supplied `grass-renderer-reference.shader` defines the accepted packed root/lateral/height/phase contract, coherent wave constants, camera-right reconstruction, local push, and stateless recovery.

## Competing hypotheses / discriminator
1. **Generic card geometry is causal — confirmed.** Grass shared the same reusable card cluster as other tuft foliage. Falsifier: if semantic Grass bypassed that mesh while the captured silhouette remained; the production route proves it did not.
2. **Missing grass deformation/presentation contract is causal — confirmed.** The old path did not consume the supplied packed channels or reference wind/push equations. Standard characters already publish live transforms through `GrassInteractorRegistry`; when no registered character exists, lightweight camera-player showcases use the main camera as the local fallback.
3. **Vegetation placement/all foliage is wrong — rejected for this mark.** Flowers, reeds, dead grass, shrubs, vines, etc. are outside the marked defect and remain on their existing renderer; the regression asserts that separation.

## Fix + behavioral regression
- Engine-wide semantic Grass now builds deterministic tapered ribbons into 32 m spatial chunks when `SetInstances` changes. Coverage, colour, and ground shade use independent world-space FBM fields; UV0–UV3 match the supplied shader exactly. Per-frame wind, camera-facing reconstruction, local interactor push, and recovery stay GPU-only in `VoxelEngine/ProceduralVegetationGrass`.
- `ProceduralGrassBillboardTests.SemanticGrassUsesPackedSpatialChunksAndLeavesOtherVegetationOnExistingPath` exercises the real renderer, deterministic packed topology/fields, unaffected non-grass routing, and no per-frame mesh mutation.
- `ProceduralGrassBillboardTests.GrassShaderStaysReadableAcrossOrbitPushesLocallyAndRecoversAtFixedTime` renders the production shader through a 90° orbit and verifies local displacement plus fixed-time recovery.

## Blast radius / cost
- Only `VegetationKind.Grass` changes presentation. Old grass used 28 verts/14 tris per semantic instance; new density is 5–15 ribbons = 50–150 verts/40–120 tris (~1.8–5.4× verts, ~2.9–8.6× tris; ~100/80 at 10 blades). Draw submission becomes one draw per occupied 32 m grass chunk; non-grass instancing is unchanged. Meshes rebuild only on `SetInstances`, never per frame. This is a known rendering-budget tradeoff against the existing 6/7/9 ms PC/console/mobile voxel-render targets and requires exact-SHA CI plus original-pose replay before promotion.
