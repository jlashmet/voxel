# Plan — 20260826-132429-861-VoxelShowcase

## Goal

Make the captured Kentridge facade glazing read unmistakably as architectural windows rather than large flat amber material slabs, while preserving the city's masonry/reveal vocabulary and existing glazing materials.

## Capture

- Note: `are these windows?  they look aweful`
- Scene: `VoxelShowcase`
- Saved camera: `(172.89835, 35.65001, 18.45081)`, FOV `70`
- Resolution: `1928x836`
- One capture, no annotation circles.

## Findings and current hypothesis

The captured facade uses the shared rectangular `ArchitectureVoxelPatterns.GlazedOpening` helper. Attempt 1 proved that restoring glazing through the full wall depth was wrong, but a thin centered pane alone was visually insufficient: the exact-camera replay still read as one uninterrupted amber slab.

Attempt 2 therefore targets the facade composition rather than only pane depth. Large rectangular openings retain masonry at the perimeter and through a central mullion, producing two inset thin planar panes. Small openings keep the simpler single-pane construction. The original facade normal is preserved explicitly while emitting split cells so both X-normal and Z-normal windows retain correct pane depth.

The integrated framed-glazing contract is green, and the successful exact saved-camera replay now shows distinct inset panes with masonry between and around them. The only remaining product gate before terminal bookkeeping is explicit human approval of that subjective visual result.

## Acceptance criteria

- [x] Inspect the assigned capture metadata and all annotation circles (none present).
- [x] Trace the captured window semantics from Kentridge generation into voxel authoring.
- [x] Add focused EditMode coverage for thin centered panes and for retained masonry framing/subdivision across facade orientations.
- [x] Record attempt 1's failed exact-camera replay and its visual conclusion.
- [x] Implement attempt 2 in the shared glazing pattern without city/material/renderer special-casing.
- [x] Commit and push attempt-2 production/test work to `fixes/agent-3` before issue bookkeeping.
- [x] Periodically reconcile current `master` into `fixes/agent-3`, including the shared SceneIssue replay and CI-dedup process updates.
- [x] Re-run the focused framed-glazing regression from the integrated product/test state through `ci-test/fixes/agent-3`; run `33003343182` and `ci/single-test` are green.
- [x] Use the shared `scene_issue` targeted-CI request to replay the exact saved camera in the standalone player; authoritative run `33004782593` is green after the one permitted infrastructure retry.
- [x] Persist the successful final replay image in this capture directory as `verification-final.jpg` and preserve the original `screenshot-001.png` as before evidence for subjective review.
- [x] Record the attempt-2 replay result in `experiment-006-exact-saved-camera-framed-glazing-replay.md`; keep runner/cache/timeout observations consolidated in `ci-operations.md`.
- [ ] Obtain explicit human approval that the subjective window-quality complaint is resolved before marking the capture fixed.
- [ ] Immediately before terminal bookkeeping, recheck current `master`; integrate any newer changes and rerun affected validation only if tested inputs changed.
- [ ] In a separate bookkeeping commit after approval, set `issue.json` to `fixed`, fill `resolvedUtc`, `resolutionSummary`, `regressionTest`, and the valid production/test `fixCommit`, then move the entire capture to `SceneIssues/closed/20260826-132429-861-VoxelShowcase`.
- [ ] Verify the terminal feature head, closed/open queue state, fix ancestry, and required green `ci/single-test`; then leave the feature head unchanged and enter the coordinator's batched promotion flow. Do not push `master` unless explicitly designated as the batch promoter.
- [ ] After the coordinator's batch promotion, verify remote `master` contains the fix/bookkeeping and only the closed capture before accepting another assignment.

## Constraints

- Work only this assigned capture.
- Use only `fixes/agent-3` and `ci-test/fixes/agent-3`; do not create another branch, PR, or one-shot replay workflow.
- `.github/test-request.json` changes belong only to CI request commits, never the feature branch.
- Build each CI request commit directly on the exact feature SHA, then move the remote CI ref once per iteration.
- Exact saved-camera replay uses the shared targeted-test workflow with `scene_issue` and integer `replay_seconds`.
- Showcase-dependent CI uses the shared runner-local content-fingerprinted bake cache; do not force or delete it.
- Remote worker: do not run Unity locally.
- Do not start or capture another SceneIssue.
