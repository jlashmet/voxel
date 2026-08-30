# Plan

## Goal / acceptance
Finish the stylized-water feature with one reusable production renderer and exact built-player proof. Still, river, and waterfall must use canonical voxel storage/extraction plus one renderer-owned `Hidden/VoxelEngine/WaterSurface`; no scene-local water mesh/material fork. Built evidence must visibly show distinct motion and a convincing waterfall: downward flow, turbulence/aeration, irregular breakup, lip/edge/base foam, and mist/spray. `.github/test-request.json` stays off `fixes/agent-9`.

`SceneIssues/feature-readme.md` is absent; follow `AGENTS.md` and canonical `SceneIssues/README.md`.

## Current findings
- Exact runs `33323151755` and repaired `33324084398` passed automated gates, but direct player review rejected both: the waterfall cliff/lip/pool render while the falling vertical sheet does not.
- Framing is falsified: the Cascade curtain is ~2.3 m in front of the cliff and the square-on camera targets it. Shader-side face rejection is also unlikely: the shared pass uses `Cull Off`, no fragment discard, and strong vertical Waterfall opacity.
- Discovery/starvation/culling are falsified: initial discovery admits partially occupied bricks; the water cache uses `WaterMaterialMask`, has a 2,048-entry arena, prioritizes dirty bricks by camera, and visibility tests full brick AABBs rather than zero-thickness mesh bounds.
- Confirmed separate reusability defect: solid extraction still hard-codes material IDs 11/16 as non-solid in CPU/Burst density/classification paths. That leaves `RiverWater` (22) eligible for overlapping solid geometry. Cascade (16) is already excluded, so this does not explain the missing sheet.
- `FlowerBlue = Cascade` is only a transitional alias declaration; no live usages were found. Do not change Cascade gameplay semantics.
- Arbitrary-ID/remap water-profile regression is added but still needs final exact-SHA CI.

## Next discriminators / work
1. Prove the exact authored Cascade curtain survives canonical `ShowcaseWorld` storage readback and production water-cache geometry/material encoding. Add the focused regression before changing shader art again.
2. Audit all stale solid-water classification copies (CPU/Burst/GPU), replace hard-coded IDs with the installed presentation-water mask using the smallest job-safe scalar plumbing, and add regression coverage. This is required for reusable RiverWater even if independent of the Cascade visual bug.
3. If storage/cache proof shows Cascade geometry exists, continue only at the next shared boundary (upload/material encoding/draw/depth) until the missing-sheet root cause is proven.
4. Merge latest `master`, freeze final feature SHA, submit one final canonical request on `ci-test/fixes/agent-9`, inspect all exact-built frames/logs/metrics, then close only after A1–A17 pass.

## Cost / blast radius
Six 32-entry `Vector4` water tables cost 3,072 bytes. `Cull Off` can increase transparent fragments; final evidence must retain budgets and record measured impact. Classification changes must affect rendering only—never collision/destruction/spreading/storage semantics.
