# Plan — 20260826-135433-808 WorldbuildingGalleryShowcase

## Evidence / acceptance
- One capture/marked region: `screenshot-001.png`, center `(0.4802, 0.6678)`, radius `0.0690`. Human reopen requires `reference-grass-target.jpg`: dense continuous stylized pixel meadow, irregular layered silhouettes, multiple green regions, no repeated three-blade/dark-bar icons, plus local player bend/recovery.
- Exact-SHA replay `33209185232` / job `98977900917` at source `8f6d7b3f...` was green but visually failed: direct inspection of the pinned Gallery capture showed the marked center still occupied by a dark upright multi-card tuft. Shader retention/startup and packed Grass bend/recovery were working, isolating a non-Grass presentation owner.
- Corrected replay `33212988821` / job `98990282719` at source `79638262...` reproduced the same marked dark card cluster, while `OrdinaryMeadowNettleUsesPackedGrassRendererWhileAquaticTuftsStayGeneric` failed with Nettle packed blade count `0`. Production `IsGrass` still classified only semantic Grass, proving the intended Nettle owner fix had not actually been implemented.

## Hypotheses / discriminator
1. **Renderer-owned macro coverage created Grass holes — confirmed/fixed, not sufficient.** Semantic Grass is never rejected; seed varies only 5–15 local ribbons.
2. **Standalone shader stripping caused the bad mark — confirmed/fixed, not sufficient.** Always-Included retention removed the player shader failure; the marked tuft survived.
3. **Legacy Shape-0 billboard was the surviving icon — rejected as sufficient by replay `33209185232`.** Shape `0.75` bypassed the old shader branch, but generic `BuildFoliage(Tuft)` still builds seven upright crossed cards.
4. **Ordinary meadow Nettle owns the surviving generic Tuft — confirmed.** `SelectMeadow` can emit Nettle and, among non-Grass meadow kinds, Nettle alone resolves to `GrowthForm.Tuft`; the failed `33212988821` boundary regression independently proved it was still generic. `WaterGrass` is also Tuft but comes from the aquatic selector, so blanket-routing Tuft would be incorrect.

## Selected fix / regression
- Keep the existing packed semantic Grass renderer and standalone shader retention.
- `9a6e2b93...`: classify only `Grass` + ordinary meadow `Nettle` for `ProceduralGrassBatch`; preserve vegetation kind/scale and leave aquatic/flowering/branching/surface-cover accents on their authored generic paths.
- `5e1baa35...`: `OrdinaryMeadowNettleUsesPackedGrassRendererWhileAquaticTuftsStayGeneric` locks that production boundary: Nettle produces packed ribbons, WaterGrass does not, and semantic Grass remains packed. `KnownSemanticGrassInstancesReachProductionRendererEvenInFormerCoverageHoles` and `GrassShaderStaysReadableAcrossOrbitPushesLocallyAndRecoversAtFixedTime` remain the coverage and player-interaction gates.

## Blast radius / cost / gate
- No ecology, placement density, vegetation-kind, shader-retention, or scene-data change. Only Nettle presentation ownership changes. WaterGrass/Reed/Cattail/flowers/Fern/Clover and other accents remain generic.
- Nettle replaces one existing seven-card Tuft presentation with the existing 5–15-ribbon packed-grass path. No new renderer/material or per-instance draw is introduced; it reuses 32 m grass chunks. Cost increase is bounded to Nettle placements (meadow selection weight 0.14).
- Feature already merged master through `8190e920...` (master parent `d39f1ad7...`); current `origin/master` will be merged again only after validation as required.
- Final gate: one exact-SHA PlayMode request on `ci-test/fixes/agent-1` for the three focused regressions above plus a 30 s built pending `WorldbuildingGalleryShowcase` replay. Require green `ci/single-test`, no runtime shader/startup exception, and direct inspection of the marked pose showing no repeated dark upright card icon before promotion.