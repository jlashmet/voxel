# Plan

## Goal / acceptance
Finish the stylized-water feature with one reusable production renderer and exact built-player proof. Still, river, and waterfall use canonical voxel storage/extraction plus one renderer-owned `Hidden/VoxelEngine/WaterSurface`; no scene-local water mesh/material fork. Built evidence must visibly show distinct motion and a convincing waterfall: downward flow, turbulence/aeration, irregular breakup, lip/edge/base foam, and mist/spray. `.github/test-request.json` stays unmodified on `fixes/agent-9`.

Follow `AGENTS.md`, `SceneIssues/README.md`, and `SceneIssues/feature-readme.md`.

## Proven findings
- Metal procedural-indirect arena addressing was fixed by explicit `_SurfaceVertexBase` (`33339706799`). Subsequent anisotropic strands and irregular Cascade ribbons improved flow/silhouette but did not produce convincing free spray (`33343405166`, `33345745137`).
- Root-cause isolation showed production lacked reusable lip/base/edge topology and could not create mist pixels outside the curtain. Shared extraction now encodes generic topology plus `WaterSprayFlag` in the reserved material byte and emits impact-spray geometry at true lower vertical-water boundaries; all use the same canonical mesh/material/draw path. Independent extraction/arena regressions pass.
- `33355120310` passed `WaterArenaDrawRegressionTests`, automatic module validation, and a 60-second `WaterRenderingShowcase` player replay, but direct review still rejected free spray/mist. Cost stayed bounded: arena `v=1,886,976/34,408,080`, `i=2,841,088/60,214,140`, `draws=191/16,384`, `leaseFail=0`; allocated ~698.4 MiB, reserved ~862 MiB, average-frame samples ~0.89–1.40 ms; GPU timing unavailable.
- Production-path runs `33356900725` and `33357865312` failed before publication: one mesh job remained running after 120 coroutine yields (~0.46 s), with all downstream failure counters zero. Adding nonblocking `JobHandle.ScheduleBatchedJobs()` still reproduced that state in `33358290720`.
- Required minimal root-cause probe `33361014521` passed when the same cache path used a two-second wall-clock bound. The cache job is not stuck; the fixed 120-yield test bound was invalid. The corrected original discriminator subsequently passed (`33361724893`), proving `WaterSprayFlag` survives Storage → production cache → shared GPU arena.
- Exact raster discriminator `33362961099` passed on source SHA `6e8b574b7055e960a482f54c465ea62099be9b2a`: spray-tagged canonical arena geometry rasterizes with the installed Cascade profile and is clipped for still water. Automatic `kentridge-integration` built and ran cleanly. This rules out extraction/upload, draw binding, profile installation, and the spray shader branch as the remaining visibility cause.
- Direct review plus the two successful discriminators isolate the remaining defect to presentation footprint: the prior canonical spray skirt extended only `1.6` voxels outward and `2.4` voxels upward (~0.4 m × 0.6 m at 0.25 m voxels), too small to read beside a several-metre waterfall. Candidate `bf6e2c4756b8b90b688b680a7dfe2e9d519e229b` replaces it with three differently pitched canonical spray sheets and adds a reusable world-space extent regression.
- Kentridge is a real independent consumer of `ShowcaseWorld` + `RenderingWorldBinding` + `RenderingComposition`, but its available built captures contain no visible water and therefore cannot satisfy A5 visual portability proof.

## Next work
1. Validate candidate `bf6e2c4756b8b90b688b680a7dfe2e9d519e229b` with `WaterArenaDrawRegressionTests` plus a 60-second exact-built `WaterRenderingShowcase` replay.
2. Directly review near/wide/time-separated waterfall evidence. Accept only if downward motion, turbulence/aeration, irregular breakup, lip/edge/base foam and free mist/spray are readable without degrading still/river/contact quality.
3. On the same visually accepted feature head, run `ShowcaseWaterPresentationRegressionTests` and `WaterSprayProductionPathRegressionTests.CascadeSprayFlagSurvivesCanonicalStorageCacheAndGpuUpload`; confirm clean player/shader/resource behavior and finalize measured cost.
4. Prove global replacement in `VoxelShowcase` and one additional actual production scene containing visible water. Do not count Kentridge until visible water is demonstrated and do not count `WorldbuildingGalleryShowcase`.
5. Only after A1–A17 and exact-SHA gates pass: complete metadata, move open→closed, merge latest master, and non-force promote the exact closed head.

## Cost / blast radius
Static water tables cost 3,072 bytes plus one uint semantic mask. Arena addressing adds one scalar per existing water draw. Topology/spray reuse the existing 32-byte vertex stride. The candidate changes one spray quad per exposed vertical lower boundary to three ordinary quads (12 vertices / 18 indices total per boundary); non-waterfall profiles still clip them and no additional draw/material/renderer path is introduced. Final accepted-head arena/memory/frame figures remain to be measured. GPU timing is reported only if the runtime supplies it.

## Merge state
Feature includes master `2ea5f5c95f89fbf0403dbefb50b782829583d304` via merge `87000073f2ca648922a18ae0788ed9008a55dd18`. Master remained `2ea5f5c95f89fbf0403dbefb50b782829583d304` immediately before the candidate validation request. Re-read master before every exact CI request and final promotion.
