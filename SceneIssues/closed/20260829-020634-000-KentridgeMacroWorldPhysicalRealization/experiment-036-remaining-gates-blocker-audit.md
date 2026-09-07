# Experiment 036 — Remaining gates blocker audit

## Purpose
Re-audit every unchecked task after Retry 5 and the documentation-only evidence commits, to ensure no independent acceptance/correctness work is being skipped while the renderer prerequisite is blocked by the coordinator-prescribed merge order.

## Current remote state
- Feature: `fixes/agent-6` at `c71c9b6ff7ec32f30d98709ffd546876902626a2` before this record.
- CI transport: `ci-test/fixes/agent-6` at `a0efab2841ee7175cea6e678b0fd30ee8b724f44`.
- Latest agent-6 run: `33641059051`, completed failure.
- Master: `b18d470f66221c7cb6091249f4683c2d994bffec`, containing the GPU renderer production-restoration merge.
- No queued/running agent-6 CI exists.

## Dependency audit
### Gate A — perimeter-foundation behavioral regression
The current perimeter-plinth assertion has never executed because repository-derived persistent renderer/GPU validation fails first on the stale pre-merge renderer state. Acceptance (9) therefore cannot be checked. Reissuing the same request from the documentation-only feature head would exercise the same executable source and is not a materially different fix or justified infrastructure retry.

### Gate B — strict built-player publication and visual acceptance
Settlement surveys, Rossdam lake/constrained-route evidence, Southern Ridge/pass evidence, final network overview, CharacterMotor traversal, and Fairy/Orc storage/readability all require the supported player evidence sequence to advance past `HasCompletePublishedNearSurfaceCoverage()`. Retry 5 remained false through 180 s with visible unpublished surface holes. Weakening that gate would change acceptance and is forbidden.

### Gate C — final runtime/cost evidence
Final vertical-residency counts, multi-target convergence, steady-state CPU/render/far-field cost, and process/managed/native/GPU-memory footprint require a successful final player replay. Existing Retry 5 evidence is useful but partial: Moordell readiness, FPS, prepare-sections timing, render-upload traffic, and one residency sample do not prove the required final multi-target budget.

### Gate D — closure and promotion
`resolutionSummary`, `regressionTest`, `fixCommit`, open-to-closed movement, final master merge, post-merge exact-SHA validation, and non-force promotion are all downstream of the required green exact-SHA gates and acceptance completion.

## Conclusion
No unchecked task is independently actionable on the current executable state. The only known repository prerequisite that can unblock Gate A/B/C is already on `origin/master`, but the coordinator instruction explicitly places the master merge after green exact-SHA gates. Agent-6 must therefore remain blocked rather than cherry-pick/copy renderer changes, merge master early, weaken coverage, modify unrelated renderer tests, or issue an identical CI retry.
