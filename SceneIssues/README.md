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
SceneIssues/open/<timestamp>-<scene>/
  issue.json
  screenshot-001.png
  screenshot-002.png
  screenshot-003.png
  ...
```

### Publish captures to the queue

Every new capture is saved beneath `SceneIssues/open/` and must be committed and pushed through
`master` before an agent can work on it. `origin/master:SceneIssues/open/` is the authoritative
intake queue. Never add a new capture on `fixes`, `fixes/agent-N`, or a CI request branch; those
branches may only modify captures that already exist on `origin/master`.

From an up-to-date local `master`, publish every newly saved capture with:

```sh
./push_scene_issues.sh
```

The command refuses to run off `master`, requires local and remote master to match, validates that
each new capture is open, stages only brand-new directories beneath `SceneIssues/open/`, creates one
intake commit, and pushes it to `origin/master`. It never folds edits to existing tickets or other
staged work into the intake commit.

Only `status: fixed` captures belong under `SceneIssues/closed/`. A blocked capture remains under
`SceneIssues/open/`; blocked is not a terminal queue state. Keep `issue.json.status` consistent with
folder membership so it remains a useful audit check rather than replacing the directory queue.

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

## Agent fixing process — persistent per-agent branches

The local coordinator assigns each open capture to one browser-agent slot. Each slot owns exactly
two persistent branches for its lifetime:

```text
fixes/agent-N
ci-test/fixes/agent-N
```

Agents reuse those branches across assignments and never create a branch per capture. An individual
agent works on exactly one assigned capture at a time; separate slots may work on separate captures
concurrently. The coordinator's atomic local registry is the claim authority, so an agent must not
self-select another open capture or continue into the queue after finishing its assignment.
The coordinator reads only `origin/master:SceneIssues/open/` and rejects completion from a worker
branch that introduces a capture ID absent from master.

When the coordinator assigns an agent a captured issue:

1. Fetch `origin` and use only the feature and CI branches named in the assignment. If the feature
   branch does not exist, create it from current `origin/master`. If it already exists, resume it
   without discarding unmerged work and bring it up to date with `origin/master` as needed. Never
   use the former shared `fixes` branch.
2. Work only on the capture named by the coordinator. Queue order and exclusive claims belong to
   the coordinator; do not select another open directory yourself.
3. Work on exactly that one open issue until it is fixed or concretely blocked. Read its note and inspect **all** of its screenshots and circled regions before changing code.
4. Replay the issue and step through every captured viewpoint. Confirm the failure in the marked regions and determine the smallest responsible subsystem before editing production code.
5. Add or extend a focused regression using the saved scene/pose fixture. For transient problems, use all captured frames that are materially different rather than arbitrarily choosing one screenshot.
6. Implement the smallest fix that resolves the reproduced problem without weakening coverage, performance budgets, or unrelated assertions.
7. Write an experiment file for every attempt as soon as it produces a result — see **Document every experiment** below. The numbered experiment files are what makes the three-attempt count in the next step objective rather than a matter of recollection.
8. **Three-attempt rule:** if the issue is still not solved after three genuine fix attempts, stop guessing in the full scene and build a bare-bones reproduction that isolates the failing behavior with the minimum geometry, data, systems, and configuration needed to reproduce it. Use that reproduction to understand the root cause before making more production changes. The reproduction may be checked in temporarily on the assigned `fixes/agent-N` branch and may run in the normal CI build so the behavior is repeatable while debugging. **Remove the temporary bare-bones reproduction and any CI-only wiring before merging the assigned branch to `master`.**
9. Run the new regression plus the smallest relevant existing test set. Coordinator-assigned remote
   agents use the push-triggered targeted-CI process in `AGENTS.md` and do not invoke Unity. A local
   Claude Code session follows the Unity-running rules in `CLAUDE.md`; it must never bypass
   `tools/unity-run.sh` or start a second editor unsafely.
10. Replay the original capture again after the fix. For multi-frame issues, check every recorded viewpoint and every marked region.
11. Commit the production/test fix on the assigned `fixes/agent-N` branch with a message that names the capture, for example `Fix scene issue 20260822-...: prevent far-field flicker`. This gives the fix a real commit SHA.
12. For a verified fix, update `issue.json`: set `status` to `fixed`, fill `resolvedUtc`, summarize the resolution in `resolutionSummary`, record the regression test in `regressionTest`, and put the production/test commit SHA from the previous step in `fixCommit`. Then move the entire capture directory from `SceneIssues/open/` to `SceneIssues/closed/`. If the issue is blocked, document the blocker and experiments but keep it in `open/`; blocked is not closed or complete.
13. Commit the issue update and open-to-closed move on the same assigned `fixes/agent-N` branch, for example `Resolve scene issue 20260822-...`. The extra bookkeeping commit is deliberate: it avoids inventing the SHA of a commit before that commit exists.
14. Push the assigned feature branch after the issue is reproduced, fixed, regressed,
    replay-verified, documented, and committed. The coordinator verifies the terminal `issue.json`
    directly from `SceneIssues/closed/` on that remote branch, including that the open path is gone
    and the fixed issue's `fixCommit` is on the branch.
    It also requires `ci/single-test` success on the assigned CI request branch and verifies that
    branch contains the recorded fix commit.
15. Stop after pushing the terminal bookkeeping and wait for the coordinator. It will assign the
    next capture to the same tab and branch. One PR may accumulate that agent's sequential fixes;
    do not create one PR/branch pair per capture.

## Document every experiment

An experiment is any attempt to learn something about the issue: a replay, a diagnostic build, a
targeted CI run, a probe, a hypothesis tested in code. **Every experiment gets its own Markdown
file in the issue's own capture directory**, whether it succeeded or failed:

```text
SceneIssues/open/<timestamp>-<scene>/
  issue.json
  screenshot-001.png
  experiment-001-gpu-boundary-ownership.md
  experiment-002-coarse-lod-phase.md
  ...
```

These files move together to the matching `SceneIssues/closed/<timestamp>-<scene>/` directory when
the issue is fixed. Historical evidence must not be split across open and closed paths.

Number them in the order they were run and give each a short slug naming the hypothesis. Keep
each file concise — a screenful, not an essay — and write it immediately after the experiment
finishes, while the result is still known. Each file states:

- **Hypothesis** — what was believed to be wrong, in one or two sentences.
- **What was performed** — the change, replay, or test that was actually run, and the source
  commit SHA it ran against so the run can be reproduced.
- **Result** — what actually came back. Cite the concrete evidence: test names and pass/fail,
  captured metrics, or the `verification-*.png` / `.txt` replay artifacts saved beside it.
- **What was learned** — the conclusion, stated as a verdict: hypothesis confirmed, disproven,
  or inconclusive and why.
- **Next** — what the result implies for the following experiment.

A disproven hypothesis is a required result, not a wasted file. Recording that a cause was ruled
out is what stops the next agent from re-running the same experiment, and it is the only durable
trace of the reasoning once the branch history has been squashed or the diagnostic wiring
removed. Never delete an experiment file after the fix lands; it stays with the capture as the
record of how the cause was found.

Save replay screenshots, logs, and telemetry produced by an experiment into the same capture
directory as `verification-<slug>.png` / `verification-<slug>.txt`, and reference them from the
experiment file. Include the source commit and any non-default configuration (for example
`gpu_cutover_disabled=1`) at the top of the text artifact.

The per-issue `resolutionSummary` in `issue.json` remains the terminal one-paragraph answer. The
experiment files are the working record behind it, and both are expected on a resolved issue.

The point of persistent agent branches plus coordinator-owned claims is to make visual cleanup a
safe parallel queue: each worker follows reproduce → inspect marked regions → test → fix → verify →
document → commit → resolve, then waits for its next exclusive assignment.
