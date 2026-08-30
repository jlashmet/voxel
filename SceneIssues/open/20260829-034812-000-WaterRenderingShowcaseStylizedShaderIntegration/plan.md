# Plan

## Goal / acceptance
Finish the stylized-water feature with one reusable production renderer and exact built-player proof. Still, river, and waterfall use canonical voxel storage/extraction plus one renderer-owned `Hidden/VoxelEngine/WaterSurface`; no scene-local water mesh/material fork. Built evidence must visibly show distinct motion and a convincing waterfall: downward flow, turbulence/aeration, irregular breakup, lip/edge/base foam, and mist/spray. `.github/test-request.json` stays unmodified on `fixes/agent-9`.

Follow `AGENTS.md`, `SceneIssues/README.md`, and `SceneIssues/feature-readme.md`.

## Proven findings
- Exact runs `33323151755`, `33324084398`, and `33336797164` passed automation but direct waterfall review rejected the falling sheet.
- `33336797164` proved the authored 62x62x2 Cascade curtain survives `ShowcaseWorld` storage, production `CpuWaterSurfaceChunkCache` extraction/upload/publication, cache visibility, and non-empty indexed geometry. The repeated visual symptom is therefore above the cache boundary.
- Shared arena addressing was a real correctness gap: water indices are lease-local while vertices use independently aligned arena ranges. Feature SHA `cfa69aeaf7406244d382999fae5b13a23d5c6daa` attempted to carry `VertexStart` through indirect `startInstance` and consume `SV_InstanceID` in `WaterSurface.shader`.
- Exact run `33337560328` passed that buffer-level regression and a 60-second built-player replay, but direct 32s/42s review still showed the bare stone cliff; the wide frame also lacked later river water while the early lake rendered.
- Required minimal repro run `33339119323` isolated the platform behavior on the target Metal backend: an indirect `startInstance=256` reaches the procedural shader as `SV_InstanceID=0`. The prior transport therefore cannot address later arena vertex leases on this backend.
- The production correction now mirrors the already-proven solid draw contract: water binds `_SurfaceVertexBase` explicitly per draw alongside `_SurfaceIndexBase`; `WaterSurface.shader` consumes that explicit base. Indirect `startInstance` is restored to neutral zero, and the temporary diagnostic shader is removed.
- Separate reuse defect remains fixed: CPU/Burst/GPU solid classification consumes the installed semantic water mask rather than IDs 11/16, covered by arbitrary opaque water-ID regression.

## Next work
1. Re-read current `origin/master`; merge it if it advanced before validation.
2. Run `WaterArenaDrawRegressionTests` on the exact corrected feature SHA through only `ci-test/fixes/agent-9`, with the required 60-second WaterRenderingShowcase replay.
3. Directly inspect near/wide/time-separated frames. The correction is not accepted unless the river and waterfall leases now render and the waterfall visibly meets downward-flow/turbulence/aeration/breakup/lip-edge-base foam/mist requirements.
4. On the same accepted feature head, run `ShowcaseWaterPresentationRegressionTests` and inspect final player logs/telemetry and the shared `VoxelShowcase`/`WorldbuildingGalleryShowcase` paths.
5. Only after every acceptance item is evidenced, complete A1–A17, issue metadata, open→closed move, latest-master merge, and non-force exact-head promotion.

## Cost / blast radius
Six 32-entry `Vector4` water tables cost 3,072 bytes plus one uint semantic water mask. The proven addressing correction adds one scalar integer to the water draw's existing `MaterialPropertyBlock`; it adds no geometry allocation, draw call, or simulation/storage work. This matches the existing solid renderer's semantic `_SurfaceVertexBase`/`_SurfaceIndexBase` contract. `Cull Off` remains the only prior transparent-fragment expansion and final player telemetry must remain within the recorded budget. Rendering classification/addressing changes do not alter collision, destruction, spreading, storage, or simulation semantics.

## Merge state
`fixes/agent-9` contains master `ebdc2e4f63ef73153cd4e0ff5c62efe604f35470` through merge `84fecff091649390e7ee8a67228a636219191e21`. Re-read master before the corrected exact-SHA request and again before promotion.
