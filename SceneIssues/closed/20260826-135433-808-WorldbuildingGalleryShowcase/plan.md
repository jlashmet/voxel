# Plan — 20260826-135433-808 WorldbuildingGalleryShowcase

## Evidence / marked region
- One marked region: `screenshot-001.png`, center `(0.4802, 0.6678)`, radius `0.0690`. Human acceptance is `reference-grass-target.jpg`: dense continuous stylized pixel meadow with irregular layered silhouettes/multiple green regions, no repeated three-blade or dark-card icons, plus local player bend/recovery.
- Replay `33209185232` / job `98977900917` was green but visually failed: the pinned marked region still contained a dark upright multi-card tuft. Shader retention/startup and packed-Grass bend/recovery were working, isolating a non-Grass presentation owner.
- Replay `33212988821` / job `98990282719` reproduced the same mark while `OrdinaryMeadowNettleUsesPackedGrassRendererWhileAquaticTuftsStayGeneric` failed with Nettle packed blade count `0`, proving ordinary Nettle still fell through the generic path.

## Competing hypotheses / discriminators
1. **Renderer-owned macro coverage created Grass holes — fixed but insufficient.** Semantic Grass is no longer rejected; the marked icon survived.
2. **Standalone shader stripping caused the bad mark — fixed but insufficient.** Retention removed the shader failure; the marked tuft survived.
3. **Legacy Shape-0 billboard was the surviving icon — rejected as sufficient.** Shape `0.75` bypassed that branch, but generic `BuildFoliage(Tuft)` still builds seven upright crossed cards.
4. **Ordinary meadow Nettle owns the surviving generic Tuft — confirmed.** `SelectMeadow` emits Nettle and Nettle resolves to `GrowthForm.Tuft`; the failing boundary regression independently proved it remained generic. `WaterGrass` is also Tuft but is aquatic, so blanket-routing Tuft would be wrong.

## Fix / regression
- `9a6e2b93969c807bd7175d7ff231c9e274311814`: route only semantic `Grass` plus ordinary meadow `Nettle` through `ProceduralGrassBatch`; preserve `WaterGrass` and other accents on generic rendering.
- `OrdinaryMeadowNettleUsesPackedGrassRendererWhileAquaticTuftsStayGeneric` locks that boundary. Existing `KnownSemanticGrassInstancesReachProductionRendererEvenInFormerCoverageHoles` and `GrassShaderStaysReadableAcrossOrbitPushesLocallyAndRecoversAtFixedTime` retain coverage and player bend/recovery gates.

## Blast radius / cost
- No ecology, placement-density, vegetation-kind, scene-data, or global grass-density change. Only Nettle presentation ownership changes; WaterGrass/Reed/Cattail/flowers/Fern/Clover and other accents remain generic.
- Nettle replaces one existing seven-card Tuft presentation with the existing 5–15-ribbon packed path. No renderer/material or per-instance draw is added; cost increase is bounded to Nettle placements (meadow selection weight `0.14`) in existing 32 m grass chunks.

## Final verification
- Runtime verification at the captured pose shows the marked near-camera meadow without the prior dark upright card cluster while preserving authored aquatic accents; the existing player-interaction regression remains green.
- Exact source SHA `a3b0c60880dcc0731c6b9f900c13d9a72e51d91c` was tested by sole-child transport `0a4e06f8f40d143bc2197cb1addb6ad0fd9d18b4`. Targeted workflow run `33214102360`, job `98993762977`, completed `success`, including requested test and real-player visual capture steps.
