# Experiment 004 — current-head exact replay transport

## Hypothesis

The visual seam may already be gone on the current integrated tree because the failed attempt-1 replay is 163 master commits old. A fresh bake and exact saved-camera replay should establish whether production attempt 2 is still necessary.

## What was performed

- Confirmed `fixes/agent-1` and current `master` initially had the same tree `392d94d2f0d0f185c6d73d2a1ad0e15da1a51798` (`master` `bfccb29f34f2373ae7cafac5a38e21a7c2e9ba86`).
- Added a temporary exact-camera resource on feature commit `249b033c316717ff3cb72d70b18fb079dbf1a34b`, using `captures[0].camera` from this issue.
- Force-reset the assigned `ci-test/fixes/agent-1` branch to that exact feature commit.
- Requested `VoxelEngine.Tests.PlayMode.StationaryRenderBenchmarkTests.StationaryBenchmarkIsSeparatedFromSurveyCapture` through `.github/test-request.json`; the normal single-test workflow would bake VoxelShowcase and invoke the real-player capture utility.
- First request commit: `55df134d39c63359a9891362c0ecd5f08d3de2e5`, request id `scene221508-current-head-20260826T1655Z`.
- Reissued on the same CI branch with a unique request id after no run appeared: request commit `3dc362fe0959a8b5e457a832e2c3f41cd9cb5112`, request id `scene221508-current-head-20260826T1658Z-r1`.
- Queried both commit-status and Actions-run APIs for both request commits.

## Result

**Transport failure; visual hypothesis not tested.**

Neither request commit received a `ci/single-test` component status, and neither SHA appeared in the repository Actions run collection. The aggregate commit-status endpoint reports `state=pending` only because there are zero component statuses (`statuses=[]`, `total_count=0`); there is no queued or running workflow behind it.

The assigned CI branch itself is correct and points at the reissued request commit. Older pushes to the same branch have normal `Tests (single)` runs, so the workflow/branch convention exists; the connector-authored pushes in this session did not emit the push-triggered run.

## What was learned

**Current-head visual state remains inconclusive.** The diagnostic did not execute, so it cannot count as a production-fix attempt and cannot be cited as either a visual pass or fail. It did establish a concrete CI-trigger transport blocker for connector-authored request commits in this session.

## Next

Remove the temporary camera resource from the feature branch and continue source-level owner analysis without making production attempt 2. Before final verification, the required `ci/single-test` and exact-pose replay still must be obtained through a functioning repository CI trigger; do not close the issue on source inspection alone.
