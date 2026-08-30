# Plan

## Goal / acceptance
Finish the stylized-water feature with one reusable production renderer and exact built-player proof. Still, river, and waterfall use canonical voxel storage/extraction plus one renderer-owned `Hidden/VoxelEngine/WaterSurface`; no scene-local water mesh/material fork. Built evidence must visibly show distinct motion and a convincing waterfall: downward flow, turbulence/aeration, irregular breakup, lip/edge/base foam, and mist/spray. `.github/test-request.json` stays off `fixes/agent-9`.

Follow `AGENTS.md`, `SceneIssues/README.md`, and `SceneIssues/feature-readme.md`.

## Proven findings
- Exact runs `33323151755` and `33324084398` passed automation but failed direct waterfall review.
- Exact run `33336797164` on feature SHA `44947d3b0a4c60c09edbd0433cad389b984067bc` passed the 5-test `ShowcaseWaterPresentationRegressionTests` suite and a 60-second real-player build/capture, but direct 32s/42s review again showed the cliff with no falling sheet.
- That run proves the exact authored 62x62x2 Cascade curtain survives `ShowcaseWorld` storage, production `CpuWaterSurfaceChunkCache` extraction/upload/publication, and cache visibility with non-empty indexed geometry. The repeated visual symptom is therefore above the cache boundary.
- Root cause isolated at shared arena draw addressing: water indices are uploaded chunk-local at `lease.IndexStart`, vertices at independent aligned `lease.VertexStart`, but `CpuWaterSurfaceChunkCache.Entry.Draw`/`WaterSurface.shader` previously applied only the index base. Every water lease after the first could dereference vertices from the wrong arena range, explaining why early still water rendered while later Cascade geometry vanished/misplaced.
- Focused regression `WaterArenaDrawRegressionTests.SecondArenaLeasePublishesVertexBaseInIndirectDrawRecord` was added before the correction and requires a nonzero second lease to carry its vertex base.
- Minimal shared correction: `SurfaceGeometryArena.UploadArgs` uses indirect `startInstance` for immutable `VertexStart`; `WaterSurface.shader` consumes `SV_InstanceID` and adds it to the local water index. No scene art, gameplay, storage, or presentation semantics changed.
- Separate reuse defect is also fixed: CPU/Burst/GPU solid classification consumes the installed semantic water mask rather than IDs 11/16, covered by an arbitrary opaque water-ID regression.

## Next work
1. Validate the arena-addressing regression on the exact corrected feature SHA through `ci-test/fixes/agent-9`; inspect the same real-player waterfall views directly.
2. On the same corrected SHA, validate the production water presentation/cache suite so arbitrary-ID/remap, solid classification, portability, and exact Cascade storage→cache proof are all exact-SHA green.
3. If the waterfall remains absent, treat the arena-addressing hypothesis as falsified and isolate the next upload/draw/depth boundary before another fix. Do not alter shader art speculatively.
4. If visual and behavioral gates pass, inspect logs/telemetry and complete A1–A17, issue metadata, open→closed move, latest-master merge, and non-force exact-head promotion.

## Cost / blast radius
Six 32-entry `Vector4` water tables cost 3,072 bytes plus one uint semantic water mask. The draw fix adds no allocation or draw call: it reuses the existing fourth indirect argument. `Cull Off` remains the only prior transparent-fragment expansion and final player telemetry must remain within the recorded budget. Rendering classification/addressing changes do not alter collision, destruction, spreading, storage, or simulation semantics.

## Merge state
`fixes/agent-9` already contains `origin/master` `ebdc2e4f63ef73153cd4e0ff5c62efe604f35470` through merge `84fecff091649390e7ee8a67228a636219191e21`. Re-read master before each final exact-SHA request and again before promotion.
