# Experiment 014 — Current-bake replay CI second admission

## Hypothesis
Reissuing the exact saved-view replay with a fresh request id on the assigned CI branch will admit promptly and supersede the previously delayed request.

## What was performed
- Source commit: `654be76f11bf9e66f67a6d1c4498691287c429d4` (same production/test inputs as the prior replay source; only Experiment 013 documentation was added after `b88508d2a456c943999e57acfe581e8e2bb85104`).
- Reset `ci-test/fixes/agent-6` to that source commit and requested only `VoxelEngine.CI.SceneIssue20260825033053TreeInteractionTests.CapturedViewTreeBlocksPlayerAndRespondsToShot` in PlayMode.
- Request commit: `31b64e474c4ff9b00b0e64ce7c16f3220632cf1e`, request id `scene033053-current-bake-retry-20260826T1713Z`.
- Checked the exact request SHA through Actions runs, commit statuses, and check runs for more than seven minutes after its `2026-08-26T17:15:08Z` commit time. Also checked repository-wide push runs to distinguish repository admission latency from this request specifically.

## Result
**Inconclusive — request was not admitted.** GitHub created no Actions run, no check run, and no `ci/single-test` status for `31b64e474c4ff9b00b0e64ce7c16f3220632cf1e`. During the same interval, a different `ci-test/*` push committed at 17:19 was admitted to the same `tests-single.yml` workflow at 17:22, so the repository workflow dispatcher was active. The older superseded agent-6 request `f5e326e75f69b86dde5ab53073016ae75214e943` remained queued and is not authoritative under the latest-request-wins contract.

## What was learned
The replay itself still has not produced evidence from the newest request. This is a second per-push admission miss, not a Unity failure and not evidence about the captured geometry.

## Next
Create one fresh request from the new feature head on the same assigned CI branch. Monitor only that newest request. If it admits, inspect its replay artifact; if it again fails admission while newer repository pushes continue to admit, treat CI admission as a concrete blocker rather than claiming replay verification.
