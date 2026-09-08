# Experiment 012 — Stationary castle gap evidence

## Scope

This experiment records the fixed-camera VoxelShowcase evidence from exact targeted-CI request `00e166f7073b5c8871a45cd100bb49e5731ca1bd` / run `34202852688` / artifact `10050933912` (`sha256:b3553904f585429255f4b3077e35076b8731118cc0bdc4258810d7b63248df68`). The exact validated production/test source is `909c9893bc0d491fd3676e87e2616da32e5d2933`.

This is evidence only. It does **not** close the castle reproduction/trace task: traversal, return, negative-coordinate coverage, and a GPU-readback cell-to-draw trace remain required before any further renderer repair.

## Reproduction identity

The standalone SceneIssue replay built and ran `Assets/Scenes/VoxelShowcase.unity` for 180 seconds and captured 17 stationary frames from approximately `t=14.6s` through `t=174.7s`. The player log explicitly reports `SCENEISSUE issue.json has no replayable camera snapshot.` No autowalk or survey motion was requested.

The shipped scene contains a deterministic `Showcase Camera` at local/world position `(25.6, 30.0, 16.6)`, rotation quaternion `(0.13052619, 0, 0, 0.99144486)` (15° X hint), vertical FOV 70°, near clip 0.05m, far clip 16000m. Because the SceneIssue did not persist a capture snapshot, this transform is the reproducible scene-camera basis, not claimed post-runtime replay metadata.

## Stationary visual result

Representative terminal frame: `SceneIssue/Screenshots/showcase-016-t174.7s-stationary.png` (1600×900).

The stationary view is **not production-correct**. The defects remain obvious after nearly three minutes of fixed viewing:

- The castle shell is severely incomplete: large exterior wall/tower spans are absent and interior floors/rooms are exposed through what should be solid facades. The central/right castle mass contains multiple hard rectangular missing regions rather than a small isolated seam.
- The left side of the view contains a very large dark triangular/planar mass cutting across foreground-to-midground presentation. Its silhouette and scale are incompatible with the intended castle/terrain composition and it remains stable rather than resolving as a transient startup frame.
- Multiple bright cyan rectangular surface regions remain across the foreground and right-side midground. They form hard planar boundaries against surrounding terrain/water presentation and remain visible in the terminal frame.
- Smaller detached/floating or abruptly truncated geometry is visible around the castle walls and vegetation line. These are secondary to the large castle-shell and planar-gap failures above.

These observations are stable across the late stationary frames, so the defect is not merely a pre-readiness screenshot artifact.

## Renderer state at the late stationary window

The final residency reports show the view still far from complete:

- `missingVisible` stays roughly `505 -> 504 -> 504 -> 501 -> 501` across the final five reports.
- GPU demand reports zero unqueued demand in those samples, so the visible-missing accounting is represented in the existing demand/worker queues rather than silently lost at that aggregate boundary.
- Shared mirror source state reports `ready=40270`, `pending=0`, `mixed=40270/40270`; no arena allocation failure is reported (`gpuPressure allocFail=0 evicted=0`).
- `noSlot` nevertheless climbs from about `132721` to `138954` while one demand footprint remains active.
- Twelve GPU stages remain in flight. The oldest remains approximately 159–163 seconds old with `step=2 origin=int3(47, 31, 31) edge=18 source=GPU restarts=0`.
- Published ring residency at the final report is approximately step1 `130`, step2 `125`, step4 `59`, step8 `16`, while the GPU selection still reports `501` missing frustum candidates.

The currently available aggregate evidence therefore rules out “the frame just needed one more normal startup tick.” It does **not** yet identify which exact visible missing candidate corresponds to which captured pixel/world gap, nor whether the first broken transition is source admission, extraction/count, page publication, retirement, or draw selection.

## Why no world bounds are asserted yet

The acceptance task requires every selected visible gap to carry frame and **world** bounds plus a cell/candidate-to-draw causal trace. This exact run cannot honestly supply those world bounds because:

1. `issue.json` has no replayable capture snapshot and therefore no persisted camera/pose record for this run.
2. Existing `RINGS` diagnostics expose only aggregate missing counts plus the oldest active extraction footprint, not the exact GPU-readback missing candidate that maps to a visible gap.
3. Inferring a gap coordinate from the oldest extraction origin would conflate “oldest active request” with “this screen-space hole,” which is not proven.

The next diagnostic must therefore stay bounded and presentation-only: retain a constant-size identity from existing GPU demand feedback for a current missing-visible candidate, then correlate that exact `(sourceStep, chunk)` with desired/source generation, queue age, source coverage/admission, extraction/count, page handle/publication generation, retirement, selected draw and `Entry.WorldBounds`. Any occupancy/material probe needed after that identity is selected must come from a bounded GPU readback path; it must not create or replace world authority.

## Decision

**Stationary defect reproduced: yes. Visual acceptance: reject. Root cause: not yet proven.**

Do not implement another product repair from this evidence alone. First obtain the bounded missing-candidate identity and causal trace, then run the required frontier traversal and return route under the same convergence budgets. The task remains unchecked until those pieces are portable and linked to the captured defect.
