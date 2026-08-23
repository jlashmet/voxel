# Scene issue captures

This directory is intentionally outside `Assets/` so screenshots and JSON fixtures are visible to source control and coding agents without Unity importing every PNG.

## Capture a problem

1. Run any scene in Play Mode.
2. Move to the bad view.
3. Click **Flag issue [F8]** in the upper-right corner, or press `F8`.
4. The tool freezes the current camera viewpoint, captures a clean screenshot, pauses the game, and opens the issue note.
5. Describe what is wrong.
6. For a one-frame problem, click **Save issue**.
7. For flicker, popping, transient holes, LOD transitions, or anything that needs more evidence, click **Keep capturing**. The scene resumes and the issue session stays active.
8. Press `F8` whenever another useful bad frame appears. Each press adds a screenshot to the same issue without ending the session.
9. Click **Finish issue** when done, adjust the note if needed, then click **Save issue**.

A multi-frame capture is written to:

```text
SceneIssues/<timestamp>-<scene>/
  issue.json
  screenshot-001.png
  screenshot-002.png
  screenshot-003.png
  ...
```

Every screenshot has its own entry in `issue.json`, including its exact world-space camera position and quaternion rotation, conservative movement/pose-anchor transform, screen size, Unity frame number, and time-since-scene-load. The issue-level record contains the scene path, Unity version, platform, note, and resolution bookkeeping.

`formatVersion: 2` uses the `captures[]` array. The first frame is also mirrored into the old single-frame fields so version-1 replay/test helpers do not break immediately.

## Replay a problem

Use either:

- **Tools > Scene Issue Capture > Replay Latest Capture**
- **Tools > Scene Issue Capture > Replay Capture...**

Unity opens the recorded scene, enters Play Mode, waits for an active camera, and pins the camera to the first recorded viewpoint. For a multi-frame issue, use **Previous** and **Next** in the replay banner to move through every captured viewpoint. The issue note stays visible. Click **Release camera** when you want normal camera control again.

The screenshots are evidence of what was seen. The replay poses are the reproducible fixture.

## Regression-test workflow

The original screenshots are evidence of the bug, not expected golden images. After a fix:

1. Replay every captured frame and verify the same scene/viewpoints no longer show the defect.
2. Reuse the relevant `captures[]` entries from `issue.json` as deterministic scene/pose fixtures for focused PlayMode or rendering regressions.
3. Prefer a direct geometry/material/streaming/state assertion when it is more stable than pixel comparison.
4. If pixel comparison is appropriate, capture the *fixed* view as the expected baseline; never use the broken screenshot as the golden image.
5. Keep all original screenshots with the issue after it is resolved so the regression has historical evidence of the failure it guards.

## Agent fixing process — one `fixes` branch

Captured scene issues are intentionally fixed **one at a time on one shared branch named `fixes`**. Do not create a branch per issue.

When an agent is asked to work through captured issues:

1. Start from current `master`. If `fixes` does not exist, create `fixes` from current `master`. If it already exists, continue on that branch and bring it up to date with `master` only as needed.
2. Find the open captures under `SceneIssues/`. Unless the developer gives a different priority, work oldest open issue first.
3. Work on exactly one issue at a time. Read its note and inspect **all** of its screenshots before changing code.
4. Replay the issue and step through every captured viewpoint. Confirm the failure and determine the smallest responsible subsystem before editing production code.
5. Add or extend a focused regression using the saved scene/pose fixture. For transient problems, use all captured frames that are materially different rather than arbitrarily choosing one screenshot.
6. Implement the smallest fix that resolves the reproduced problem without weakening coverage, performance budgets, or unrelated assertions.
7. Run the new regression plus the smallest relevant existing test set. Follow the repository's Unity-running rules in `CLAUDE.md`; never bypass `tools/unity-run.sh` or start a second editor unsafely.
8. Replay the original capture again after the fix. For multi-frame issues, check every recorded viewpoint.
9. Update that issue's `issue.json`: set `status` to `fixed`, fill `resolvedUtc`, summarize the resolution in `resolutionSummary`, record the regression test in `regressionTest`, and record the fixing commit in `fixCommit` once known. If the issue cannot be reproduced or is blocked, record that explicitly instead of pretending it is fixed.
10. Commit the issue fix on `fixes` with a message that names the capture, for example `Fix scene issue 20260822-...: prevent far-field flicker`.
11. Only after that issue is reproduced, fixed, regressed, replay-verified, documented, and committed should the agent move to the next open issue — **still on the same `fixes` branch**.
12. Push `fixes` after each completed issue so each fix remains an inspectable commit. A single PR can accumulate the sequential fixes; do not create one PR/branch pair per capture unless the developer explicitly changes this policy.

The point of the shared branch is to make visual cleanup an ordered queue: reproduce → test → fix → verify → document → commit, then advance to the next issue.