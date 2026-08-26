# SceneIssue workflow

`origin/master:SceneIssues/open/` is the intake queue. Folder membership is state: verified fixes
move to `SceneIssues/closed/`; blocked work stays open. Original screenshots remain unmodified,
while `issue.json` stores camera poses and normalized annotations.

## Capture and publish

In Play Mode, press `F8` or click **Flag issue**. Annotate each bad region, add frames for transient
problems with **Keep capturing**, then save. Replay with **Tools > Scene Issue Capture** and step
through every recorded pose.

New captures must reach `master` before assignment. From an up-to-date local `master`, run:

```sh
./push_scene_issues.sh
```

Never introduce a new capture on a worker or CI branch.

## Assignment and branches

The coordinator assigns one capture and two persistent refs to each slot:

```text
fixes/agent-N
ci-test/fixes/agent-N
```

Work only on the assigned capture. Fetch first; create the feature branch from current
`origin/master` or resume it without discarding work, then merge current master. Refresh before a
substantial new attempt and before final CI. Do not self-select another issue or modify another
capture. A branch with unmerged work from an earlier assignment cannot receive a new issue.

## Investigate and fix

1. Inspect every screenshot, frame, annotation, and note directly. Treat marked regions as separate
   defects until evidence proves a shared owner.
2. Replay every pose and identify the responsible runtime object, profile/material, coordinates,
   triangle/voxel, or ownership decision. A synthetic repro proves possibility, not that it caused
   the captured defect.
3. Record at least two plausible hypotheses and run the smallest experiment that distinguishes
   them. State what result would falsify the leading hypothesis.
4. After three genuine failed fix attempts, isolate the behavior in a minimal reproduction before
   changing production code again. Remove temporary repro wiring before promotion.
5. Add a focused behavioral regression that exercises the production computation. Source-text or
   string-presence assertions may supplement it but cannot be the sole regression for rendering,
   geometry, or performance.
6. Implement the smallest proven fix. For shared systems, identify affected consumers, test likely
   negative regressions, and quantify geometry/runtime cost against an explicit existing budget.
7. Replay every original pose and marked region after the fix.

## Keep evidence concise

Maintain one `plan.md`, normally no more than 500 words:

```text
Observed defect / acceptance criteria
Competing hypotheses
Next discriminator
Results and falsified hypotheses
Selected fix
Remaining gates
```

Update it after each material result and replace stale detail with a one-line conclusion. It must
identify the current commit, active hypothesis, next discriminator, and remaining gates.

Record each product experiment as `experiment-NNN-<slug>.md`, limited to a screenful: hypothesis,
action and source SHA, concrete result, verdict, and next step. Keep operational polling, queue, and
runner notes in one `ci-operations.md` instead of producing an experiment or commit per poll.

Store durable evidence beside the issue as `verification-<slug>.png|txt`. Do not rely on chat or a
remote artifact that will expire.

## Targeted CI

Commit and push production/test work to `fixes/agent-N`. Build the CI request commit directly on
the exact feature SHA, changing `.github/test-request.json` on the CI branch only, then force-update
`ci-test/fixes/agent-N` once. Use the smallest exact EditMode or PlayMode filter; replay requests may
name the assigned `scene_issue` and 20–60 `replay_seconds`.

Monitor `ci/single-test` for the exact request SHA. Never create another branch, PR, no-op commit,
custom/one-shot workflow, or permission probe to trigger it. Do not replace a known queued/running
request because of age or ordering. If no exact-SHA run exists after 30 minutes, issue at most one
replacement for the same source state. Let the shared workflow manage its content-fingerprinted
Showcase bake cache; do not bypass or clear it as a retry mechanism.

Inspect logs and artifacts on failure. Change production code only for a product failure. For an
infrastructure failure, wait for recovery and retry once. A failed, cancelled, or timed-out workflow
is diagnostic only and cannot satisfy a gate, even if it emitted screenshots.

## Closure gates

A fixed branch must have all of the following:

- the production/test commit pushed and named by `issue.json.fixCommit`;
- a focused behavioral regression with green exact-SHA targeted CI;
- all original poses replayed successfully;
- `verification-final.png` committed in the capture (it may be an exact-pose contact sheet);
- `status: fixed`, `resolvedUtc`, `resolutionSummary`, and `regressionTest` completed;
- the entire capture moved from `open/` to `closed/` in a separate bookkeeping commit; and
- no unrelated capture, `.github/test-request.json`, or custom workflow on the feature branch.

For subjective requests such as “looks bad,” “AAA quality,” or reference matching, tests are
necessary but insufficient: preserve before/after exact-pose evidence and obtain explicit human
approval before closure. If blocked, document the evidence and blocker, keep the issue open, and
wait.

## Batched promotion

Green CI and terminal bookkeeping make a branch ready; workers do not push master independently.
Keep the verified head unchanged while the coordinator collects a batch.

The designated promoter fetches and verifies every listed exact head, then merges all of them in an
isolated worktree based on current `origin/master`. If a head moved, a merge conflicts, or master
advanced, stop and report it. Otherwise push the integrated HEAD to `origin/master` once with a
normal non-force update and no intermediate ref.

The assignment is complete only when remote master contains both fix and bookkeeping commits, the
capture exists only under `closed/`, and required CI is green. Then wait for the coordinator; do not
start another capture.
