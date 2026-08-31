# SceneIssues workflow

`SceneIssues` tracks captured defects and acceptance-driven features.

Use the assignment-specific guide:
- `SceneIssues/issue-readme.md` — capture-driven defects.
- `SceneIssues/feature-readme.md` — feature work.

`AGENTS.md` and this file define common rules.

## Queue state

`origin/master` is authoritative:
- `open/` — available or active work.
- `closed/` — completed work on master.

Keep blocked or unfinished work in `open/`. Do not invent intermediate queue states.

## Assignment and branches

Each worker uses:

```text
fixes/agent-N
ci-test/fixes/agent-N
```

Work only on the assigned task. Fetch first; create the feature branch from current `origin/master` or resume valid work. Merge current master when compatibility requires it and always before final validation/promotion. Do not self-select work or modify another SceneIssue.

For captured defects, publish new captures to `master` before assignment; never introduce them on worker or CI branches.

## Targeted CI

Commit production/test work to `fixes/agent-N`. `ci-test/fixes/agent-N` is that worker's only targeted-CI transport. Build each request from the exact feature SHA; change `.github/test-request.json` only on the CI branch.

Never replace queued/running CI. After a completed failure, inspect evidence, fix the product failure or retry proven infrastructure failure, then reuse the same transport. Do not create alternate CI branches, PRs, no-op commits, custom workflows, or permission probes.

The explicit request remains the exact-SHA trigger and fast targeted regression. For production diffs, CI additionally derives module validation from `*.module-validation.json` metadata: affected focused tests, affected module-local built-player scene/scenario validation, and the canonical built-player `KentridgePlayableSlice` integration gate. Agents must not manually enumerate those automatically required module/player targets. Required zero-match tests, missing scenes/scenarios, missing captures, skipped player targets, or failed required artifact proof are failures.

Use the smallest regression that proves the change. Player-visible acceptance requires exact-SHA built-player validation and durable evidence; editor-only tests are supplemental. PlayMode screenshots or RenderTextures are not visual acceptance evidence.

## Completion and merge

Before closure, satisfy every acceptance criterion and required checklist item. Complete `resolutionSummary`, `regressionTest`, and `fixCommit` when supported by the verified result.

After all required exact-SHA gates pass, move only the assigned task from `open/` to `closed/`, set `status: fixed` and `resolvedUtc`, and commit the bookkeeping on the feature branch.

Fetch current `origin/master`, merge it into the feature branch, resolve only in-scope conflicts, push the branch, then push that exact head to `origin/master` non-force. If master advances, fetch, merge, revalidate affected work as needed, and retry. Never force-push master.