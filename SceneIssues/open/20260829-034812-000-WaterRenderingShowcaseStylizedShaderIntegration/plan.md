# Plan

## Goal / acceptance
Finish the stylized-water feature with one reusable production renderer and exact built-player proof. Still, river, and waterfall use canonical voxel storage/extraction plus one renderer-owned `Hidden/VoxelEngine/WaterSurface`; no scene-local water mesh/material fork. Built evidence must visibly show distinct motion and a convincing waterfall: downward flow, turbulence/aeration, irregular breakup, lip/edge/base foam, and mist/spray. `.github/test-request.json` stays unmodified on `fixes/agent-9`.

Follow `AGENTS.md`, `SceneIssues/README.md`, and `SceneIssues/feature-readme.md`.

## Proven findings
- `33339706799` fixed the Metal procedural-indirect arena-addressing defect with explicit `_SurfaceVertexBase`, restoring lake/river/waterfall geometry.
- `33343405166` replaced the waterfall lattice with anisotropic descending strands; motion improved, silhouette/mist remained unacceptable.
- `33345745137` changed only showcase Cascade placement to overlapping voxel ribbons; silhouette improved, but lip/base/mist still failed. Because the same symptom survived two materially different fixes, visual tweaking stopped for root-cause isolation.
- Comparing durable `WaterfallReference.shader` with production isolated missing reusable sheet topology: the production shader could not know lip/base/side boundaries, and shader-only mist could not create pixels outside the curtain.
- Shared extraction now marks lip/base/edge topology in the reserved high byte of `SmoothSurfaceVertex.Material`; `33346565021` proved those semantics and remained within the existing stride/buffer/draw path, but direct review still showed no free spray volume.
- Canonical impact-spray geometry was then added at true lower vertical-water boundaries, tagged with `WaterSprayFlag` and rendered through the same water mesh/material/draw. Non-waterfall profiles clip the semantic geometry.
- `33355120310` passed `WaterArenaDrawRegressionTests`, automatic module validation and a 60-second standalone `WaterRenderingShowcase` replay on the spray head. Direct 32.2s/42.2s/52.2s review still rejects free-spray acceptance: descending strands and irregular ribbons remain, but the lower curtain still reads as bright sheet bottoms rather than a plume.
- Spray-head cost remains bounded in that replay: arena `v=1,886,976/34,408,080`, `i=2,841,088/60,214,140`, `draws=191/16,384`, `leaseFail=0`; allocated memory 698.4 MiB, reserved 861.6–863.6 MiB, and 10–50s average-frame samples ~0.89–1.40 ms. CPU/GPU FrameTimingManager values are unavailable (`-1`) and are not inferred.
- Production-path discriminator `33356900725` was inconclusive because no cache build published within 120 coroutine yields.
- Diagnostic discriminator `33357865312` isolated that non-completion precisely: `dirty=1 runningJobs=1 pendingUploads=0 pendingBytes=0 meshOverflow=0 arenaFailures=0 blockingCompletionViolations=0 staleBuilds=0 residents=0 uploadedBytes=0 residentGpuBytes=0`. The test completes its 120 yields in ~0.46 s, so the worker batch is not becoming dispatch-ready in the Unity test harness; no downstream cache failure is indicated.
- The discriminator now calls nonblocking `JobHandle.ScheduleBatchedJobs()` after `cache.Prepare`, preserving the 120-frame bound and avoiding `JobHandle.Complete()` or any production change.
- Portability audit correction: `WorldbuildingGalleryShowcase` is a bounded structure-gallery mesh path and must not count as production-water proof. `KentridgePlayableSlice` does use canonical `ShowcaseWorld`, `RenderingWorldBinding` and `RenderingComposition.ConfigureWorld`; visible water content there still must be proven before using it for A5.

## Next work
1. Re-run only `WaterSprayProductionPathRegressionTests.CascadeSprayFlagSurvivesCanonicalStorageCacheAndGpuUpload` on the exact harness-flush feature head. If it reaches publication, prove or deny `WaterSprayFlag` survival through Storage → production cache → GPU arena.
2. If the flag survives, isolate the downstream rendering cause (geometry orientation/depth/presentation) before another visual correction. If it does not survive, fix only the proven canonical extraction/upload defect.
3. Run `WaterArenaDrawRegressionTests` plus a 60-second `WaterRenderingShowcase` replay on the corrected exact head. Accept only if free impact spray/mist is clearly readable alongside coherent downward flow, aeration/breakup, irregular silhouette, and lip/edge/base foam.
4. On the first visually accepted feature head, run `ShowcaseWaterPresentationRegressionTests` and the production-path spray regression, inspect final logs/telemetry, prove `VoxelShowcase` plus one actual production water scene, complete A1–A17/issue metadata, move open→closed, merge latest master, and non-force promote the exact closed head.

## Cost / blast radius
Six 32-entry `Vector4` water tables cost 3,072 bytes plus one uint semantic water mask. Explicit arena addressing adds one scalar integer to the existing per-water-draw property block and no draw call/allocation. Lip/base/edge/spray semantics reuse the already-reserved high byte of `SmoothSurfaceVertex.Material`, so vertex stride remains 32 bytes and there is still one canonical water buffer/draw path. Impact spray adds one ordinary quad only at an exposed vertical lower boundary; non-waterfall profiles clip it in the shader. Final exact-player telemetry must confirm these candidate bounds on the visually accepted head; unavailable GPU timing must not be invented.

## Merge state
`fixes/agent-9` contains master `2ea5f5c95f89fbf0403dbefb50b782829583d304` through merge `87000073f2ca648922a18ae0788ed9008a55dd18`. Master was re-read unchanged immediately before the harness-flush discriminator work. Re-read again before every exact CI request and final promotion.
