# Plan

## Goal / acceptance
Ship one reusable stylized water renderer for still, flowing/river, and waterfall profiles through canonical voxel storage/extraction and `Hidden/VoxelEngine/WaterSurface`. Built-player evidence must visibly show distinct motion and a production-quality waterfall with coherent downward flow, turbulence/aeration, irregular breakup, lip/edge/base foam, and free mist/spray. Scene code owns composition only; no scene-local shader/material fork. `.github/test-request.json` stays off `fixes/agent-9`.

## Current proven state
- Shared still/river/waterfall profiles, presentation-driven water classification, canonical topology, spray flagging, cache/GPU arena transport, and selective depth-neutral spray pass are implemented and regression-covered.
- Metal indirect-draw vertex-base addressing, waterfall body depth breakup, receiving-water impact contact, turbulent carrier warp, and semantic side-edge erosion have each been isolated and corrected on earlier exact heads.
- Strengthened semantic-edge behavior passed exact run `33390047406`; the current body/extraction path also passed `WaterArenaDrawRegressionTests` in `33397721853`.
- `ShowcaseWaterPresentationRegressionTests` initially failed only because its cache discriminator used a fixed 120-coroutine-yield bound. The production cache path already uses asynchronous jobs, and an existing production-path regression had documented that yields can elapse faster than workers receive time. Replacing the fixed-yield race with the same nonblocking two-second wall-clock policy produced green exact run `33401066675` on `e82176c81508464c590183902009706fb4d800d7`; the standalone showcase also built and replayed for 60 seconds.
- Direct review of `33401066675` rejects final visual closure: the curtain sides are substantially more irregular and the body no longer has the earlier flat/straight-edge failure, but close built frames still expose repeated hard triangular/planar spray forms at the waterfall base.

## Current root cause
Per the two-attempt rule, do not resume arbitrary spray tuning. The canonical extractor emits three tapered trapezoidal spray sheets. Their lower footprints are intentionally the widest part of each sheet, while the depth-neutral spray shader currently begins its rise envelope at `sprayUv.y = 0.015`. That makes the broad shared impact hinge visible almost immediately, so even noisy/alpha-feathered spray advertises the underlying planar trapezoid/triangle carrier near the pool.

A focused independent production-shader raster discriminator, `WaterSprayFeatheringRegressionTests.SprayPassKeepsImpactHingeTransparentWhileFreeMistRemainsVisible`, renders ordinary spray-tagged canonical arena geometry. Exact run `33402041555` on `5c7ec8bdb96796bec7839f240be63609060ca0aa` fails as intended with **302 lit pixels** in the first ~7% above the impact edge, while the real standalone showcase still builds/replays successfully. This directly reproduces the planar-hinge symptom without scene composition.

## Selected correction
Keep canonical geometry/cardinality, `WaterSprayFlag`, cache/GPU transport, `ZWrite Off` spray pass, noise, material/profile selection, and still/river behavior unchanged. Let existing waterfall-body impact foam own the actual contact band and delay only free-mist visibility by changing the spray rise envelope from `smoothstep(0.015, 0.16, v)` to `smoothstep(0.12, 0.32, v)` (`ec95915d91c803fc8eaa1e35031456b63d7fdeb9`). This targets the demonstrated raster cause rather than adding scene-specific geometry or another rendering path.

## Next gates
1. Run `WaterSprayFeatheringRegressionTests` on the current exact feature head; require zero hinge-band pixels and retained free mist.
2. Run `WaterArenaDrawRegressionTests` plus automatic module validation and 60-second built `WaterRenderingShowcase` replay on the same exact head.
3. Directly inspect near/wide/time-separated screenshots. If hard planar/triangular base spray survives, stop and isolate a new minimal cause before another fix.
4. If visual quality is accepted, run `ShowcaseWaterPresentationRegressionTests` and `WaterSprayProductionPathRegressionTests.CascadeSprayFlagSurvivesCanonicalStorageCacheAndGpuUpload` on the same accepted head.
5. Confirm standalone startup/runtime/shader/stripping health and finish the accepted-head cost/blast-radius statement.
6. Resolve A5/A14 portability only with defensible built evidence from `VoxelShowcase` plus another actual production scene containing visible canonical water. `WorldbuildingGalleryShowcase` cannot count; Kentridge integration without visible water is insufficient.
7. Only after all acceptance is proven: update issue resolution fields, move open→closed, fetch/merge latest master, rerun exact-head gates if the merge changes the validated head, and non-force promote to `origin/master`.

## Cost / blast radius
Water profile tables remain six 32-entry `Vector4` arrays (3,072 bytes) plus one semantic mask. Spray retains the existing 32-byte vertex stride and three-sheet geometry cardinality; only spray-containing entries pay one additional indirect draw. The current hinge fix changes only two constants in the waterfall spray fragment envelope and adds no storage, extraction, API, allocation, draw-count, or non-waterfall cost. Final accepted-head CPU/GPU/memory/render statement remains pending; do not invent unavailable GPU timing.

## Blocker / merge state
A5/A14 remain blocked because no qualifying second existing production scene with **visible** canonical water has been proven. Continue independent renderer/test work without weakening acceptance or modifying unrelated scenes merely to manufacture evidence.

The feature contains an older master merge; fetch and merge the then-current `origin/master` only at the required final closure stage.
