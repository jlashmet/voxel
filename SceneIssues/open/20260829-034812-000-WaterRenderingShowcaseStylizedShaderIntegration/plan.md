# Plan

## Goal / acceptance
Finish the stylized-water feature with one reusable production renderer and exact built-player proof. Still, river, and waterfall use canonical voxel storage/extraction plus one renderer-owned `Hidden/VoxelEngine/WaterSurface`; no scene-local water mesh/material fork. Built evidence must visibly show distinct motion and a convincing waterfall: downward flow, turbulence/aeration, irregular breakup, lip/edge/base foam, and mist/spray. `.github/test-request.json` stays unmodified on `fixes/agent-9`.

Follow `AGENTS.md`, `SceneIssues/README.md`, and `SceneIssues/feature-readme.md`.

## Proven findings
- Metal procedural-indirect arena addressing was fixed by explicit `_SurfaceVertexBase` (`33339706799`). Subsequent anisotropic strands and irregular Cascade ribbons improved flow/silhouette but did not produce convincing free spray (`33343405166`, `33345745137`).
- Root-cause isolation showed production lacked reusable lip/base/edge topology and free mist volume. Shared extraction now carries generic topology plus `WaterSprayFlag` through the same canonical mesh/material/draw path.
- `33361724893` proves `WaterSprayFlag` survives Storage → production cache → shared GPU arena. `33362961099` proves spray-tagged canonical arena geometry rasterizes for Cascade and clips for still water. Extraction/upload, draw binding, profile installation and spray branch reachability are therefore proven.
- `33364050913` passes `WaterArenaDrawRegressionTests`, automatic module validation and a 60-second exact-built WaterRenderingShowcase replay for the enlarged three-sheet fan. Direct review rejects that candidate: spray is finally visible but exposes hard triangular/starburst fan geometry at the base, while the close waterfall still reads too strongly as parallel bright translucent slabs.
- `33364793999` passes the feathered-spray regression/module/replay exact gate, but direct review again rejects the impact presentation. Feathering reduced brightness without removing the angular negative-space/starburst artifact.
- After the second materially different visual fix failed the same acceptance symptom, minimal root-cause isolation identified the renderer defect: spray shares the body pass's `ZWrite On`, so translucent spray fragments write depth and occlude later transparent water, revealing the fan footprint as terrain/pool-shaped negative space. More shader feathering cannot correct that depth-order behavior.
- Current candidate `3ec832584029b8432d190c7121db876120d24398` keeps the canonical water shader/material/arena but moves spray to a second `ZWrite Off` pass. `CpuWaterSurfaceChunkCache.Entry` records whether the published canonical mesh contains spray and issues the second draw only for those entries; ordinary water remains one draw.
- Kentridge is a real independent shared-renderer consumer, but its available built captures contain no visible water and cannot satisfy A5 visual portability proof.

## Next work
1. Validate exact feature head containing `3ec832584029b8432d190c7121db876120d24398` with `WaterArenaDrawRegressionTests` plus a 60-second exact-built WaterRenderingShowcase replay using only `ci-test/fixes/agent-9`.
2. Directly review near/wide/time-separated evidence. Reject if fan/starburst negative space remains, spray reads as hard sheets, or the waterfall reads as a vertical blue/white plane.
3. On a visually accepted head, run `ShowcaseWaterPresentationRegressionTests` and the production-path spray regression, confirm clean player/shader/resource behavior, and finalize accepted-head cost including the conditional spray draw.
4. Prove global replacement in `VoxelShowcase` and one additional actual production scene containing visible water; do not count `WorldbuildingGalleryShowcase`, and do not count Kentridge until visible water is demonstrated.
5. Only after A1–A17 and exact-SHA gates pass: complete metadata, move open→closed, merge latest master, and non-force promote the exact closed head.

## Cost / blast radius
Static water tables cost 3,072 bytes plus one uint semantic mask. Arena addressing adds one scalar per existing water draw. Spray reuses the 32-byte vertex stride and canonical water material; three ordinary quads are emitted only at exposed vertical lower boundaries. The local spray coordinate consumes only the otherwise unused low two bits of `Active` for spray vertices. The depth fix adds one extra procedural-indirect draw only for published water entries containing `WaterSprayFlag`; ordinary still/river water remains one pass. Final accepted-head arena/memory/frame figures remain to be measured; GPU timing is reported only if supplied by runtime telemetry.

## Merge state
Feature previously included master `2ea5f5c95f89fbf0403dbefb50b782829583d304` via merge `87000073f2ca648922a18ae0788ed9008a55dd18`. Master advanced to `8d8fccd1198e36d164c92fc80760580de12efe51` before this validation cycle. Do not merge it merely to start the focused candidate gate; re-read master before every exact CI request and merge current master before final exact-SHA closure/promotion as required.