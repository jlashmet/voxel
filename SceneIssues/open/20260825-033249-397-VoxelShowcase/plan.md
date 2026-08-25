# Plan — SceneIssue 033249 floating tower

## Goal
Identify why the tower visible in the saved VoxelShowcase pose is unsupported/floating, add a focused regression for the actual placement/terrain invariant, and make the smallest deterministic generation fix that grounds or removes the invalid placement without disturbing unrelated Kentridge/world-generation behavior.

## Scope
- Assigned capture only: `20260825-033249-397-VoxelShowcase`.
- Inspect the original screenshot and exact saved camera pose before production edits.
- Reproduce the same view through the repository's SceneIssue replay path and identify the responsible generated structure/site.
- Trace the smallest responsible placement, terrain-adaptation, structure-support, or composition subsystem.
- Add a regression based on the saved fixture or the deterministic structure/terrain facts that directly prove the unsupported-tower condition cannot recur.
- Implement only the smallest production fix required by that invariant.

## Constraints
- Use only `fixes/agent-1` and `ci-test/fixes/agent-1`.
- Never edit `.github/test-request.json` on `fixes/agent-1`.
- Preserve all prior unmerged work already accumulated on the persistent feature branch.
- Preserve the original screenshot and issue metadata as evidence until terminal bookkeeping.
- Remote validation uses push-triggered targeted CI; do not run Unity locally.
- Follow deterministic CPU-authority and single-source-of-truth constraints from `CLAUDE.md` and the active world-feature authoring spec.
- Record every replay, diagnostic, or fix attempt as a numbered experiment file immediately after it produces a result.

## Acceptance criteria
- The exact saved VoxelShowcase pose no longer contains a visibly unsupported/floating tower.
- The root cause is captured as a deterministic generation/placement invariant rather than a camera-specific cosmetic workaround.
- A focused regression proves the relevant structure cannot be emitted without valid terrain/structural support (or proves the invalid placement is rejected/adjusted, depending on root cause).
- The requested targeted CI test executes at least one test and `ci/single-test` is green.
- A fresh replay of the original saved pose is inspected after the fix and saved as verification evidence.
- Production/test changes are committed and pushed before the separate terminal bookkeeping commit.
- Terminal bookkeeping sets `status=fixed`, `resolvedUtc`, `resolutionSummary`, `regressionTest`, and `fixCommit`, then moves the entire capture from `SceneIssues/open/` to `SceneIssues/closed/`.

## Tasks
- [x] Verify assignment exists on `origin/master` and persistent branch is current with master.
- [x] Read `AGENTS.md`, `CLAUDE.md`, `SceneIssues/README.md`, and capture metadata.
- [ ] Inspect the original screenshot and reproduce the saved pose before production changes.
- [ ] Identify the exact floating structure and smallest responsible subsystem.
- [ ] Record baseline replay/diagnostic experiment.
- [ ] Add a focused red regression.
- [ ] Implement the smallest deterministic fix.
- [ ] Run targeted CI and iterate until green.
- [ ] Replay every captured viewpoint after the fix and inspect the reported defect area.
- [ ] Review final diff against repository architecture/spec constraints.
- [ ] Record verification evidence and complete this plan.
- [ ] Push separate open→closed terminal bookkeeping commit and stop.

## Initial facts
- One capture frame, no circle annotations.
- Original frame is `1364x836`, captured at ~`222.43s` after scene load, so the report is from a settled scene rather than early startup.
- Camera position: `(104.30445, 48.25026, 13.34524)`; FOV `70`.
- Issue note: `there is a floating tower up here. doens't make snese.`
