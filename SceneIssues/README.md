# Scene issue captures

This directory is intentionally outside `Assets/` so screenshots and JSON fixtures are visible to source control and coding agents without Unity importing every PNG.

## Capture a problem

1. Run any scene in Play Mode.
2. Move to the bad view.
3. Click **Flag issue [F8]** in the upper-right corner, or press `F8`.
4. The tool freezes the current camera viewpoint, captures a clean screenshot before any annotation UI is drawn, and pauses the game.
5. Describe what is wrong and click **Save issue**.

A capture is written to:

```text
SceneIssues/<timestamp>-<scene>/
  issue.json
  screenshot.png
```

`issue.json` contains the scene path, camera hierarchy path, exact world-space camera position and quaternion rotation, projection settings, conservative movement/pose-anchor transform, screen size, frame/time information, Unity version, and the note.

## Replay a problem

Use either:

- **Tools > Scene Issue Capture > Replay Latest Capture**
- **Tools > Scene Issue Capture > Replay Capture...**

Unity opens the recorded scene, enters Play Mode, waits for an active camera, moves the recorded movement/pose anchor (when it can be safely resolved) and camera to the recorded pose, and pins the camera there. The issue note is shown on screen. Click **Release camera** when you want normal camera control again.

## Regression-test workflow

The original screenshot is evidence of the bug, not an expected golden image. After a fix:

1. Replay the capture to verify the same scene and viewpoint no longer show the defect.
2. Reuse `issue.json` as the deterministic scene/pose fixture for a focused PlayMode or rendering regression.
3. If pixel comparison is appropriate, capture the *fixed* view as the expected baseline; do not use the broken screenshot as the golden image.

Keeping the pose fixture separate from the expected assertion lets a captured visual bug become a geometry/material/streaming assertion when that is more stable than raw image comparison.
