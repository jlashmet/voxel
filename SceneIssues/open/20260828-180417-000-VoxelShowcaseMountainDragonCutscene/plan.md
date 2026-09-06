# Plan

## Acceptance and ownership
Built `VoxelShowcase` must show a substantial grounded natural mountain, a readable continuous shared-road ascent from accessible terrain, a stably supported allowed cube dragon, and normal proximity dialogue exactly `Hello, I'm Mr. Dragon.` Every issue criterion/checklist item remains required. WorldBuilder owns landform/road authoring; Showcase composes it; Cutscenes owns dialogue; Rendering owns presentation; Composition owns bake provenance. Preserve canonical input and unchanged 240 s / 14 GiB bake guards.

## Current evidence
Run `34001756898` on source `a4a3df0d...` traversed 92/92 waypoints but its seven production captures were rejected: large magenta and gray slab masses obscured the mountain/road. Dedicated far-feature shader packaging was present, so missing shader packaging was not the fix.

Run `34006671692` on source `affc45d5...` passed module validation but the temporary isolation observer failed before its comparison frames because it could not rediscover the `DontDestroyOnLoad` replay harness. That lookup is corrected.

Run `34010802098` on source `3c46a02f...`, request `6df028095878dc272f0718f56a5435d843782d8f`, failed before player execution because `FarFeatureEmptyProjectionTests` used Mathematics types in a `noEngineReferences: true` test assembly. Experiment 041 records the compiler boundary and the demonstrated production defect.

The production defect is now corrected on the feature branch: operation-only `TerrainCorridor`/carve/paint/detail bakes no longer become null-geometry far-feature instances whose renderer fallback box spans the full operation bounds. Geometry, material and style are selected from the same positive `Fill`/`FillIfEmpty` primitives. The focused regression assembly now follows the repository's working Mathematics test pattern. Current master remains `356b2e0e4d2818901c73bbc6b1788f8d6850356d`.

## Next exact discriminator
Run repository-derived module validation and the 210 s Mountain Dragon SceneIssue replay on one exact source SHA. Require the focused Composition projection regression to pass. Inspect the ordinary full-rendering route captures first; if substantial magenta/slab output remains, use the same-camera isolation frames to identify any remaining draw owner before further rendering changes.

## Remaining gates
After a production-quality visual pass, remove temporary isolation instrumentation, rerun exact validation if source changes, promote only the matching visually accepted startup payload/manifest, prove clean-checkout consumption, complete every task and issue criterion, close directly `open -> closed`, re-sync current master if it advances, then promote only through PR + auto-merge and verify the closed assignment on master.
