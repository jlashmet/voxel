# SceneIssue workflow

The folders on `origin/master` are the queue state:

- `open/`: available or actively assigned;
- `pending/`: verified fix awaiting human review; and
- `closed/`: human-approved fix.

Blocked work stays open. Original screenshots remain unmodified; `issue.json` stores exact camera
poses and normalized annotations.

## Capture and publish

In Play Mode, press `F8` or click **Flag issue**. Annotate each bad region, add frames for transient
problems with **Keep capturing**, then save. Replay with **Tools > Scene Issue Capture** and inspect
every recorded pose.

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
substantial new attempt and final CI. Do not self-select another issue or modify another capture.

After the verified pending state reaches master, the coordinator authorizes one additional,
task-specific review ref:

```text
review/scene-issue/<capture-id>
```

That branch may only move its capture from `pending/` to `closed/` and update review bookkeeping.
It must never contain production code, tests, CI requests, or workflows.

## Investigate and fix

1. Inspect every screenshot, frame, annotation, and note directly. Treat marked regions as separate
   defects until evidence proves a shared owner.
2. Replay every pose and identify the responsible runtime object, profile/material, coordinates,
   triangle/voxel, or ownership decision. A synthetic repro proves possibility, not causality.
3. Record at least two plausible hypotheses and run the smallest discriminating experiment. State
   what would falsify the leading hypothesis.
4. After three genuine failed fix attempts, isolate the behavior in a minimal reproduction before
   changing production code again. Remove temporary wiring before promotion.
5. Add a focused behavioral regression through the production computation. Source-string checks
   may supplement it but cannot be the sole rendering, geometry, or performance regression.
6. Implement the smallest proven fix. For shared systems, identify affected consumers, test likely
   negative regressions, and quantify cost against an existing budget.
7. Replay every original pose and marked region after the fix.

## Keep evidence concise

Maintain one `plan.md`, normally no more than 500 words: observed defect and acceptance criteria;
competing hypotheses; next discriminator; material results; selected fix; current commit; and
remaining gates. Replace stale detail with a one-line conclusion rather than growing a diary.

Record each product experiment as `experiment-NNN-<slug>.md`, limited to a screenful: hypothesis,
action and source SHA, result, verdict, and next step. Put polling, queue, and runner notes in one
`ci-operations.md`. Store durable evidence beside the issue as `verification-<slug>.png|txt`.

## Targeted CI

Commit and push production/test work to `fixes/agent-N`. Build the request commit directly on the
exact feature SHA, changing `.github/test-request.json` on the CI branch only, then force-update
`ci-test/fixes/agent-N` once. Use the smallest exact EditMode or PlayMode filter; replay requests may
name the assigned `SceneIssues/open|pending|closed/<id>/issue.json` and 20–60 `replay_seconds`.

Monitor `ci/single-test` for the exact request SHA. Never create another branch, PR, no-op commit,
custom workflow, or permission probe to trigger it. Leave queued/running requests alone. If no
exact-SHA run exists after 30 minutes, issue at most one replacement for the same source state. Let
the shared workflow manage its Showcase bake cache.

Inspect logs and artifacts on failure. Change production code only for a product failure. For an
infrastructure failure, wait and retry once. Failed, cancelled, or timed-out workflows are
diagnostic only and cannot satisfy a gate.

## Submit for review

A feature branch is ready for pending promotion only when it has:

- the pushed production/test commit named by `issue.json.fixCommit`;
- a focused behavioral regression with green exact-SHA targeted CI;
- every original pose replayed successfully;
- `verification-final.png` committed in the capture;
- `status: pending`, `resolutionSummary`, `regressionTest`, and `fixCommit` completed;
- the entire capture moved from `open/` to `pending/` in a separate bookkeeping commit; and
- no unrelated capture, CI request file, or workflow in the feature-only diff.

Do not set `resolvedUtc` yet. Green CI and pending bookkeeping make the branch ready; workers do not
push master independently. The coordinator batches ready branches, designates one promoter, and
advances master once. The promoter verifies every exact head and uses an isolated worktree from
current `origin/master`; moved heads, conflicts, or an advancing master stop the batch.

Once the pending capture is on master, create `review/scene-issue/<capture-id>` from current master.
Move only that capture from `pending/` to `closed/`, set `status: fixed` and `resolvedUtc`, then open
a non-draft PR titled `Approve SceneIssue <capture-id>`. Include the original and
`verification-final.png` evidence in its body. Do not merge it yourself.

The coordinator releases the worker for its next ticket after it verifies the clean review branch
and open PR. The PR merge is human approval and moves the capture to `closed/`. Bookkeeping-only
review PRs and their merges skip Unity CI because the fix was already tested before pending
promotion.

If review is rejected, leave the PR unmerged and return the capture to `open/` before reassigning
it. A PR comment alone does not put it back in the worker queue.
