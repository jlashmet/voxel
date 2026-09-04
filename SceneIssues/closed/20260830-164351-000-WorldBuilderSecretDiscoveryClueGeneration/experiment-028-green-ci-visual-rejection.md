# Experiment 028 — Green exact CI, visual rejection

## Hypotheses

1. The remaining SecretDiscovery visual failure is caused by incomplete renderer convergence or validation-scene configuration, so the corrected production-fidelity player plus strict `visible > 0 && missing == 0` synchronization should produce acceptable Gallery captures.
2. The remaining symptom is owned by the shared base Gallery renderer/presentation path, so SecretDiscovery semantic and readiness gates can pass while the actual full-resolution production scene remains visually invalid.

## Action / source

Validated exact feature SHA `3e6cd24436fa0a5b3f8f23279697ada624734d16` through CI request `698aa3347a3065d1e495ba260cc90913fde71907`, workflow run `33852280392`. The request was not replaced while queued/running. Automatic module validation and standalone SceneIssue replay completed successfully.

Reviewed the artifact `single-test-33852280392` at full resolution, specifically:

- `SceneIssue/Screenshots/SecretDiscoveryAudit/01-natural-cave-approach.png`
- `SceneIssue/Screenshots/SecretDiscoveryAudit/02-authored-breakable-boundary.png`
- ordinary stationary Gallery captures from the same standalone replay.

## Result

Behavioral/execution gate: PASS. The exact run completed green, including automatic module validation and the standalone SceneIssue replay.

Visual gate: FAIL (`unacceptable`). The breakable capture is still below/through the terrain and exposes a large void/underside region despite the strict renderer-readiness gate. The natural capture is vegetation-dominated and does not communicate an understandable cave-secret clue at gameplay scale. Ordinary Gallery frames from prior exact evidence show the same class of base presentation defect outside the SecretDiscovery-specific pose.

The shared GPU renderer restoration is not yet authoritative on `origin/master`: PR #227 was closed unmerged, while PR #240 only merged `master` into `fixes/agent-1`.

## Verdict

Hypothesis 1 is falsified. Passing exact CI and renderer convergence does not establish production-quality visual output. Hypothesis 2 remains the leading explanation: the unresolved acceptance symptom is downstream/shared Gallery rendering rather than another SecretDiscovery publication/readiness defect.

Per `SceneIssues/issue-readme.md`, multiple materially different SecretDiscovery-side fixes have now failed to remove the same visual symptom. Do not apply another speculative camera/renderer workaround. Keep the SceneIssue open and record the renderer restoration as an external prerequisite.

## Next step

When the shared GPU renderer restoration lands through its own authoritative promotion path onto `origin/master`, merge current master into `fixes/agent-5`, run a fresh exact-SHA targeted gate, and re-review the two full-resolution SecretDiscovery captures. Close only if both are production-quality and understandable at gameplay scale.
