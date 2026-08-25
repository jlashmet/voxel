# Plan — SceneIssue 033249 floating tower

## Goal
Identify why the tower visible in the saved VoxelShowcase pose is unsupported/floating, add a focused regression for the actual deterministic cause, and make the smallest generation/composition fix that removes the invalid placement without disturbing unrelated Kentridge behavior.

## Scope
- Assigned capture only: `20260825-033249-397-VoxelShowcase`.
- Inspect the original screenshot and exact saved camera pose before production edits.
- Reproduce the same view through the repository's SceneIssue replay path and identify the responsible generated structure/site.
- Trace the smallest responsible placement, terrain-adaptation, structure-support, or composition subsystem.
- Add or reuse a regression based on the saved fixture or deterministic source/structure facts that directly proves the unsupported-tower condition cannot recur.
- Implement only the smallest production fix required by that invariant; if a later commit already made that exact fix on the persistent branch, identify and verify it instead of layering a duplicate capture-specific workaround.

## Constraints
- Use only `fixes/agent-1` and `ci-test/fixes/agent-1`.
- Never edit `.github/test-request.json` on `fixes/agent-1`.
- Preserve all prior unmerged work already accumulated on the persistent feature branch.
- Preserve the original screenshot and issue metadata as evidence until terminal bookkeeping.
- Remote validation uses push-triggered targeted CI; do not run Unity locally.
- Follow deterministic CPU-authority and single-source-of-truth constraints from `CLAUDE.md` and the active world-feature authoring spec.
- Record every replay, diagnostic, or fix attempt as a numbered experiment file immediately after it produces a result.

## Acceptance criteria
- [x] The exact saved VoxelShowcase pose no longer contains a visibly unsupported/floating tower.
- [x] The root cause is captured as a deterministic generation/composition invariant rather than a camera-specific cosmetic workaround.
- [x] A focused regression prevents VoxelShowcase from returning to the legacy Kentridge catalogue that produced the captured object.
- [x] The requested targeted CI test executes at least one test and `ci/single-test` is green.
- [x] A fresh replay of the original saved pose is inspected after the fix and retained as verification evidence.
- [x] The production fix exists as commit `416522e1816fd4e6a315f9831e523156304e1c18` on the persistent feature branch and the verification evidence is committed before terminal bookkeeping.
- [ ] Terminal bookkeeping sets `status=fixed`, `resolvedUtc`, `resolutionSummary`, `regressionTest`, and `fixCommit`, then moves the entire capture from `SceneIssues/open/` to `SceneIssues/closed/`.

## Tasks
- [x] Verify assignment exists on `origin/master` and persistent branch is current with master.
- [x] Read `AGENTS.md`, `CLAUDE.md`, `SceneIssues/README.md`, and capture metadata.
- [x] Inspect the original screenshot and reproduce/compare the saved pose before production changes.
- [x] Identify the smallest responsible subsystem: VoxelShowcase's legacy `KentridgeCombinedVoxelCatalogue` composition route.
- [x] Record baseline and isolation replay/diagnostic experiments.
- [x] Identify the focused regression that rejects the historical legacy route: `WorldBuilderAuthoringVisibilityTests.KentridgeTownAuthoringUsesOnlyWorldBuilderPublicBoundary`.
- [x] Identify the smallest deterministic fix already present on the persistent branch: commit `416522e1816fd4e6a315f9831e523156304e1c18`, routing VoxelShowcase through the single WorldBuilder-authored Kentridge plan.
- [x] Run targeted CI and confirm `ci/single-test` success (request `8947407043450b4df94fc61830fab140ea6ed41d`, run `32895526009`).
- [x] Replay every captured viewpoint after the fix and inspect the reported defect area (single saved viewpoint; current-source run `32886508286`).
- [x] Review the causal source diff against repository architecture/spec constraints; the fix removes a parallel authoring path and reinforces the single-source boundary.
- [x] Record verification evidence in experiment 006.
- [ ] Remove the temporary replay-only workflow, push separate open→closed terminal bookkeeping, verify remote state, and stop.

## Final findings
- The original frame is `1364x836`, captured at ~`222.43s` after scene load, so the report is from a settled scene rather than startup noise.
- Capture-era source `760dc909138088a46778f026501c17dd25f1b86d`, freshly baked and replayed in isolation, reproduces the floating tower.
- Current source with the WorldBuilder route, freshly baked and replayed at the same pose, does not reproduce it.
- Commit `416522e1816fd4e6a315f9831e523156304e1c18` is the isolated causal delta: it replaces `KentridgeCombinedVoxelCatalogue.Build(...)` with `WorldBuilderTownAuthoring.Author(...)` + `WorldBuilderVoxelCatalogue.Build(...)`.
- The focused WorldBuilder public-boundary regression is green on assigned targeted CI.
