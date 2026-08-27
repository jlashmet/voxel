# Plan — 20260826-132505-873 VoxelShowcase

## Defect / acceptance
Capture note: `there is a floating mailbox`; no circles, so the whole saved pose is acceptance. The foreground object is the east-market street lamp near authored `(1530,549)` dm. Accept only when a clean native-resolution replay of the saved pose shows its gray foot visually contacting the working-yard shoulder, with pole/lantern continuous and nearby streetscape unchanged.

## Competing hypotheses / evidence
1. **Lamp uses the wrong district elevation — confirmed and fixed.** The authored macro elevation differs from the generated working-yard terrace surface; placement now follows that district surface.
2. **Thin reconstructed support disappears — confirmed and fixed.** The dark pole and stone foot use `SurfaceStyles.Planar`.
3. **A boundary seam remains between terrace and foot — selected product fix.** The foot is embedded one voxel while preserving its top/upper lamp geometry. The behavioral regression evaluates both production catalogues and proves generated-surface overlap plus foot→pole→lantern continuity.
4. **The latest visually failing replay proves more embed is needed — rejected as evidence.** Request `d633741fe940c31b783b01f912d55839527d5208` (source `e4f952ad29118443bbf487f786e98583df73204d`, run `33107330590`) passed the regression, but the job restored cached `ShowcaseWorld.bytes`. The bake-cache fingerprint omitted `Assets/Game/WorldBuilder`, where the lamp fix lives, so that replay could be stale.

## Fix / regression / blast radius
Keep the district-surface placement, Planar support, and one-voxel foot embed. `CapturedEastMarketLampKeepsPlanarSupportUnderLantern` remains the behavioral regression through current catalogue/program evaluation. Add `Assets/Game/WorldBuilder` to the reusable VoxelShowcase bake-cache fingerprint so world-generation source changes invalidate startup bakes. This changes verification caching only: no runtime allocations/jobs and no renderer-wide behavior.

## Current / gates
Feature head `e4f952ad29118443bbf487f786e98583df73204d` contains current master `314d0dc4b3b88167bdc5eefbc43bf31e45c10eb1`. Commit the cache-fingerprint correction plus experiment evidence, request exact-head PlayMode + saved-pose replay, confirm the run uses a bake keyed by the current WorldBuilder state, then inspect the image directly. Only after green CI and visually accepted clean replay should the capture advance through pending→closed and the verified feature head be merged to master.
