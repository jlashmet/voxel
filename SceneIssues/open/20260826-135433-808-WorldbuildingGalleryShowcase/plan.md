# Plan — 20260826-135433-808 WorldbuildingGalleryShowcase

## Evidence / acceptance
- One marked region: `screenshot-001.png`, center `(0.4802, 0.6678)`, radius `0.0690`. Human review against `reference-grass-target.jpg` requires a dense continuous pixel-art meadow, irregular layered silhouette, multiple green regions, and no repeated three-blade icons/dark bars.
- Runtime ownership traces semantic `VegetationKind.Grass` through `ProceduralVegetationBatchRenderer`. `VegetationPlacement` is authoritative for whether/what vegetation grows; its `VegetationInstance` is the semantic identity. Rendering may derive blade geometry from position/normal/seed/scale but must not reject that identity.

## Hypotheses / discriminators
1. **Generic card geometry caused the stamped silhouette — confirmed.** Grass previously shared the reusable card cluster; production routing now bypasses that path while non-grass stays unchanged.
2. **Missing dedicated deformation/presentation contract caused poor grass motion/readability — confirmed.** Packed ribbon UVs drive the supplied grass shader's camera-right reconstruction, wind, local push, and stateless recovery.
3. **Renderer-owned macro coverage is valid ecology — rejected.** `ProceduralGrassBatch` was applying world-space FBM and dropping semantic Grass below `0.20`, second-guessing `VegetationPlacement`. The new GrassLookdev regression deliberately feeds former coverage-hole placements directly into the production renderer.

## Selected fix + regression
- Commit `a04822ae92bd355e74a140fa46a248cb1e8a0cc9` removes renderer-level macro rejection. Every semantic Grass placement now renders; deterministic seed only varies local density (5–15 ribbons). World-space colour/ground-shade variation remains presentation-only.
- `GrassLookdevTests.KnownSemanticGrassInstancesReachProductionRendererEvenInFormerCoverageHoles` creates an isolated `GrassLookdev` runtime scene, bypasses terrain/ecology, and proves formerly rejected semantic placements all produce production packed grass.
- `GrassLookdevTests.SeedControlsOnlyDeterministicLocalBladeDensity` proves deterministic bounded presentation variation.
- Existing `ProceduralGrassBillboardTests` still cover spatial chunking, non-grass routing, immutable per-frame meshes, orbit readability, player displacement, and recovery.

## Blast radius / cost / gates
- Only `VegetationKind.Grass` presentation changes. Geometry remains 5–15 ribbons = 50–150 verts / 40–120 tris per semantic instance; this correction removes holes but does not raise the per-instance maximum. Meshes rebuild only on `SetInstances`; draw submission remains one draw per occupied 32 m grass chunk.
- Remaining gates: green exact-SHA targeted PlayMode CI for GrassLookdev + existing grass regressions; green exact-SHA built-application Worldbuilding Gallery harness; replay the original marked pose and inspect visual finish before pending promotion.
