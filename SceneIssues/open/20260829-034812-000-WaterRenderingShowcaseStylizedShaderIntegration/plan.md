# Plan

## Goal / acceptance
Finish the resumed stylized-water feature with one reusable renderer and exact built-player visual proof. Still, river, and waterfall must use canonical voxel storage/extraction plus one renderer-owned `Hidden/VoxelEngine/WaterSurface`; no scene-local water mesh/material fork. Built evidence must visibly show distinct still/river/waterfall behavior, including readable downward waterfall flow, turbulence, aeration, irregular breakup, lip/edge/base foam, and mist/spray. `.github/test-request.json` stays off `fixes/agent-9`.

`SceneIssues/feature-readme.md` is absent; follow `AGENTS.md` and canonical `SceneIssues/README.md`.

## Current findings
- Exact run `33323151755` on pre-repair `d3729aa...` passed 3/3 focused PlayMode tests and standalone build/launch, with ~1.5–2.1 ms average frame windows and ~698 MiB allocated memory, but visual review rejected the waterfall.
- Repair already landed: two-sided shared water pass, stronger vertical breakup/aeration/opacity, thinner semantic Cascade sheet/fingers, square-on waterfall framing, longer converged capture phases, and a vertical-cascade extraction regression.
- Current branch is merged with `master` through `3b3a55c...`; later `c1eef18...` added reusability-review tasks.
- Reusability audit: engine extraction uses only `WaterMaterialMask`; presentation/profile data lives in `WaterPresentationDefinition` and `VoxelPresentationCatalogue`; shared shader behavior is profile/table-driven. Showcase code only chooses game material/profile IDs, placement, camera, and capture telemetry. Capture telemetry executes only when the screenshot harness enables unattended capture.
- Missing proof: existing tests use canonical game IDs but do not prove arbitrary material IDs can share a semantic water profile or remap one ID to another profile without engine-code changes.

## Hypotheses / next discriminator
1. Repaired shared shader now produces a production-quality waterfall. Final exact built-player frames must prove or falsify this.
2. Renderer behavior is material-ID agnostic. Add a focused installer regression using arbitrary IDs, two rows sharing one profile, then remap one ID to a different profile and verify catalogue/mask state changes only from presentation data.

## Selected work / gates
1. Add the arbitrary-ID/remap regression and restore canonical game presentation after the test.
2. Re-read `master`, merge if advanced, review feature-only diff/cost, then freeze the repaired feature SHA.
3. Submit one canonical PlayMode + SceneIssue 60-second request via `ci-test/fixes/agent-9`; do not replace queued/running CI.
4. Require green focused tests + standalone build/launch, inspect logs/artifacts and every converged frame, and re-measure frame/memory/draw/overdraw implications of `Cull Off`.
5. Only after A1–A17 pass: complete metadata, move open → pending, then pending → closed with `status=fixed`/`resolvedUtc`; merge latest master and non-force promote the exact feature head.

## Blast radius / cost
Scope remains shared water presentation/extraction/shader, focused tests, bounded showcase authoring, build registration, and this assignment. Six 32-entry `Vector4` water tables cost 3,072 bytes. `Cull Off` may increase transparent fragments on exposed shells; final player evidence must record the measured impact without weakening budgets.
