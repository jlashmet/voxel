# Experiment 016 — reset admission window

## Hypothesis
After resetting `ci-test/fixes/agent-6` from the stale request head to the feature/template head, any reset-triggered `tests-single.yml` workflow must be allowed to appear before the real replay request is pushed; otherwise a delayed reset run could be instantiated later and cancel the real request under latest-request-wins concurrency.

## What was performed
- Reset `ci-test/fixes/agent-6` to feature commit `4f785189292f98f9f1e449397503f17a3b3b48f2` at approximately `2026-08-26T18:05Z`.
- Verified the CI branch points exactly at that feature commit and `.github/test-request.json` is back to the repository template.
- Polled Actions for `head_sha=4f785189292f98f9f1e449397503f17a3b3b48f2` through `2026-08-26T18:18Z`, exceeding the roughly 13-minute delayed-admission interval previously observed on this branch.

## Result
**No reset workflow was admitted.** GitHub returned zero workflow runs for the reset SHA throughout the full observed latency window. The CI ref remained stable at `4f785189292f98f9f1e449397503f17a3b3b48f2`.

## What was learned
The reset-to-feature event is no longer a credible late-arriving cancellation hazard within the branch's observed admission behavior. The replay can now be reissued once from the stable CI source without another reset.

## Next
Update only `.github/test-request.json` on `ci-test/fixes/agent-6` with a new unique request id for `VoxelEngine.CI.SceneIssue20260825033053TreeInteractionTests.CapturedViewTreeBlocksPlayerAndRespondsToShot`, then monitor only that new request SHA through terminal status and inspect its uploaded replay evidence.
