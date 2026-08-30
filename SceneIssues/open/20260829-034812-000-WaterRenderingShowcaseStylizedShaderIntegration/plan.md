# Plan

## Goal / acceptance
Finish the stylized-water feature with one reusable production renderer and exact built-player proof. Still, river, and waterfall use canonical voxel storage/extraction plus one renderer-owned `Hidden/VoxelEngine/WaterSurface`; no scene-local water mesh/material fork. Built evidence must visibly show distinct motion and a convincing waterfall: downward flow, turbulence/aeration, irregular breakup, lip/edge/base foam, and mist/spray. `.github/test-request.json` stays unmodified on `fixes/agent-9`.

Follow `AGENTS.md`, `SceneIssues/README.md`, and `SceneIssues/feature-readme.md`.

## Proven findings
- Exact runs `33323151755`, `33324084398`, and `33336797164` passed automation but direct waterfall review rejected the falling sheet.
- `33336797164` proved the authored Cascade curtain survives canonical storage, production water-cache extraction/upload/publication, visibility, and non-empty indexed geometry.
- Required minimal repro run `33339119323` proved the target Metal procedural-indirect path delivers `SV_InstanceID=0` even with indirect `startInstance=256`.
- The production arena correction therefore binds `_SurfaceVertexBase` explicitly per water draw, matching the solid renderer's existing semantic offset contract; indirect `startInstance` stays zero.
- Exact corrected run `33339706799` is green for `WaterArenaDrawRegressionTests` and the 60-second real-player replay. Direct review proves the addressing defect is fixed: the early lake, later river, and waterfall curtain all render.
- That same direct review still rejects visual closure: the waterfall reads as a broad, bright rectangular wall with repetitive crossed bands, weak downward-flow readability, weak breakup, and no convincing visible mist/spray. This is a demonstrated acceptance-quality defect, not another geometry/addressing failure.
- The reusable water API already exposes semantic waterfall controls (`turbulence`, `edgeFoam`, `impactFoam`, `mist`); no new game-ID or scene-specific renderer API is required.
- Shader review identified a concrete quality cause: the waterfall branch forced every vertical fragment to at least ~0.84 alpha and combined crossed high-frequency sine bands. The current correction replaces that lattice with anisotropic descending strand fields and lets turbulence/coverage break vertical-sheet opacity instead of forcing a solid wall.

## Next work
1. Run the exact updated shader head through `WaterArenaDrawRegressionTests` plus the 60-second WaterRenderingShowcase replay using only `ci-test/fixes/agent-9` after confirming no active agent-9 CI and current master state.
2. Directly inspect near/wide/time-separated frames. Accept the shader correction only if downward motion, aerated strands/breakup, foam hierarchy and mist read materially better without losing river/lake rendering.
3. If the shader correction passes motion/material quality but the outer silhouette still reads too rectangular, change only WaterRenderingShowcase's ordinary Cascade voxel placement into an irregular stepped/fingered curtain; do not introduce a bespoke mesh/material/effect path.
4. On the first visually accepted feature head, run `ShowcaseWaterPresentationRegressionTests`, inspect final player logs/telemetry, and reconcile shared `VoxelShowcase` / `WorldbuildingGalleryShowcase` water paths.
5. Only after every acceptance item is evidenced, complete A1–A17, issue metadata, open→closed move, latest-master merge, and non-force exact-head promotion.

## Cost / blast radius
Six 32-entry `Vector4` water tables cost 3,072 bytes plus one uint semantic water mask. Explicit arena addressing adds one scalar integer to the existing per-water-draw property block and no draw call/allocation. The visual correction changes only existing waterfall shader math and opacity semantics; still/river branches and storage/simulation remain unchanged. `Cull Off` remains the prior transparent-fragment expansion. Final player telemetry must remain within the recorded budget; unavailable FrameTimingManager GPU values must not be invented.

## Merge state
`fixes/agent-9` contains master `ebdc2e4f63ef73153cd4e0ff5c62efe604f35470` through merge `84fecff091649390e7ee8a67228a636219191e21`. Re-read master before the next exact-SHA request and again before promotion.
