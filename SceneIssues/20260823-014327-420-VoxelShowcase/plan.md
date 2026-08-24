# SceneIssue 014327 — transient terrain mound near town

## Goal

Eliminate the terrain-looking mound that appears in front of the town at the first saved camera
pose and disappears after the player moves a few steps to the second pose.

## Scope

- Exact 1293×718 replay of both saved `Showcase Camera` poses.
- Near/coarse LOD selection, replacement, and transition ownership if the artifact still reproduces.
- Terrain authoring only if voxel-state evidence shows the mound is authoritative rather than a
  presentation-level LOD artifact.
- A focused deterministic regression for the proven invariant.

## Constraints

- Continue on the shared `fixes` branch and work only this oldest open capture.
- Treat the two captures as a movement sequence and compare the marked screen region at both poses.
- Use `tools/unity-run.sh` for local Unity; never invoke the Unity binary directly.
- Record every replay, test, and diagnostic as a numbered experiment in this capture directory.
- Preserve temporary replay fixtures and player artifacts outside the committed production tree.
- Commit production/test changes before a separate `issue.json` resolution commit.

## Initial evidence

- Capture 001 at `(-107.60, 23.75, -333.80)` marks a small foreground terrain feature while the
  town remains distant on the horizon.
- Capture 002 moves roughly 9.6 metres forward/right to `(-102.49, 23.15, -325.67)` and reports
  that the feature disappears.
- Both screenshots were taken after more than 200 seconds in the scene with 95 visible surfaces,
  so this is more consistent with distance/LOD replacement than initial streaming convergence.

## Findings

- Current-head exact replays at both saved poses converge with zero missing visible surfaces. The
  triangular terrain-like silhouette inside the original pose-1 circle is replaced by the castle's
  rectangular structural frontage; pose 2 remains structurally consistent.
- Expanding all detail-band handoffs from 0.6 to 0.8 does not materially change the marked current
  presentation, so the current result is not an accidental finer-ring substitution.
- A first renderer diagnostic was invalid because gameplay moved the camera after setup. Pinning
  the pose on every render frame corrected the experiment.
- The pinned marked ray reaches authoritative castle content about 416 m from the camera, covered
  by source-step-8 HLOD. The original transient mound therefore matches invalid nearer GPU terrain
  occluding a valid distant structure, rather than the castle itself becoming terrain HLOD.
- The focused boundary/vertex-parity and complete HLOD-summary set passes 13/13 on the clean tree.
  No new production or test edit is warranted because the causal boundary invariant and the marked
  structure's HLOD invariants are already directly covered.

## Acceptance criteria

1. Current-head exact replays at both saved poses classify whether the marked artifact persists.
2. A focused regression proves the responsible LOD/ownership invariant with a nonzero test count.
3. The smallest causal fix removes the transient mound without hiding authoritative terrain.
4. Final exact-pose replay shows stable terrain presentation across the saved movement sequence.
5. Temporary replay wiring is absent and `issue.json` is resolved in a separate commit.

## Work

- [x] Read both captures, camera metadata, note, and marked screen locations.
- [x] Replay current `fixes` at both exact poses and inspect the marked regions.
- [x] Isolate whether the mound is voxel state, coarse mesh, transition mesh, or stale replacement.
- [x] Confirm the proven cause is already covered by focused boundary and HLOD regressions.
- [x] Preserve and document the existing smallest causal boundary-ownership fix.
- [x] Run focused affected Unity validation: boundary/parity and HLOD summaries pass 13/13.
- [x] Replay both saved poses and compare terrain stability.
- [ ] Review, commit, push, resolve `issue.json` separately, and confirm branch cleanup.
