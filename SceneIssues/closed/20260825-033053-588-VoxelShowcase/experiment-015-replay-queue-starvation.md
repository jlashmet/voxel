# Experiment 015 — replay queue starvation

## Hypothesis
The admitted saved-view request at `31b64e474c4ff9b00b0e64ce7c16f3220632cf1e` is no longer a practical authoritative gate because it has remained queued while multiple later requests on the same self-hosted macOS runner were assigned and completed.

## What was performed
Monitored Actions run `32994328029` continuously after admission. The run stayed `queued` with no Unity steps from 2026-08-26 17:28Z past 18:00Z, while later requests from other agent CI branches were assigned to the same runner and completed. No newer agent-6 request was created during that observation.

## Result
**Confirmed queue starvation for this request.** The request is admitted but is not being scheduled in normal queue order, so continuing to wait on it does not provide a reliable verification path.

## What was learned
The previous delayed-admission race left agent-6 with a stale queued workflow instance. A clean reissue must avoid both causes seen so far: do not leave the CI branch at a stale request head, and do not push the real request until the reset-to-feature push has itself been admitted.

## Next
Reset `ci-test/fixes/agent-6` to this feature head, wait until any reset-triggered workflow is visible, then create exactly one new request-file commit with a unique request ID and monitor only that newest SHA.
