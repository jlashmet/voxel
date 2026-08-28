# Plan — 20260826-135433-808 WorldbuildingGalleryShowcase

## Evidence / acceptance
- One marked region: `screenshot-001.png`, center `(0.4802, 0.6678)`, radius `0.0690`. Human review against `reference-grass-target.jpg` requires a dense continuous pixel-art meadow, irregular layered silhouette, multiple green regions, and no repeated three-blade icons/dark bars.
- `VegetationPlacement` owns whether/what grows and emits authoritative `VegetationInstance`; presentation may derive blade geometry from position/normal/seed/scale but must not reject semantic Grass.

## Hypotheses / discriminators
1. **Generic card geometry caused the stamped grass silhouette — confirmed.** Semantic Grass now bypasses the shared foliage-card mesh and uses packed ribbons.
2. **Renderer-owned macro coverage is valid ecology — rejected.** `ProceduralGrassBatch` no longer drops semantic Grass using world-space FBM; seed only varies local blade density.
3. **The packed grass path reached the built player — rejected by first final replay.** Request `f682626a06539df4bab9a65bda7052b9d7409241` passed 2/2 GrassLookdev editor tests and built/launched the real Gallery, but `player-run.log` emitted `Vegetation shader was not found: VoxelEngine/ProceduralVegetationGrass` and the original mark still showed the legacy blocky tuft. Build logs compiled the shader, proving player stripping/runtime lookup—not geometry/ecology—was the discriminator.

## Selected fix + regression
- `a04822ae92bd355e74a140fa46a248cb1e8a0cc9`: every semantic Grass placement renders 5–15 deterministic ribbons; former coverage-hole placements are covered by `GrassLookdevTests.KnownSemanticGrassInstancesReachProductionRendererEvenInFormerCoverageHoles`; seeded density determinism is covered separately.
- `a16c77109bfb95400d531cc4ef6a273cbb5a49e4`: add grass shader GUID `63dcfc6a12854b9c966a3b01d41b69c3` to `GraphicsSettings.m_AlwaysIncludedShaders`, matching the existing foliage/surface/vine retention strategy so `Shader.Find` works in standalone players.
- Existing `ProceduralGrassBillboardTests` cover spatial chunking, non-grass routing, immutable per-frame meshes, orbit readability, player displacement, and recovery.

## Blast radius / cost / gates
- Only Grass presentation/player shader retention changes. Geometry remains 5–15 ribbons = 50–150 verts / 40–120 tris per semantic instance; meshes rebuild only on `SetInstances`; draw submission remains one per occupied 32 m chunk. Always-including one dedicated shader adds build/runtime shader data but no extra draw work.
- Remaining gate: fresh exact-SHA GrassLookdev + built Worldbuilding Gallery replay after shader-retention fix; require no missing-grass-shader runtime error and inspect the original marked pose before pending promotion.
