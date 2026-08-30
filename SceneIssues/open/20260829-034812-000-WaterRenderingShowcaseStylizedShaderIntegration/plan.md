# Plan

## Goal / acceptance
Finish the stylized-water feature with one reusable production renderer and exact built-player proof. Still, river, and waterfall must use canonical voxel storage/extraction plus one renderer-owned `Hidden/VoxelEngine/WaterSurface`; no scene-local water mesh/material fork. Built evidence must visibly show distinct motion and a convincing waterfall: downward flow, turbulence/aeration, irregular breakup, lip/edge/base foam, and mist/spray. `.github/test-request.json` stays off `fixes/agent-9`.

Follow `AGENTS.md`, canonical `SceneIssues/README.md`, and the now-merged `SceneIssues/feature-readme.md`.

## Current findings
- Exact runs `33323151755` and repaired `33324084398` passed automated gates, but direct player review rejected both: the waterfall cliff/lip/pool render while the falling vertical sheet does not.
- Framing is falsified: the Cascade curtain is ~2.3 m in front of the cliff and the square-on camera targets it. Shader-side face rejection is also unlikely: the shared pass uses `Cull Off`, no fragment discard, and strong vertical Waterfall opacity.
- Discovery/starvation/culling are falsified: initial discovery admits partially occupied bricks; the water cache uses `WaterMaterialMask`, has a 2,048-entry arena, prioritizes dirty bricks by camera, and visibility tests full brick AABBs rather than zero-thickness mesh bounds.
- The exact authored 62x62x2 Cascade curtain now has a focused PlayMode discriminator through `ShowcaseWorld` storage readback into production `CpuWaterSurfaceChunkCache`; exact CI will determine whether Cascade geometry/material identity survives that boundary before any further renderer change.
- The separate solid-classification reuse defect is implemented across CPU/Burst/GPU-equivalent density paths: material IDs are opaque and the installed presentation-water mask is mirrored through `SharedStatic` plus `_SolidWaterMaterialMask`. An arbitrary-ID regression proves the semantic boundary without gameplay IDs.
- `FlowerBlue = Cascade` remains only a transitional alias declaration; no live usages were found. Do not change Cascade gameplay semantics.

## Next discriminators / work
1. Run the focused exact Cascade storage→water-cache regression and semantic solid-classification regression through the assigned CI transport on the merged feature SHA.
2. If Cascade cache geometry is absent, add a focused regression for the proven storage/cache root cause and implement only the smallest shared correction. If geometry survives, continue only at the next shared upload/material-encoding/draw/depth boundary; do not alter shader art speculatively.
3. Obtain one green exact-SHA player build and inspect all durable near/wide/time-separated frames, logs, and telemetry directly against A1–A17.
4. After every checkbox and acceptance criterion passes, complete metadata, move open→closed, merge latest `master` again, and non-force promote the exact feature head to `origin/master`.

## Cost / blast radius
Six 32-entry `Vector4` water tables cost 3,072 bytes plus one uint semantic water mask mirrored to Burst/GPU state. `Cull Off` can increase transparent fragments; final evidence must retain budgets and record measured impact. Classification changes affect rendering only—never collision/destruction/spreading/storage semantics.

## Current commit / merge state
`fixes/agent-9` was merged with `origin/master` `ebdc2e4f63ef73153cd4e0ff5c62efe604f35470` via two-parent merge `84fecff091649390e7ee8a67228a636219191e21`; master-side changes were disjoint from agent-9 feature paths and preserved exactly. The subsequent plan bookkeeping commit must be included in the exact CI source SHA.
