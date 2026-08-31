# Tasks

## Workflow / architecture
- [x] Read `AGENTS.md`, `SceneIssues/README.md`, and `SceneIssues/feature-readme.md`; keep separate plan/tasks.
- [x] Keep water authoring in canonical `ShowcaseWorld`/Storage and rendering in the shared renderer; no bespoke proof material/mesh path.
- [x] Keep shared material APIs semantic/config-driven and scene/game material IDs in composition.
- [x] Preserve existing storage/streaming/edit/diagnostic/gameplay semantics.

## Shared implementation / regression coverage
- [x] Add reusable still, flowing/river, and waterfall presentation profiles in `Hidden/VoxelEngine/WaterSurface`.
- [x] Adapt shallow/deep color, foam/contact, animated detail, reflection/refraction and profile-specific motion through shared configuration.
- [x] Preserve per-vertex water identity and drive solid classification from the installed semantic water mask rather than hard-coded game IDs.
- [x] Add reusable lip/base/edge topology and `WaterSprayFlag` through canonical extraction/cache/GPU arena.
- [x] Keep spray in the canonical water mesh/material, with a selective `ZWrite Off` second pass only for spray-containing entries.
- [x] Replace same-span spray fan with three tapered sheets with distinct impact footprints.
- [x] Replace shallow same-span showcase ribbons with four overlapping connected Cascade bands; scene owns composition only.
- [x] Punch true low-coverage holes only in vertical waterfall body fragments so alpha breakup cannot still depth-stamp.
- [x] Lower authored Cascade feet into the receiving-water contact band.
- [x] Use existing semantic `WaterEdgeFlag` to erode only waterfall outer silhouette; leave still/river unchanged.
- [x] Add independent edge/body/extraction/cache/arena/spray production-path regressions.
- [x] Fix `ExactCascadeCurtainImpactsBesideReceivingWaterAndSurvivesProductionCache` timing false-negative by replacing the fixed 120-yield race with the existing nonblocking two-second wall-clock cache policy (`e82176c81508464c590183902009706fb4d800d7`).
- [x] Add `WaterSprayFeatheringRegressionTests.SprayPassKeepsImpactHingeTransparentWhileFreeMistRemainsVisible` as a focused raster discriminator for the surviving hard planar base-spray defect (`349e50e23cd3d6da213b696f13add2a7a6a21b9d`, meta `5c7ec8bdb96796bec7839f240be63609060ca0aa`).
- [x] Isolate current spray defect before another fix: canonical trapezoids are broadest at the impact hinge, while the spray shader begins visibility at `v=0.015`; exact red run `33402041555` reports 302 lit pixels in the first ~7% above that hinge. This makes the broad planar carrier visible where impact foam should own contact.
- [x] Fix only the isolated raster cause: keep canonical geometry/cardinality/depth behavior and move free-mist rise feathering to `smoothstep(0.12, 0.32, sprayUv.y)` so the shared planar hinge is fully transparent while mist remains above it (`ec95915d91c803fc8eaa1e35031456b63d7fdeb9`).

## Higher-band spray wedge root-cause isolation
- [x] Isolate the post-hinge symptom before another visual change: production carriers retain roughly 52-59% of their impact span at the crown while the spray mask still fades only over `v=0.54..1.0`, leaving broad crown geometry eligible for opacity. This is higher-band geometry/mask coupling, distinct from the already-approved transparent hinge.
- [x] Add `WaterSprayFeatheringRegressionTests.SprayPassDoesNotAdvertiseBroadCarrierAsHigherBandWedge` on `b52d42fd4d0293691680007eac7936e91762be97`; the fixture holds a representative broad tapered carrier constant, requires hinge transparency and readable mid-band mist, then caps higher-band lit area.
- [x] Apply one focused shared-renderer correction: keep carrier generation/cardinality and waterfall body behavior unchanged, but move only the spray upper falloff from `smoothstep(0.54, 1.0, v)` to `smoothstep(0.46, 0.72, v)` (`82506d54aeac9b26c7a154c7df7f612949ae4b49`).
- [ ] Let focused pre-fix discriminator run `33410018946` complete without replacement and record its result; request SHA `05408481a0a79f8c5d34869ddfe87e5f2556769c` is based exactly on `b52d42fd...`.
- [ ] Re-run `WaterSprayFeatheringRegressionTests` on the exact resulting fixed feature SHA and require both hinge and higher-band assertions green with readable free mist.
- [ ] Re-run `WaterArenaDrawRegressionTests` + automatic module validation + 60-second built `WaterRenderingShowcase` replay on that same exact feature SHA.
- [ ] Directly inspect saved built-player high-pitch/time-separated waterfall evidence and verify the higher-band planar/triangular wedges are absent while impact transparency and free mist remain legible.
- [ ] Record the exact resulting accepted feature SHA here after the higher-band follow-up lands.

## Exact-SHA evidence already established
- [x] `33385919424` green `WaterArenaDrawRegressionTests` + module + 60s replay on `a1a3594d...`; direct visual reject led to semantic-edge isolation.
- [x] `33386958512` product-strength red on first semantic-edge head (~1.15% vs required 2%); no unchanged retry.
- [x] `33390047406` green strengthened `WaterfallEdgeCoverageRegressionTests` + module + 60s replay on exact `e5747c90935a63b9b9665f04861e41a4f676e1ac`.
- [x] `33397721853` green exact `WaterArenaDrawRegressionTests` + module + 60s replay on `e5747c90...`.
- [x] `33398819271` presentation suite red only because cache coroutine used a fixed-yield timing race; standalone build/replay succeeded.
- [x] `33401066675` green `ShowcaseWaterPresentationRegressionTests` + module + 60s replay on exact `e82176c81508464c590183902009706fb4d800d7`.
- [x] Directly review `33401066675`: outer curtain sides are improved/irregular and body behavior remains stable, but hard triangular/planar base spray persists; visual closure rejected.
- [x] `33402041555` intentionally red focused spray-hinge regression on exact `5c7ec8bdb96796bec7839f240be63609060ca0aa`: hinge band had 302 lit pixels; standalone showcase still built/replayed successfully.
- [x] `33402597873` green `WaterSprayFeatheringRegressionTests` + automatic module validation + 60s replay on exact `8085f42b73dd07e17453a0a0803b5b24aa6b80f9`; the focused discriminator now proves the impact hinge is transparent while free mist remains visible.
- [x] `33405237070` green exact `WaterArenaDrawRegressionTests` + automatic module validation + 60s `WaterRenderingShowcase` replay for feature head `59607eadeb7f7c4f3bc9776f39882df576d25991` via request SHA `73380cb72699ffc31d8208d222990939f38bdda1`; no request was replaced or rerun.
- [x] Directly review `33405237070`: time-separated waterfall frames still show obvious bright triangular/planar spray wedges above the receiving-water contact band (including side and center footprints). This fails the explicit visual gate despite green automation; closure rejected.

## Next exact-head gates
- [x] Run `WaterSprayFeatheringRegressionTests` on the hinge-fixed exact feature head and require hinge band = 0 while free mist remains visible (`33402597873`).
- [x] On that hinge-fixed exact head, run `WaterArenaDrawRegressionTests` + automatic module validation + 60-second `WaterRenderingShowcase` replay (`33405237070` on `59607ead...`; automation green, visual gate rejected).
- [x] Directly inspect near/wide/time-separated built screenshots from `33405237070`; hard planar/triangular spray remains, so visual acceptance is rejected and a focused product fix is required.
- [ ] Complete the higher-band spray wedge exact-head gates above.
- [ ] After the spray-wedge fix is visually accepted, run `ShowcaseWaterPresentationRegressionTests` on the same accepted exact head.
- [ ] Run `WaterSprayProductionPathRegressionTests.CascadeSprayFlagSurvivesCanonicalStorageCacheAndGpuUpload` on the same accepted exact head.
- [ ] Confirm exact player build has no startup/runtime/shader compile/stripping/pink/missing-resource failure.
- [ ] Complete final CPU/GPU/memory/render-cost statement; do not weaken budgets or invent unavailable GPU timing.
- [ ] Resolve A5/A14 portability: prove one qualifying additional production scene with visible canonical water. `WorldbuildingGalleryShowcase` cannot count; Kentridge renderer integration alone is insufficient without visible-water evidence.

## Acceptance / closure
- [ ] Validate every issue acceptance criterion A1-A17 on the final exact head.
- [ ] Fill `resolutionSummary`, `regressionTest`, `fixCommit`, `status=fixed`, and `resolvedUtc` only after all acceptance is proven.
- [ ] Move the assigned issue directly from `open/` to `closed/` only after all gates are green.
- [ ] Fetch and merge latest `origin/master` at final closure stage, rerun required exact-head gates if the merge changes the validated head, then non-force promote exact closed feature head to `origin/master`; fetch/merge/retry if master advances.

## Current blockers
- [ ] A5/A14 remain externally/content-blocked: no defensible second existing production scene with **visible** canonical water has yet been proven. Continue independent renderer/test validation without changing acceptance or modifying unrelated scenes merely to manufacture evidence.
- [ ] Current `master` (`2edf4c2e151492f67c4a1c1b846a9b7948284aba`) and `fixes/agent-9` are materially diverged; a final-sync PR was confirmed non-mergeable and closed (`#185`) rather than forcing or dropping unrelated master changes. Resolve only in-scope conflicts before final exact-head validation/promotion.
