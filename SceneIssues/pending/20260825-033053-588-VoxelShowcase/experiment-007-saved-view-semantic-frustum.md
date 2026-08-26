# Experiment 007 — saved-view sampled semantic rays

## Hypothesis
Scanning the recorded camera viewport with production semantic-tree sweeps should intersect at least one of the trees implicated by the capture, allowing the saved view to verify both collision and shot damage.

## What was performed
Ran `VoxelEngine.CI.SceneIssue20260825033053TreeInteractionTests.CapturedViewTreeBlocksPlayerAndRespondsToShot` from feature head `0b35250a6e5427b20b732bacfb9a47d75488b188`. The assigned CI branch contained that feature commit and request commit `0b3251f629d865bcfd3f31d3da91b590492a56b4`; workflow run `32928802857` executed the PlayMode filter. The fixture sampled a 19x13 viewport grid, sweeping each ray with a 0.28 m radius to a maximum range of 250 m.

## Result
**Failed after executing exactly one test.** VoxelShowcase loaded successfully and logged `Procedural vegetation: worldgen published 36 semantic Showcase trees`, but none of the sampled 250 m sweeps hit semantic tree geometry. The player-overlap and damage assertions were therefore not reached. The generic real-player capture step had no profile configured for this filter and emitted no screenshot.

## What was learned
**Inconclusive about which tree population is visible.** This experiment rules out only the sampled rays within 250 m; it does not prove that no semantic tree intersects the full 16 km camera frustum. The 19x13 grid is also much coarser angularly than a distant branch/trunk, so visible semantic geometry can fall between samples. Treating this as proof of a different tree system would overstate the evidence.

## Next
Synchronize with current `master` because it already contains the related rooted-tree shooting/presentation fix, then make the capture replay select visible semantic branch geometry directly from generated branch bounds against the saved camera frustum instead of relying on sparse ray samples.
