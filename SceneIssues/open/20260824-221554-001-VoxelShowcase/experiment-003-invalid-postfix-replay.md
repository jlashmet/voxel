# Experiment 003 — Invalid post-fix replay

## Hypothesis

The integrated source fix at `d9e0d893795147cfcd9580495358dd173ee77176` would remove the grass-textured stair treads when replayed through the existing standalone-player verification path.

## What was performed

CI run `32929118687` on `ci-test/fixes/agent-3` built `VoxelShowcase` and captured presented frames at the issue's recorded 1364×836 resolution. The workflow intended to replay the assigned camera and reused the checked-in `Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes`.

## Result

**Invalid verification.** The workflow completed successfully, but the final screenshot still showed green/grass-textured stair treads. More importantly, `player-run.log` reported `SCENEISSUE camera pinned at (136.32, 25.55, 58.98) fov=70`, which does not match the assigned capture at approximately `(-20.98, 13.50, 13.99)` with FOV 60. Inspection of `tools/showcase-player-capture.sh` showed that exact issue replay requires its `--scene-issue` argument; the workflow had instead written an unused resource fixture. The run also did not rebake the startup world from the integrated source.

## What was learned

The green screenshot cannot be used to accept or reject the production fix because this run verified neither the recorded pose nor freshly baked world data. A green workflow result is only operational success; visual verification must check the emitted camera coordinates and rendered evidence.

## Next

Rebuild `ShowcaseWorld.bytes` from the integrated source, invoke `showcase-player-capture.sh --scene-issue SceneIssues/open/20260824-221554-001-VoxelShowcase/issue.json`, verify the log reports the saved camera/FOV, and inspect the resulting screenshot before closing the issue.
