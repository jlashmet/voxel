Voxel Development Skill
Use this skill whenever working on **`jlashmet/voxel`**.

## Instructions

- Read the repository root **`CLAUDE.md`** before doing work.
- Treat its architectural constraints and referenced specs as binding.
- Inspect the relevant implementation, tests, and plans before making changes.
- Prefer fixing proven causes and invariants over speculative changes.

## Planning

If a task is more than a trivial one-step edit, create or update a durable Markdown plan in **`.claude/plans/`** before implementation begins.

- Resume an existing relevant plan instead of creating a duplicate.
- State the goal, scope, important constraints, and concrete acceptance criteria.
- Break the work into a checkable task list using Markdown checkboxes.
- Check items off as they are completed and keep the plan current while work is in progress.
- Record material findings, failed hypotheses, blockers, and validation evidence so another agent can resume the work without reconstructing the investigation from chat history.
- If the implementation direction changes, update the plan before continuing so it remains the authoritative record of the work.
- A tiny edit that can be implemented and validated in one straightforward step does not require a separate plan.

## Branch discipline

A task uses **exactly two branches for its entire lifetime**: one feature branch and one CI
request branch. Both names are fixed when the task starts and never change.

```
<feature-branch>          the work
ci-test/<feature-branch>  the CI request branch, force-reset each iteration
```

Iterating means **force-updating these two refs, never creating a third**. Do not create a new
branch to retry a test, try a variant, capture a baseline, or hold evidence. Suffixes such as
`-v2`, `-latest`, `-retry`, `-resume`, `-baseline`, `-small`, `-clean`, `-temp`, `-final`, or a
date/SHA fragment are forbidden. A distinct `request_id` is what makes a run unique — not a
distinct branch.

Reuse is what makes the latest-request-wins cancellation below work. Pushing each retry to a
new branch defeats it: every stale attempt keeps its own runner slot instead of being
superseded.

Never push a placeholder, probe, or scratch branch (`tmp-*`, `temp-*`, `noop-*`, `__*`,
`do-not-use-*`). If a ref would not be meaningful to a human reviewer a week later, it does not
belong on `origin`.

For queued work the branch name is not chosen, it is derived — the branch list is the claim
registry that keeps concurrent agents off each other's work:

```
sceneissue/<id>    ci-test/sceneissue/<id>    id verbatim from issue.json
```

Delete `ci-test/<feature-branch>` as soon as the task's validation is green; delete the feature
branch once it merges. The agent that pushed a branch is responsible for removing it. Leaving
unmerged branches on `origin` is a defect, not housekeeping — it is unreviewed work that looks
abandoned and consumes self-hosted runner capacity.

## Validation loop

Assume you cannot execute Unity, **`tools/ci-test`**, or manually dispatch GitHub workflows.

Use the repository's push-triggered targeted-test mechanism on the task's single CI request
branch defined above. Do **not** update `.github/test-request.json` on the feature/PR branch:
that would create a PR synchronize event and fan out normal PR CI again.

The self-hosted runner executes **one job at a time for the whole fleet**, and several agents
share it. Your queued request delays every other agent. Do not push a CI request to confirm a
guess — push it to confirm a hypothesis you have already reasoned through, and prefer one
request that validates a fix and its regression together over two sequential requests.

A requested single test is a fast-feedback path and must complete in **less than 5 minutes** once its workflow job starts. Keep the requested test narrow enough to fit that budget; if it does not, split or narrow the test instead of extending the single-test timeout.

Each `ci-test/...` branch is latest-request-wins. When a newer request is pushed to the same CI branch, GitHub Actions cancels any older queued or running single-test workflow for that branch rather than allowing requests to queue up. Only the newest request on that branch should be monitored as authoritative.

For each iteration:

1. Make the code/test changes on the feature branch, commit them, and push the feature branch.
2. Force-reset the task's **`ci-test/<feature-branch>`** to the exact feature commit that should be tested. Create it only on the first iteration; every later iteration reuses and resets that same ref.
   - Connector-only agents create the branch when absent, and otherwise move it with a forced ref update. Reuse is mandatory, not an option.
   - The CI branch must point at the exact source commit before the request-file commit is added.
3. On the `ci-test/...` branch only, update **`.github/test-request.json`** with the smallest relevant Unity test:
   - `platform`: `EditMode` or `PlayMode`
   - `test`: the fully qualified test name or exact filter
   - `request_id`: a new unique string for every requested run
4. Commit/push that request-file change on the `ci-test/...` branch and record the resulting request commit SHA.
5. Monitor commit status **`ci/single-test`** on the newest request commit until it reaches a terminal state.
   - A missing status means the self-hosted job is queued/not started yet.
   - `pending` means the workflow has started.
   - `success` means the requested test actually passed.
   - `failure`/`error` means the requested test or its setup failed.
   - If a shell with authenticated `gh` is available, `tools/ci-wait --sha <request-commit>` polls continuously (5 seconds by default).
   - `tools/ci-wait` automatically honors `Retry-After` for HTTP 429/rate-limit 403 responses and otherwise exponentially backs off before retrying the API.
   - Connector-only agents should poll the same commit-status context through the GitHub connector/API.
   - If a newer request is pushed to the same `ci-test/...` branch, stop monitoring the superseded request and monitor only the newest request commit.
6. On failure, follow the status target URL and inspect the failed-step logs and uploaded `single-test-*` artifact. Determine the cause, modify the feature branch, commit/push it, reset the CI branch to the new feature head, create a new request commit, and repeat.
7. Continue this loop until the target behavior is proven and CI is green.

This CI-branch separation is intentional: targeted request commits never update the open PR head, so they do not start the affected PR suite, architecture gate, or other pull-request workflows.

Do not stop after implementing a plausible fix. Continue iterating through CI until the goal is complete or a concrete blocker remains.

## Testing

- Start with the smallest test that proves the behavior being worked on.
- A single requested test must fit the **under-5-minute** CI budget; do not make the single-test workflow slower to accommodate an oversized test.
- Add or improve regression tests when an invariant was previously untested.
- After targeted validation passes, run the appropriate broader affected tests when warranted.
- Never interpret a CI run that executed zero tests as success.
- Do not claim a test passed unless its GitHub Actions run actually completed successfully.

## Completion

Before declaring the task complete:

- Review the final diff.
- Verify it follows **`CLAUDE.md`** and relevant specs.
- Confirm the relevant CI jobs are green.
- Delete the task's `ci-test/<feature-branch>` from `origin`.
- Confirm the task created no branches on `origin` beyond its one feature branch and that one CI branch.
- State what was changed and what CI validation actually passed.

**Try in chat**
