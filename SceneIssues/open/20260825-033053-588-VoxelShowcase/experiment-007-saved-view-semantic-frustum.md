# Experiment 007 — saved-view semantic frustum

## Hypothesis
Scanning the whole recorded camera viewport with the production semantic-tree sweep should intersect at least one of the trees implicated by the capture, allowing the exact saved view to verify both collision and shot damage.

## What was performed
Ran `VoxelEngine.CI.SceneIssue20260825033053TreeInteractionTests.CapturedViewTreeBlocksPlayerAndRespondsToShot` from feature head `0b35250a6e5427b20b732bacfb9a47d75488b188`. The assigned CI branch contained that feature commit and request commit `0b3251f629d865bcfd3f31d3da91b590492a56b4`; workflow run `32928802857` executed the PlayMode filter.

## Result
**Failed after executing exactly one test.** VoxelShowcase loaded successfully and logged `Procedural vegetation: worldgen published 36 semantic Showcase trees`, but the viewport scan failed with `No semantic tree geometry is intersected anywhere in the saved whole-frame camera view.` The player-overlap and damage assertions were therefore not reached. The generic real-player capture step had no profile configured for this filter and emitted no screenshot.

## What was learned
The assumption that the trees visible in the capture are among the 36 semantic castle-tree instances is disproven for this saved viewpoint. The semantic tree world exists, but none of its geometry occupies the recorded camera frustum, so further selector tuning would test the wrong population.

## Next
Trace every VoxelShowcase tree/vegetation presentation source, especially far-terrain or other non-semantic vegetation paths, and identify which source can render trees in the recorded view while remaining absent from player collision and shot damage.
