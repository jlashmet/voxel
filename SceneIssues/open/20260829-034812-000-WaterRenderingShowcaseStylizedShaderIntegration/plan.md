# Plan

## Goal / acceptance
Finish the stylized-water feature with one reusable production renderer and exact built-player proof. Still, river, and waterfall use canonical voxel storage/extraction plus one renderer-owned `Hidden/VoxelEngine/WaterSurface`; no scene-local water mesh/material fork. Built evidence must visibly show distinct motion and a convincing waterfall: downward flow, turbulence/aeration, irregular breakup, lip/edge/base foam, and mist/spray. `.github/test-request.json` stays unmodified on `fixes/agent-9`.

Follow `AGENTS.md`, `SceneIssues/README.md`, and `SceneIssues/feature-readme.md`.

## Proven findings
- Metal procedural-indirect arena addressing was fixed by explicit `_SurfaceVertexBase` (`33339706799`). Subsequent anisotropic strands and irregular Cascade ribbons improved flow/silhouette but did not produce convincing free spray (`33343405166`, `33345745137`).
- Root-cause isolation showed production lacked reusable lip/base/edge topology and free mist volume. Shared extraction now carries generic topology plus `WaterSprayFlag` through the same canonical mesh/material/draw path.
- `33361724893` proves `WaterSprayFlag` survives Storage → production cache → shared GPU arena. `33362961099` proves spray-tagged canonical arena geometry rasterizes for Cascade and clips for still water. Extraction/upload, draw binding, profile installation and spray branch reachability are therefore proven.
- `33364050913` passes `WaterArenaDrawRegressionTests`, automatic module validation and a 60-second exact-built WaterRenderingShowcase replay for the enlarged three-sheet fan. Direct review rejects that candidate: spray is finally visible but exposes hard triangular/starburst fan geometry at the base, while the close waterfall still reads too strongly as parallel bright translucent slabs.
- Candidate `4cdc5cd9cf7e6b49dc070bef0cfe59a4a0f3477c` addresses those demonstrated defects without a new renderer: spray corners carry two local bits in the existing auxiliary vertex word for shared-shader feathering; waterfall threads are modulated along the fall, body/aeration are darker and less uniformly opaque, and topology foam remains localized.
- Kentridge is a real independent shared-renderer consumer, but its available built captures contain no visible water and cannot satisfy A5 visual portability proof.

## Next work
1. Validate `4cdc5cd9cf7e6b49dc070bef0cfe59a4a0f3477c` with `WaterArenaDrawRegressionTests` plus a 60-second exact-built WaterRenderingShowcase replay.
2. Directly review near/wide/time-separated evidence. Do not accept if spray geometry remains legible as sheets/fans or the waterfall reads as a vertical blue/white plane.
3. On a visually accepted head, run `ShowcaseWaterPresentationRegressionTests` and the production-path spray regression, confirm clean player/shader/resource behavior, and finalize accepted-head cost.
4. Prove global replacement in `VoxelShowcase` and one additional actual production scene containing visible water; do not count `WorldbuildingGalleryShowcase`, and do not count Kentridge until visible water is demonstrated.
5. Only after A1–A17 and exact-SHA gates pass: complete metadata, move open→closed, merge latest master, and non-force promote the exact closed head.

## Cost / blast radius
Static water tables cost 3,072 bytes plus one uint semantic mask. Arena addressing adds one scalar per existing water draw. Spray still reuses the 32-byte vertex stride and the existing water draw/material; three ordinary quads are emitted only at exposed vertical lower boundaries. The new local spray coordinate consumes only the otherwise unused low two bits of `Active` for spray vertices. Final accepted-head arena/memory/frame figures remain to be measured; GPU timing is reported only if supplied by runtime telemetry.

## Merge state
Feature includes master `2ea5f5c95f89fbf0403dbefb50b782829583d304` via merge `87000073f2ca648922a18ae0788ed9008a55dd18`. Master remained `2ea5f5c95f89fbf0403dbefb50b782829583d304` before this candidate validation cycle. Re-read master before every exact CI request and final promotion.
