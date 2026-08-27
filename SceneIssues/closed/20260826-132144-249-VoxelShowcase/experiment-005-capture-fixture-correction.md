# Experiment 005 — capture fixture metadata correction

## Hypothesis
The durable replay notes should be checked directly against the assigned capture's terminal `issue.json` before handing off a blocked issue.

## What was performed
Re-read `SceneIssues/open/20260826-132144-249-VoxelShowcase/issue.json` from both `fixes/agent-7` and `master` after pushing the blocker documentation. Both refs contain the same capture fixture: `Assets/Scenes/VoxelShowcase.unity`, `Showcase Camera`, position `(135.4204559326172, 135.42218017578126, 33.24650573730469)`, quaternion `(-0.021083341911435128, 0.9669615030288696, -0.239344522356987, -0.08517754822969437)`, FOV 70, near clip `0.05000000074505806`, far clip `16000`, and 1928×836 capture dimensions. The capture contains one screenshot and no circles.

The previously written plan/blocker had an incorrect camera fixture copied from unrelated working notes. Corrected those durable files immediately; no production or test code changed in this experiment.

## Result
The authoritative capture metadata is now recorded correctly in `plan.md` and `blocker.md`. The shader diagnosis and fix are unaffected because they were derived from the detailed/far shader code paths and the capture note, not from the incorrect camera coordinates.

## What was learned
Capture metadata must be treated as issue-local evidence and revalidated from the assigned `issue.json` at handoff. The exact replay remains blocked on targeted Actions delivery, but its future fixture is now unambiguous.

## Next
Keep the issue open. When CI delivery is restored, run the focused regression and exact replay using the corrected fixture before terminal bookkeeping.
