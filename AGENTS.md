Voxel Development Skill
Use this skill whenever working on **`jlashmet/voxel`**.

## Instructions

- Read the repository root **`CLAUDE.md`** before doing work.
- Treat its architectural constraints and referenced specs as binding.
- Inspect the relevant implementation, tests, and plans before making changes.
- Prefer fixing proven causes and invariants over speculative changes.

## Planning

If a task is more than a trivial one-step edit, create or update a durable Markdown plan before
implementation begins. Keep it next to the work it documents: scene-issue plans and experiment
notes live in their capture directory under `SceneIssues/open/` while active and move with the
capture to `SceneIssues/closed/` when fixed — see `SceneIssues/README.md`.

- Resume an existing relevant plan instead of creating a duplicate.
- State the goal, scope, important constraints, and concrete acceptance criteria.
- Break the work into a checkable task list using Markdown checkboxes.
- Check items off as they are completed and keep the plan current while work is in progress.
- Record material findings, failed hypotheses, blockers, and validation evidence so another agent can resume the work without reconstructing the investigation from chat history.
- If the implementation direction changes, update the plan before continuing so it remains the authoritative record of the work.
- A tiny edit that can be implemented and validated in one straightforward step does not require a separate plan.

## Branch discipline

**Scene issues use coordinator-assigned agent branches.** When the local scene-issue coordinator
assigns a capture, each agent slot has exactly one persistent feature branch and one persistent CI
request branch:

```
fixes/agent-N
ci-test/fixes/agent-N
```

The coordinator supplies the concrete `N`. Reuse those same two branches for every capture handled
by that agent; never create a branch per capture, and never push to the former shared `fixes` branch.
An agent works on only its assigned capture until it reaches a documented terminal state, although
different agent slots may handle different captures concurrently. Read `SceneIssues/README.md` and
follow its fixing process. Everything below about reuse and forbidden suffixes still applies.

**Keep long-lived feature branches current with master.** Fetch `origin` and merge current
`origin/master` into the assigned feature branch periodically while work is in progress, not only at
the final promotion step. At minimum, refresh at assignment/task start, before beginning a new
substantial implementation attempt after other work may have landed, before the final targeted-CI
request, and immediately before promotion to `master`. Preserve both sides of the history: use a
normal merge/fast-forward as appropriate, never discard unmerged feature work, and never force-push
the feature branch merely to catch up with master. If a master merge changes production code, tests,
scene data, or another input relevant to the behavior under test, treat the integrated feature head
as a new testable state and rerun the affected targeted CI before relying on older green evidence.

**Queue admission is master-first.** Every capture directory must already exist on `origin/master`
under `SceneIssues/open/` before a fixing agent may claim or edit it. New
`SceneIssues/open/<capture>/` directories are intake and must be committed and pushed through
`master`; never introduce a new capture on `fixes/agent-N`, `ci-test/fixes/agent-N`, or the retired
shared `fixes` branch. If an assigned directory is absent from `origin/master`, stop and report the
invalid assignment.

Folder membership is queue state. Only a verified fixed issue moves from `SceneIssues/open/` to
`SceneIssues/closed/`, in the separate bookkeeping commit after the production/test fix commit.
Blocked work stays in `open/`; it is neither closed nor complete.

For every other task, a task uses **exactly two branches for its entire lifetime**: one feature
branch and one CI request branch. Both names are fixed when the task starts and never change.

```
<feature-branch>          the work
ci-test/<feature-branch>  the CI request branch, force-reset each iteration
```

Iterating means **force-updating these two refs, never creating a third**. Do not create a new
branch to retry a test, try a variant, capture a baseline, or hold evidence. Suffixes such as
`-v2`, `-latest`, `-retry`, `-resume`, `-baseline`, `-small`, `-clean`, `-temp`, `-final`, or a
date/SHA fragment are forbidden. A distinct `request_id` is what makes a run unique — not a
distinct branch.

Build the final CI request commit directly on the exact feature SHA to test, then force-update the
assigned `ci-test/...` ref to that commit **once**. Never publish the feature/template head as an
intermediate remote reset: that is a second push event, and delayed event admission can allow the
older template run to cancel the real request. A CI iteration is one remote ref update, not
"reset, push, edit, push."

Reuse is what makes the latest-request-wins cancellation below work. Pushing each retry to a
new branch defeats it: every stale attempt keeps its own runner slot instead of being
superseded.

Never push a placeholder, probe, or scratch branch (`tmp-*`, `temp-*`, `noop-*`, `__*`,
`do-not-use-*`). If a ref would not be meaningful to a human reviewer a week later, it does not
belong on `origin`.

Never create a pull request, empty/no-op commit, custom workflow, or connector permission probe to
deliver or retrigger CI. Connector/API writes are real repository writes. If the assigned CI ref
cannot produce a run, document one infrastructure blocker and wait for the normal transport; do
not invent another branch or delivery mechanism.

## Validation loop

Assume you cannot execute Unity, **`tools/ci-test`**, or manually dispatch GitHub workflows.

Use the repository's push-triggered targeted-test mechanism on the task's single CI request
branch defined above. Do **not** update `.github/test-request.json` on the feature/PR branch:
that would create a PR synchronize event and fan out normal PR CI again.

A requested single test is a fast-feedback path and must complete in **less than 5 minutes** once its workflow job starts. Keep the requested test narrow enough to fit that budget; if it does not, split or narrow the test instead of extending the single-test timeout.

Showcase-dependent requests use a runner-local, content-fingerprinted startup-bake cache. Do not
force a fresh bake, delete that cache, or create a custom bake workflow as a retry mechanism. The
shared workflow invalidates the cache automatically when tracked bake inputs change.

Each `ci-test/...` branch is latest-request-wins. When a newer request is pushed to the same CI branch, GitHub Actions cancels any older queued or running single-test workflow for that branch rather than allowing requests to queue up. Only the newest request on that branch should be monitored as authoritative.

For each iteration:

1. Make the code/test changes on the feature branch, commit them, and push the feature branch.
2. Create the request commit locally, or through the Git object API, directly on the exact feature
   commit that should be tested. Do not move the remote CI ref yet.
3. In that request commit only, update **`.github/test-request.json`** with the smallest relevant Unity test:
   - `platform`: `EditMode` or `PlayMode`
   - `test`: the fully qualified test name or exact filter
   - `request_id`: a new unique string for every requested run
   - For an exact saved-camera replay, also set `scene_issue` to the assigned
     `SceneIssues/open|closed/<capture>/issue.json` path and set `replay_seconds` from 20 through 60.
     Use this shared path; never add or restore a one-shot workflow.
4. Force-update the assigned `ci-test/...` ref directly to that final request commit in one remote
   operation and record its SHA. Never publish an intermediate reset/template ref.
5. Monitor commit status **`ci/single-test`** on the newest request commit until it reaches a terminal state.
   - A missing status may mean the self-hosted job is queued/not started yet; inspect the exact-SHA
     Actions run before deciding that no workflow exists.
   - `pending` means the workflow has started.
   - `success` means the requested test actually passed.
   - `failure`/`error` means the requested test or its setup failed.
   - If a shell with authenticated `gh` is available, `tools/ci-wait --sha <request-commit>` polls continuously (5 seconds by default).
   - `tools/ci-wait` automatically honors `Retry-After` for HTTP 429/rate-limit 403 responses and otherwise exponentially backs off before retrying the API.
   - Connector-only agents should poll the same commit-status context through the GitHub connector/API.
   - If a newer request is pushed to the same `ci-test/...` branch, stop monitoring the superseded request and monitor only the newest request commit.
   - Query Actions for the exact request SHA before treating a missing commit status as a missing
     workflow. A known queued or running run must be left alone regardless of queue age or apparent
     ordering. If no exact-SHA run exists, wait at least 30 minutes before issuing one replacement.
     Never issue more than one replacement for the same source state.
6. On a product failure, follow the status target URL and inspect the failed-step logs and uploaded
   `single-test-*` artifact. Determine the cause, modify and push the feature branch, build one new
   request commit directly on that source, then force-update the assigned CI ref once and repeat.
7. Continue this loop until the target behavior is proven and CI is green.

This CI-branch separation is intentional: targeted request commits never update the open PR head, so they do not start the affected PR suite, architecture gate, or other pull-request workflows.

Do not stop after implementing a plausible fix. Continue iterating through CI until the goal is complete or a concrete blocker remains.

Runner contention, a developer's interactive Unity editor, delayed Actions admission, and a native
import/Burst crash are infrastructure results, not product failures. Record them in the issue's
single operational CI log, wait for the runner to become healthy, and retry at most once. Do not
change production code, create another branch/workflow, or emit a burst of requests in response.

### Scene-issue promotion to master

For coordinator-assigned scene issues, green targeted CI and terminal bookkeeping make the branch
ready for promotion; they do not authorize every worker to push `master`. Leave the verified
`fixes/agent-N` head unchanged and wait while the coordinator collects ready branches. The
coordinator designates one worker to integrate the entire ready batch and update `master` once.

- A worker not designated as batch promoter must not push `master` or move its verified feature
  head while waiting.
- The designated promoter must fetch immediately before promotion, verify every listed remote head,
  and merge all of them in an isolated local worktree based on current `origin/master`. Publish no
  intermediate integration ref.
- If a listed head moved, a merge conflicts, or `origin/master` advances before the push, stop and
  report it so the coordinator can rebuild the batch. Do not silently omit a member.
- Advance `master` exactly once for the complete batch with a normal non-force update. **Never
  force-push or overwrite `master`.**
- Verify the remote `origin/master` contains the fix commit and terminal bookkeeping commit, the
  capture exists only under `SceneIssues/closed/`, and the required `ci/single-test` result is green.
- Only after remote-master verification may each included scene-issue worker receive another task.

## Testing

- Start with the smallest test that proves the behavior being worked on.
- A single requested test must fit the **under-5-minute** CI budget; do not make the single-test workflow slower to accommodate an oversized test.
- Add or improve regression tests when an invariant was previously untested.
- After targeted validation passes, run the appropriate broader affected tests when warranted.
- Never interpret a CI run that executed zero tests as success.
- Do not claim a test passed unless its GitHub Actions run actually completed successfully.
- A cancelled, failed, or timed-out replay is not successful verification even if intermediate
  screenshots or logs were emitted.

## Completion

Before declaring the task complete:

- Review the final diff.
- Verify it follows **`CLAUDE.md`** and relevant specs.
- Confirm the relevant CI jobs are green.
- For a coordinator-assigned scene issue, confirm the verified terminal branch has been integrated
  into `origin/master` and that remote master contains the closed capture and recorded fix commit.
- Confirm the task created no branches on `origin` beyond its assigned feature branch and matching
  CI branch.
- State what was changed, what CI validation actually passed, and—for scene issues—the master commit
  that now contains the fix.

**Try in chat**
