# Plan — 20260826-135433-808 WorldbuildingGalleryShowcase

## Evidence / acceptance
- One capture/marked region: `screenshot-001.png`, center `(0.4802, 0.6678)`, radius `0.0690`. Human reopen requires `reference-grass-target.jpg`: dense continuous stylized pixel meadow, irregular layered silhouettes, multiple green regions, no repeated three-blade/dark-bar icons, plus local player bend/recovery.
- Exact-SHA replay `33209185232` / job `98977900917` at source `8f6d7b3f...` was green but visually failed: direct inspection of the pinned Gallery capture showed the marked center still occupied by a dark upright multi-card tuft. The same run proved shader retention/startup and the packed Grass bend/recovery path were working, so the surviving mark is a non-Grass presentation owner.

## Hypotheses / discriminator
1. **Renderer-owned macro coverage created Grass holes — confirmed/fixed, not sufficient.** Semantic Grass is no longer rejected; seed only varies 5–15 local ribbons.
2. **Standalone shader stripping caused the bad mark — confirmed/fixed, not sufficient.** Always-Included retention removed the player shader failure; the marked tuft survived.
3. **Legacy Shape-0 billboard was the surviving icon — rejected as sufficient by replay `33209185232`.** Shape `0.75` removed the old shader branch, but the generic source Tuft mesh still builds seven upright crossed cards and reproduced the same visual failure.
4. **Ordinary meadow Nettle owns the surviving seven-card Tuft — confirmed by placement/profile/rendering discrimination.** `SelectMeadow` can emit Nettle and, among non-Grass meadow kinds, Nettle alone resolves to `GrowthForm.Tuft`; generic `BuildFoliage(Tuft)` emits seven upright crossed cards. `WaterGrass` is also Tuft but comes from the aquatic selector, so blanket-routing Tuft would be incorrect.

## Selected fix / regression
- Keep existing packed semantic Grass renderer and standalone shader retention.
- `dce9c70f...`: route only `Grass` + ordinary meadow `Nettle` through `ProceduralGrassBatch`; preserve vegetation kind/profile scaling and leave aquatic/flowering/branching/surface-cover accents on their authored generic paths.
- `5e1baa35...`: `OrdinaryMeadowNettleUsesPackedGrassRendererWhileAquaticTuftsStayGeneric` locks the actual renderer boundary: Nettle produces packed ribbons, WaterGrass does not, and semantic Grass remains packed. Existing coverage-hole and production-shader push/recovery tests remain the other two final gates.

## Blast radius / cost / gate
- No ecology, placement density, vegetation-kind, shader-retention, or scene-data change. Only Nettle presentation ownership changes. WaterGrass/Reed/Cattail/flowers/Fern/Clover and other accents remain generic.
- Nettle replaces one existing seven-card Tuft source mesh with the existing 5–15-ribbon packed-grass path. It adds no renderer/material type or per-instance draw; it reuses 32 m grass chunks and existing mesh caps. Cost increase is bounded to Nettle placements, whose meadow selection weight is 0.14.
- Feature already merged master through `d45e59ea...`; master may advance again and will be merged only after validation as required.
- Final gate: move this issue to `pending`, then make one exact-SHA PlayMode request on `ci-test/fixes/agent-1` for `KnownSemanticGrassInstancesReachProductionRendererEvenInFormerCoverageHoles`, `OrdinaryMeadowNettleUsesPackedGrassRendererWhileAquaticTuftsStayGeneric`, and `GrassShaderStaysReadableAcrossOrbitPushesLocallyAndRecoversAtFixedTime`, plus a 30 s built `WorldbuildingGalleryShowcase` replay. Require green `ci/single-test`, no runtime shader/startup exception, and direct inspection of the marked pose showing no repeated dark upright card icon before promotion.
