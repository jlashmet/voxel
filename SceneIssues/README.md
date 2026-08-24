# Scene issue captures

This directory is intentionally outside `Assets/` so screenshots and JSON fixtures are visible to source control and coding agents without Unity importing every PNG.

## Capture a problem

1. Run any scene in Play Mode.
2. Move to the bad view.
3. Click **Flag issue [F8]** in the upper-right corner, or press `F8`.
4. The tool freezes the current camera viewpoint, captures a clean screenshot, pauses the game, and opens the annotation UI.
5. Drag directly on the screenshot to draw one or more circles around the bad area. Right-click removes the nearest circle; **Clear circles** removes all circles from the current frame.
6. Describe what is wrong in the issue text box.
7. For a one-frame problem, click **Save issue**.
8. For flicker, popping, transient holes, LOD transitions, or anything that needs more evidence, click **Keep capturing**. The scene resumes and the issue session stays active.
9. Press `F8` whenever another useful bad frame appears. Each press adds a screenshot to the same issue and opens that new frame for its own circle annotations.
10. Use **Previous** / **Next** in the annotation UI to review or edit circles on any captured frame.
11. Click **Finish issue** when done, review the frames/note if needed, then click **Save issue**.

A multi-frame capture is written to:

```text
SceneIssues/<timestamp>-<scene>/
  issue.json
  screenshot-001.png
  screenshot-002.png
  screenshot-003.png
  ...
```

The PNGs remain clean, unmodified captures. The circles are stored as structured normalized screen-space data in the matching frame inside `issue.json`. This keeps the original evidence suitable for visual regression work while still preserving exactly which region the developer was pointing at.

Every screenshot has its own entry in `issue.json`, including its exact world-space camera position and quaternion rotation, conservative movement/pose-anchor transform, screen size, Unity frame number, time-since-scene-load, and zero or more `circles[]` entries.

`formatVersion: 3` uses the `captures[]` array plus per-frame circle annotations. The first frame is also mirrored into the older single-frame fields so existing replay/test helpers can continue to consume a one-frame fixture.

## Replay a problem

Use either:

- **Tools > Scene Issue Capture > Replay Latest Capture**
- **Tools > Scene Issue Capture > Replay Capture...**

Unity opens the recorded scene, enters Play Mode, waits for an active camera, and pins the camera to the first recorded viewpoint. For a multi-frame issue, use **Previous** and **Next** in the replay banner to move through every captured viewpoint. The circles recorded for the selected frame are drawn back over the game view, so the fixing agent can immediately see the exact region that was reported. The issue note stays visible. Click **Release camera** when you want normal camera control again.

The screenshots are evidence of what was seen. The replay poses are the reproducible fixture. The circles identify the intended problem region without mutating the original screenshots.

## Regression-test workflow

The original screenshots are evidence of the bug, not expected golden images. After a fix:

1. Replay every captured frame and verify the same scene/viewpoints no longer show the defect, paying particular attention to every circled region.
2. Reuse the relevant `captures[]` entries from `issue.json` as deterministic scene/pose fixtures for focused PlayMode or rendering regressions.
3. Prefer a direct geometry/material/streaming/state assertion when it is more stable than pixel comparison.
4. If pixel comparison is appropriate, capture the *fixed* view as the expected baseline; never use the broken screenshot as the golden image.
5. Keep all original screenshots and circle annotations with the issue after it is resolved so the regression has historical evidence of the failure it guards.

## Agent fixing process — one claimed branch per issue

Several agents work this queue at once. A captured issue is claimed by **creating its branch on `origin`**; the branch list is the claim registry. There is no shared `fixes` branch.

### Branch names

Both names are derived mechanically from the `id` field in `issue.json`, verbatim, with nothing appended:

```text
sceneissue/<id>            the work,  e.g. sceneissue/20260823-013924-433-VoxelShowcase
ci-test/sceneissue/<id>    the CI request branch, force-reset each iteration
```

A slug, a `-v2`, a `-repro`, or a date fragment on the end breaks claim detection — "is this taken?" stops being an exact-match lookup. `AGENTS.md` branch discipline applies unchanged: exactly these two refs for the issue's whole lifetime.

### Claiming

1. List the open captures: every `SceneIssues/*/issue.json` whose `status` is `open`.
2. List the active claims: `git ls-remote --heads origin 'refs/heads/sceneissue/*'`. Connector-only agents read the same refs through the API.
3. **Check for a cluster first.** Read every other open issue captured in the same scene within a few minutes of your candidate. Captures from one walkthrough session frequently share a single root cause. If they plausibly do, claim them as a group under the **oldest** id and resolve all of their `issue.json` files on that one branch. Do not let two agents rediscover one bug in parallel — the self-hosted runner is shared, and duplicated investigation delays the whole fleet.
4. Take the oldest open issue (or cluster) with no matching claim ref. Claim it by creating `refs/heads/sceneissue/<id>` at the current `master` commit. Creating a ref fails if it already exists — if it does, another agent won; take the next candidate.
5. Re-check the claim list immediately before your first CI request. A duplicate claim costs the whole fleet a runner slot, not just you.

### Fixing

Work exactly one issue or cluster per branch.

1. Read its note and inspect **all** of its screenshots and circled regions before changing code.
2. Replay the issue and step through every captured viewpoint. Confirm the failure in the marked regions and determine the smallest responsible subsystem before editing production code.
3. Add or extend a focused regression using the saved scene/pose fixture. For transient problems, use all captured frames that are materially different rather than arbitrarily choosing one screenshot.
4. Implement the smallest fix that resolves the reproduced problem without weakening coverage, performance budgets, or unrelated assertions.
5. **Three-attempt rule:** if the issue is still not solved after three genuine fix attempts, stop guessing in the full scene and build a bare-bones reproduction that isolates the failing behavior with the minimum geometry, data, systems, and configuration needed to reproduce it. Use that reproduction to understand the root cause before making more production changes. The reproduction is checked in temporarily **on this issue's own branch — never on a new one** — and may run in the normal CI build so the behavior is repeatable while debugging. **Remove the temporary bare-bones reproduction and any CI-only wiring for it before merging.**
6. Validate. If you can run Unity locally, follow `CLAUDE.md` and never bypass `tools/unity-run.sh`. If you can only push and read the API, follow the `AGENTS.md` validation loop on `ci-test/sceneissue/<id>`.
7. Replay the original capture again after the fix. For multi-frame issues, check every recorded viewpoint and every marked region.
8. Commit the production/test fix with a message that names the capture, for example `Fix scene issue 20260822-...: prevent far-field flicker`. This gives the fix a real commit SHA.
9. Update that issue's `issue.json`: set `status` to `fixed`, fill `resolvedUtc`, summarize the resolution in `resolutionSummary`, record the regression test in `regressionTest`, and put the production/test commit SHA from the previous step in `fixCommit`. If the issue cannot be reproduced or is blocked, record that explicitly instead of pretending it is fixed. For a cluster, do this for every issue the fix resolves.
10. Commit that issue bookkeeping separately, for example `Resolve scene issue 20260822-...`. The extra bookkeeping commit is deliberate: it avoids inventing the SHA of a commit before that commit exists.
11. Rebase on current `master`, then open **one PR per issue or cluster**. `issue.json` files never conflict between agents because each lives in its own directory; production code can, and the agent that hits a conflict resolves it.
12. After the PR merges, delete both `sceneissue/<id>` and `ci-test/sceneissue/<id>` from `origin`. Leaving them behind keeps the issue looking claimed.

Only take a new claim once the current one is reproduced, fixed, regressed, replay-verified, documented, and merged.

### Sharing one runner

The self-hosted runner executes one job at a time for the entire fleet, so a queued CI request delays every other agent. Do not push a CI request to check a guess — push it to check a hypothesis you have already reasoned through, and prefer one request that validates the fix and its regression together over two sequential ones.

The point of the queue is unchanged, only its width: claim → reproduce → inspect marked regions → test → fix → verify → document → merge → release the claim.
