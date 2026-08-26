# Experiment 007 — explicit material identity regression red

## Hypothesis
The first saved-pose replay stayed stretched because far terrain discarded the exact application-owned material byte and reconstructed material policy from interpolated vertex RGB. Carrying the semantic material ID through a dedicated mesh channel should prevent a coarse triangle from selecting the wrong shared texture scale/policy.

## What was performed
- Strengthened regression source: `b905ad2d2722e942f7da7ff195d5cedae88ba9b1`.
- CI request: `a0d2447c2efad695b5a253f6567c617ae0ff64a1` on `ci-test/fixes/agent-5`.
- Workflow run: `32998080462`.
- Test: `VoxelEngine.Tests.EditMode.FarTerrainSharedTexturePresentationTests.FarTerrainReusesVoxelSurfaceTextureSamplingContract`.

## Result
**Expected red.** Exactly one test case executed and failed. The first failure was the intended new contract: `VoxelFarTerrain.cs` did not contain `_materialIdsScratch`, proving the far mesh still discarded explicit semantic material identity. Unity startup and the targeted-test infrastructure completed normally; `ci/single-test` published failure for the request commit.

## What was learned
Hypothesis remains viable and is now protected by a focused red regression. The missing behavior is specifically the far-mesh-to-shader material-ID path, not another texture asset or LOD-distance constant.

## Next
Implement the smallest second attempt: retain material IDs in reusable far-terrain scratch storage, publish them via `uv2`, consume one non-interpolated material ID per far triangle, and remove RGB reverse lookup. Then rerun this exact focused test green before another saved-pose replay.
