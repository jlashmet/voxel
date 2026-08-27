# Experiment 004 — pass-after CI delivery check

## Hypothesis
After force-resetting the assigned CI branch to the exact production fix commit, the same focused regression will start through `tests-single.yml` and pass.

## What was performed
Force-reset `ci-test/fixes/agent-7` to production fix commit `856f2bdf983d37434343d6076a81330df5765d26`, then changed only `.github/test-request.json` on that CI branch to request:
`VoxelEngine.Tests.EditMode.FarTerrainFogParityTests.FarTerrainUsesDetailedTerrainNearFieldFogEnvelope`.

The resulting latest-request commit is `11a857d675dd5a0547d4ea4b7ad2db004e82abe8`. Its parent is the exact fix commit, satisfying the CI-branch reset discipline in `AGENTS.md`.

Queried both the commit-status endpoint and Actions runs by exact head SHA. GitHub returned no `ci/single-test` status and zero workflow runs for the request commit. The same lack of event delivery occurred for the compliant red-baseline request in Experiment 002. Prior historical agent-7 runs on this same branch show `tests-single.yml` normally creates a workflow run within seconds, so this is not being interpreted as a test failure.

## Result
**Blocked before execution.** No CI job was created, so there is no Unity result, job log, or artifact to inspect and no valid basis for claiming the regression passed.

## What was learned
The remaining blocker is outside the shader/test change: push-triggered GitHub Actions event delivery is not occurring for connector-authored request commits in this session. The required validation cannot be substituted with source inspection because repository policy explicitly requires `ci/single-test` success.

## Next
Keep the capture open. When normal push-triggered Actions delivery is available, use the already prepared `ci-test/fixes/agent-7` request state (or exact-reset it again to the then-current feature production/test head) and run the focused regression before replay verification and terminal bookkeeping.
