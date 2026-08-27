# Experiment 005 — post-fix focused regression green

## Hypothesis

The far-terrain shared texture-sampling fix at `506d4b37a42639bb1b9d48f1796e7794446d3c40` should satisfy the focused presentation contract that failed on the pre-fix shader.

## What was performed

Focused test:
`VoxelEngine.Tests.EditMode.FarTerrainSharedTexturePresentationTests.FarTerrainReusesVoxelSurfaceTextureSamplingContract`

Production source: `506d4b37a42639bb1b9d48f1796e7794446d3c40`.
CI request commit: `31baa3e0326ed6a9a97fe336a51517328f1ba65a` on `ci-test/fixes/agent-5`, whose parent is exactly the production source.
GitHub Actions run: `32992776265`; job: `98254238207`.

The Unity invocation completed with status `0` and the workflow reported `Executed 1 test case(s).` for the exact filter. The request commit published `ci/single-test: success` at 2026-08-26T17:21:56Z with description `Requested Unity single test passed`.

The Actions run record was subsequently marked `cancelled`, but only after every job step had completed successfully, including the requested Unity test, result artifact upload, and final success-status publication. The explicit `ci/single-test` commit status and job log are therefore the authoritative gate evidence.

## Result

**Green confirmed.** Exactly one intended regression test passed against the production fix source.

## What was learned

The shared far-terrain texture path satisfies the focused near/far presentation contract that was demonstrably absent before the fix.

## Next

Replay the original `20260826-132038-408-VoxelShowcase` capture through the real-player `--scene-issue` path at the recorded 1928x836 camera view and visually verify the stretched second grass presentation is gone before terminal bookkeeping.