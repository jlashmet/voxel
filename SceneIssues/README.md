# SceneIssues workflow

`SceneIssues` is the shared work queue for both captured scene defects and acceptance-driven feature work.

Use the specific guide for the assignment:

- `SceneIssues/issue-readme.md` — capture-driven defects in an existing scene.
- `SceneIssues/feature-readme.md` — regular feature work tracked through the SceneIssues queue.

`AGENTS.md` and this file contain the common rules. The work-specific guide adds investigation, planning, validation, and evidence requirements.

## Queue state

The folders on `origin/master` are authoritative:

- `open/`: available or actively assigned;
- `pending/`: implementation/required pre-closure work complete and awaiting final verified closure/promotion; and
- `closed/`: completed work on master.

Blocked work stays open unless the applicable workflow explicitly permits pending. Never move an assignment backward merely because a prompt names an older path.

## Assignment and branches

The coordinator assigns one task and two persistent refs to each slot:

```text
fixes/agent-N
ci-test/fixes/agent-N
```

Work only on the assigned task. Fetch first; create the feature branch from current `origin/master` or resume it without discarding valid work. Refresh/merge current master before a substantial new attempt and again before final promotion. Do not self-select another assignment or modify another SceneIssue.

For captured defects, new captures must reach `master` before assignment; never introduce a new capture on a worker or CI branch.

## Targeted CI

Commit and push production/test work to `fixes/agent-N`. `ci-test/fixes/agent-N` is the only targeted-CI transport for that worker. Build request commits from the exact feature SHA and change `.github/test-request.json` only on the CI branch.

Never replace queued/running CI. After a completed failed/cancelled/timed-out request, inspect the run/artifact, fix a product failure (or retry a proven infrastructure failure), then reuse the same assigned CI transport for the next exact-SHA request. Do not create extra CI branches, PRs, no-op commits, custom workflows, or permission probes as alternate transports.

Use the smallest focused regression that proves the change. When acceptance involves a scene, rendering, traversal, interaction, or other player-visible behavior, final validation must build/launch the real application/player on the exact feature SHA and produce durable evidence; editor-only green tests are supplemental.

## Completion and merge

Before pending/closure, satisfy every acceptance criterion and required checklist item from the applicable work-specific guide. Complete `resolutionSummary`, `regressionTest`, and `fixCommit` when the implementation/verification state supports them.

After all required exact-SHA gates are green, move only the assigned task from `pending/` to `closed/`, set `status: fixed` and `resolvedUtc`, and commit the final bookkeeping on the feature branch. Do not create a review branch or pull request.

Fetch current `origin/master`, merge it into the feature branch, resolve only in-scope conflicts, push the feature branch, verify its exact head, then push that exact head to `origin/master` non-force. If master advances, fetch, merge, revalidate affected work as needed, and retry. Never force-push master.
