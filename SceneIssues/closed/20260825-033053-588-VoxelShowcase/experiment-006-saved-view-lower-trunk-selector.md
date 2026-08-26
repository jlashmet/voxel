# Experiment 006 — saved-view lower-trunk selector

## Hypothesis
A tree implicated by the whole-frame capture will have a lower-trunk midpoint inside the saved camera frustum, allowing the replay to use that same point for both collision and shooting assertions.

## What was performed
Reran `VoxelEngine.CI.SceneIssue20260825033053TreeInteractionTests.CapturedViewTreeBlocksPlayerAndRespondsToShot` from feature head `e2b5127bda196ca7c0fc007824ca314876ec1fef` (which contains the repaired `TreeDamageService`-based fixture). Request commit: `2d6d083f94760f377466044582eadeb20a2d0ffd`; workflow: `32928451972`.

## Result
**Failed after executing exactly one test.** The fixture compiled and loaded VoxelShowcase, but the assertion `No lower trunk from the authored tree population is visible from the saved camera pose` failed. This is a selector failure, not a collision/damage failure: neither `OverlapsWoodAabb` nor `ApplyBlast` was reached.

## What was learned
The capture has no circle and therefore does not promise that a trunk midpoint is in-frame; a canopy or branch can be the visible evidence. Requiring a lower trunk in the frustum is stricter than the reported behavior and unlike the production shooting path, which sweeps semantic tree geometry including visible branch/leaf anchors.

## Next
Scan the saved viewport with `TreeDamageService.TrySweepImpact`, select the nearest semantic tree actually intersected by a captured-view ray, use that tree's lower trunk for the player collision assertion, and apply the shot blast at the captured-view hit point.
