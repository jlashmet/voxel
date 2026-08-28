# Experiment 005 — Saved-pose replay framing and clean evidence

## Observation
Run `33117731124` proved the corrected WorldBuilder bake path: it logged a cache miss, rebuilt/stored `ShowcaseWorld.bytes`, and the lamp behavioral regression passed. Its replay still could not be accepted as visual evidence.

## Competing explanations
1. The recorded camera transform was not actually applied in the standalone player.
2. The camera was applied, but the verification capture did not reproduce the recorded viewport/clean presentation.

## Discriminating evidence
The player logged `Replaying issue with 1 screenshot(s). Verified standalone frozen pose.`, so runtime verification matched the recorded camera transform/FOV. However the original capture is `1928x836` while `tools/showcase-player-capture.sh` hard-coded the replay player to `1600x900`; that changes horizontal framing at the same vertical FOV. The generated `verification-final.png` also contained the SceneIssue replay banner, the always-on FPS diagnostics HUD, and Unity's `Development Build` watermark. The script selected the final generic harness screenshot rather than a clean saved-pose evidence path.

## Decision / falsifier
Keep the product lamp geometry unchanged. Make standalone SceneIssue replay usable in a normal non-development player by having `SceneIssueCameraReplayHarness` read `-voxel-scene-issue` directly, launch at the capture's recorded dimensions, and suppress the FPS overlay for that explicit replay path. Require the player log to confirm `SCENEISSUE camera pinned` and reject verification images smaller than the captured resolution. Falsifier: the next exact-source artifact is still not clean, still does not reproduce the saved framing, or still shows the lamp detached once the target is judgeable.
