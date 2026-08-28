# Plan — 20260826-135433-808 WorldbuildingGalleryShowcase

## Evidence / acceptance
- One capture/marked region: `screenshot-001.png`, center `(0.4802, 0.6678)`, radius `0.0690`. Human reopen requires the supplied `reference-grass-target.jpg`: dense continuous stylized pixel meadow, irregular layered silhouettes, multiple green regions, no repeated three-blade/dark-bar icons, plus local player bend and recovery.
- Runtime evidence separated owners: a standalone replay first failed because `VoxelEngine/ProceduralVegetationGrass` was stripped; after shader retention, the same saved pose rendered but the marked blocky tuft survived. `VegetationPlacement.Default` emits semantic Grass plus ordinary tuft/aquatic accents; only Grass enters `ProceduralGrassBatch`.

## Hypotheses / discriminator
1. **Renderer-owned macro coverage creates holes — confirmed/fixed.** Semantic Grass is never rejected in presentation; seed varies only 5–15 local ribbons.
2. **Standalone shader stripping explains the bad mark — partially confirmed/fixed, but not sufficient.** Always-Included retention removed the runtime error; replay still showed the icon.
3. **A non-Grass accent still uses the legacy Shape-0 three-blade billboard — confirmed.** `ShapeFor` routed ordinary tuft/aquatic growth forms through Shape 0, which reconstructs exactly the obsolete three-rooted-blade sprite. Moving those kinds to their authored multi-card geometry is the smallest owner-level fix.

## Selected fix / regression
- `a04822ae...`: packed semantic Grass renderer; 5–15 deterministic ribbons per placement with regional color fields.
- `a16c7710...`: retain the dedicated grass shader in standalone players.
- `5e2bcd1f...`: ordinary tuft/aquatic accents use shape `0.75`, preserving kind/batching while bypassing legacy Shape 0. Grass remains dedicated shape `5`.
- `b3007d87...`: `OrdinaryMeadowTuftsDoNotUseLegacyThreeBladeSpriteShape` locks the surviving-owner fix. `KnownSemanticGrassInstancesReachProductionRendererEvenInFormerCoverageHoles` covers placement ownership; `GrassShaderStaysReadableAcrossOrbitPushesLocallyAndRecoversAtFixedTime` renders the production shader and proves local push/recovery.

## Blast radius / cost / gate
- No ecology/density change and no added accent instances/draws/materials. Grass stays 50–150 verts / 40–120 tris per semantic instance, one draw per occupied 32 m chunk; accent tufts reuse existing multi-card source geometry.
- Current source includes merge `d61f3a05...` from `master` `3bca7e01...`; merge-base comparison found no overlapping changed paths.
- Final gate: one exact-SHA PlayMode request for the three focused regressions above plus 30 s built `WorldbuildingGalleryShowcase` scene-issue replay. Require green `ci/single-test`, no runtime shader/startup exception, and direct inspection of the original marked pose before promotion.
