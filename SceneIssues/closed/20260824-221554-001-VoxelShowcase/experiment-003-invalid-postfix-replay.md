# Experiment 003 — Invalid post-fix replay

## Hypothesis

The integrated source fix at `d9e0d893795147cfcd9580495358dd173ee77176` would remove the grass-textured stair treads when replayed through the standalone-player verification path.

## What was performed

CI run `32929118687` on `ci-test/fixes/agent-3` built `VoxelShowcase` and captured presented frames at the issue's recorded 1364×836 resolution. The workflow reused the checked-in `Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes` rather than rebaking the startup world from the integrated source.

## Result

**Invalid verification.** The workflow completed successfully, but the final screenshot still showed green/grass-textured stair treads. A later terminal audit corrected one detail in the original write-up: `issue.json` confirms the assigned camera itself is approximately `(136.32, 25.55, 58.98)` with FOV 70, so that reported pose was not the invalid part of this experiment. The invalidating condition was the stale baked startup-world asset: the production change affects world-generation material ownership, and this run did not regenerate the baked world before launching the player. The workflow also did not use the repository's standard `--scene-issue` replay argument, so exact fixture provenance was weaker than the later verification path.

## What was learned

The green screenshot cannot be used to reject the source fix because the player rendered stale generated world data. The camera-coordinate mismatch stated in the first version of this experiment was incorrect and is superseded by the capture metadata in `issue.json`.

## Next

Rebuild `ShowcaseWorld.bytes` from the integrated source and invoke `showcase-player-capture.sh --scene-issue SceneIssues/open/20260824-221554-001-VoxelShowcase/issue.json`, then inspect the resulting screenshot before closing the issue.
