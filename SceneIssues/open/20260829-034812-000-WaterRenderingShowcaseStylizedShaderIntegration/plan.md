# Plan

## Goal / acceptance
Finish the stylized-water feature with one reusable production renderer and exact built-player proof. Still, river, and waterfall use canonical voxel storage/extraction plus one renderer-owned `Hidden/VoxelEngine/WaterSurface`; no scene-local water mesh/material fork. Built evidence must visibly show distinct motion and a convincing waterfall: downward flow, turbulence/aeration, irregular breakup, lip/edge/base foam, and mist/spray. `.github/test-request.json` stays unmodified on `fixes/agent-9`.

Follow `AGENTS.md`, `SceneIssues/README.md`, and `SceneIssues/feature-readme.md`.

## Proven findings
- Metal procedural-indirect arena addressing was fixed by explicit `_SurfaceVertexBase` (`33339706799`). Subsequent anisotropic strands and irregular Cascade ribbons improved flow/silhouette but did not produce convincing free spray (`33343405166`, `33345745137`).
- Root-cause isolation showed production lacked reusable lip/base/edge topology and could not create mist pixels outside the curtain. Shared extraction now encodes generic topology plus `WaterSprayFlag` in the reserved material byte and emits an impact-spray quad at true lower vertical-water boundaries; all use the same canonical mesh/material/draw path. Independent extraction/arena regressions pass.
- `33355120310` passed `WaterArenaDrawRegressionTests`, automatic module validation, and a 60-second `WaterRenderingShowcase` player replay, but direct review still rejected free spray/mist. Cost stayed bounded: arena `v=1,886,976/34,408,080`, `i=2,841,088/60,214,140`, `draws=191/16,384`, `leaseFail=0`; allocated ~698.4 MiB, reserved ~862 MiB, average-frame samples ~0.89–1.40 ms; GPU timing unavailable.
- Production-path runs `33356900725` and `33357865312` failed before publication: one mesh job remained running after 120 coroutine yields (~0.46 s), with all downstream failure counters zero. Adding nonblocking `JobHandle.ScheduleBatchedJobs()` still reproduced that state in `33358290720`.
- Required minimal root-cause probe `33361014521` passed when the same cache path used a two-second wall-clock bound. The run also passed automatic `kentridge-integration` validation, building and running `KentridgePlayableSlice` for 60 seconds. Therefore the cache job is not stuck; the fixed 120-yield test bound was invalid. The original discriminator now uses the proven wall-clock bound and the temporary probe is removed.
- Kentridge is a real independent consumer of `ShowcaseWorld` + `RenderingWorldBinding` + `RenderingComposition`, but visible water content there is still unproven and cannot yet satisfy A5.

## Next work
1. Run only `WaterSprayProductionPathRegressionTests.CascadeSprayFlagSurvivesCanonicalStorageCacheAndGpuUpload` on the exact wall-clock-harness head. Prove or deny `WaterSprayFlag` survival through Storage → production cache → GPU arena.
2. If the flag survives, isolate the downstream renderer visibility/presentation cause before another visual correction. If it does not, fix only the proven extraction/upload defect.
3. Validate the corrected candidate with `WaterArenaDrawRegressionTests`, 60-second `WaterRenderingShowcase` replay, direct near/wide/time-separated review, then final presentation/production-path regressions and real production-scene water portability proof.
4. Only after A1–A17 and exact-SHA gates pass: complete metadata, move open→closed, merge latest master, and non-force promote the exact closed head.

## Cost / blast radius
Static water tables cost 3,072 bytes plus one uint semantic mask. Arena addressing adds one scalar per existing water draw. Topology/spray reuse the existing 32-byte vertex stride; spray adds one ordinary quad only at exposed vertical lower boundaries and non-waterfall profiles clip it. No production code changed for the test-harness timing fix.

## Merge state
Feature includes master `2ea5f5c95f89fbf0403dbefb50b782829583d304` via merge `87000073f2ca648922a18ae0788ed9008a55dd18`. Re-read master before every exact CI request and final promotion.
