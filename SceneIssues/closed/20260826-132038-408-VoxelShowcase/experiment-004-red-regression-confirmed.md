# Experiment 004 — pre-fix regression confirmed red

## Hypothesis

The focused presentation-contract regression should fail on the pre-fix source because `FarTerrain.shader` has no shared material/texture sampling path. The earlier absence of an Actions run was a delayed push-event delivery, not a permanent CI trigger failure.

## What was performed

Focused test:
`VoxelEngine.Tests.EditMode.FarTerrainSharedTexturePresentationTests.FarTerrainReusesVoxelSurfaceTextureSamplingContract`

Pre-fix source: `9a2a4695cb331db2bc486d6583b9ebcde8098e4d`.
CI request commit: `d9f2899e0e8cae007e895149c6646ee503cbe905` on `ci-test/fixes/agent-5`.
GitHub Actions run: `32991241203` (`tests-single.yml`, run 695).

The request commit was created at 2026-08-26T16:44:41Z. GitHub did not create the workflow run until 2026-08-26T16:55:55Z, explaining the earlier empty run/status queries.

The job executed exactly one EditMode test. Unity started successfully, the filter matched the intended regression, and the test failed on the first intended contract assertion because the pre-fix `FarTerrain.shader` did not contain `ResolveMaterialFromAlbedo`.

## Result

**Expected red confirmed.** This is a product-code regression failure, not an infrastructure/startup failure.

## What was learned

The hypothesis is confirmed: before the production change, far terrain does not participate in the shared grass/material texture-sampling contract. The earlier experiments that described the CI publication path as blocked were based on observing the branch before GitHub delivered the delayed push event; they should be read as timing observations, not as evidence that Actions could never run.

## Next

Keep the post-fix request `bc1ba26d5e3fee94f8fe7d8cfada8ec198be1023` intact until its delayed push event is delivered. Require the same exactly-one-test regression to pass against its parent production source `506d4b37a42639bb1b9d48f1796e7794446d3c40`, then replay the original saved SceneIssue camera before any terminal bookkeeping.
