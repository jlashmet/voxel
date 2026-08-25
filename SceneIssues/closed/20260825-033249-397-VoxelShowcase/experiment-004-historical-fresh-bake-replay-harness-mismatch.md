# Experiment 004 — Historical fresh bake replay harness mismatch

## Hypothesis

Replay the exact capture-time production source with a newly generated VoxelShowcase startup bake. If the floating tower remains, the defect is in historical source geometry; if it disappears, the captured frame likely came from a stale checked-in startup bake or a later source/bake mismatch.

## What performed + source commit

Re-ran GitHub Actions run `32889869053` after the concurrent Unity editor blocker from experiment 003 cleared. The workflow checked out historical source commit `760dc909138088a46778f026501c17dd25f1b86d`, restored this capture fixture from the trigger commit, and invoked `VoxelEngine.Showcase.Editor.ShowcaseWorldBaker.BakeShowcaseWorld` to regenerate `Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes` from that historical source.

Latest job: `97943516360`.

The fresh bake completed successfully in 198 seconds. The subsequent exact-pose replay attempted to call the historical checkout's `tools/showcase-player-capture.sh` with `--scene-issue`, but that script version predates the `--scene-issue` option and exited with code 2 before launching the player. The artifact was still uploaded as `sceneissue-033249-capture-source-32889869053`, artifact ID `9579656058`, digest `sha256:8c28b023c4a8f7ebe36848469fa4d3d5108562cd5a5cc2117b26ae260f535b4e`, but it contains no replay screenshots.

## Result

Inconclusive for the visual defect. The experiment does establish that the capture-time source can still be freshly baked successfully on the current runner, so the earlier editor-contention blocker is resolved. The failure is isolated to a diagnostic harness/version mismatch after the bake: `ERROR: unknown argument '--scene-issue'`.

## What learned

The historical production source and fresh-bake path are testable; no concurrency or bake failure remains. To complete the A/B without changing historical production behavior, the workflow must overlay only the current replay tooling that understands the saved SceneIssue camera fixture, while continuing to compile/bake the historical source itself.

## Next

Update the temporary capture-specific diagnostic workflow to overlay the current replay script/tooling before the player step, rerun the same historical source + fresh bake + exact saved-pose replay, then inspect every replay frame against the untouched original screenshot. Remove the temporary diagnostic workflow before the final production/test commit.
