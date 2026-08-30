# Plan

## Goal / acceptance
Finish the stylized-water feature with one reusable production renderer and exact built-player proof. Still, river, and waterfall use canonical voxel storage/extraction plus one renderer-owned `Hidden/VoxelEngine/WaterSurface`; no scene-local water mesh/material fork. Built evidence must visibly show distinct motion and a convincing waterfall: downward flow, turbulence/aeration, irregular breakup, lip/edge/base foam, and mist/spray. `.github/test-request.json` stays off `fixes/agent-9`.

Follow `AGENTS.md`, `SceneIssues/README.md`, and `SceneIssues/feature-readme.md`.

## Proven findings
- Exact runs `33323151755`, `33324084398`, and `33336797164` passed automation but direct waterfall review rejected the falling sheet.
- `33336797164` proved the authored 62x62x2 Cascade curtain survives `ShowcaseWorld` storage, production `CpuWaterSurfaceChunkCache` extraction/upload/publication, cache visibility, and non-empty indexed geometry. The repeated visual symptom is therefore above the cache boundary.
- Shared arena addressing was a real correctness gap: water indices are lease-local while vertices use independently aligned arena ranges. Feature SHA `cfa69aeaf7406244d382999fae5b13a23d5c6daa` attempted to carry `VertexStart` through indirect `startInstance` and consume `SV_InstanceID` in `WaterSurface.shader`.
- Exact run `33337560328` passed `WaterArenaDrawRegressionTests` and a 60-second built-player replay, but direct 32s/42s review still shows the bare stone cliff; the wide frame also shows later river water absent while the early lake renders. The `startInstance` transport is therefore not a sufficient production correction on the exact target path.
- Before another fix, a minimal GPU repro now probes whether the current backend actually delivers indirect `startInstance` to `SV_InstanceID`. This discriminates platform draw semantics from another water-specific upload/draw defect.
- Separate reuse defect remains fixed: CPU/Burst/GPU solid classification consumes the installed semantic water mask rather than IDs 11/16, covered by arbitrary opaque water-ID regression.

## Next work
1. Run only `WaterArenaDrawRegressionTests` on the exact probe SHA through `ci-test/fixes/agent-9` (no replay needed for this discriminator). Do not replace active CI.
2. If the probe shows `SV_InstanceID` does not receive nonzero `startInstance`, remove the diagnostic shader after diagnosis and replace the implicit transport with explicit per-draw vertex-base state already supported by the water draw `MaterialPropertyBlock`; add a focused regression for that contract.
3. If the probe passes, leave draw transport intact and isolate the next water-specific render boundary before another product fix.
4. Re-run the 60-second real-player WaterRenderingShowcase and directly inspect waterfall/river frames after the proven correction, then run the full production water presentation suite on the same exact feature SHA.
5. If visual and behavioral gates pass, inspect logs/telemetry and complete A1–A17, issue metadata, open→closed move, latest-master merge, and non-force exact-head promotion.

## Cost / blast radius
Six 32-entry `Vector4` water tables cost 3,072 bytes plus one uint semantic water mask. The intended addressing correction must add no geometry allocation or draw call; explicit per-draw scalar state is acceptable if needed because each draw already owns a `MaterialPropertyBlock`. `Cull Off` remains the only prior transparent-fragment expansion and final player telemetry must remain within the recorded budget. Rendering classification/addressing changes do not alter collision, destruction, spreading, storage, or simulation semantics.

## Merge state
`fixes/agent-9` contains current `origin/master` `ebdc2e4f63ef73153cd4e0ff5c62efe604f35470` through merge `84fecff091649390e7ee8a67228a636219191e21`; master was re-read before run `33337560328` and had not advanced. Re-read master before each final exact-SHA request and again before promotion.
