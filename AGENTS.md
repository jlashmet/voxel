Voxel Development Skill
Use this skill whenever working on **`jlashmet/voxel`**.

## Instructions

- Read the repository root **`CLAUDE.md`** before doing work.
- Treat its architectural constraints and referenced specs as binding.
- Inspect the relevant implementation, tests, and plans before making changes.
- Prefer fixing proven causes and invariants over speculative changes.

## Validation loop

Assume you cannot execute Unity, **`tools/ci-test`**, or manually dispatch GitHub workflows.

Use the repository's push-triggered targeted-test mechanism on a dedicated CI request branch. Do **not** update `.github/test-request.json` on the feature/PR branch: that would create a PR synchronize event and fan out normal PR CI again.

For each iteration:

1. Make the code/test changes on the feature branch, commit them, and push the feature branch.
2. Create or reset a dedicated branch named **`ci-test/<feature-branch-or-purpose>`** to the exact feature commit that should be tested.
   - Connector-only agents can create the branch when absent, or move it with a forced ref update when reusing it.
   - The CI branch must point at the exact source commit before the request-file commit is added.
3. On the `ci-test/...` branch only, update **`.github/test-request.json`** with the smallest relevant Unity test:
   - `platform`: `EditMode` or `PlayMode`
   - `test`: the fully qualified test name or exact filter
   - `request_id`: a new unique string for every requested run
4. Commit/push that request-file change on the `ci-test/...` branch and record the resulting request commit SHA.
5. Monitor commit status **`ci/single-test`** on the request commit until it reaches a terminal state.
   - A missing status means the self-hosted job is queued/not started yet.
   - `pending` means the workflow has started.
   - `success` means the requested test actually passed.
   - `failure`/`error` means the requested test or its setup failed.
   - If a shell with authenticated `gh` is available, `tools/ci-wait --sha <request-commit>` polls continuously (5 seconds by default).
   - `tools/ci-wait` automatically honors `Retry-After` for HTTP 429/rate-limit 403 responses and otherwise exponentially backs off before retrying the API.
   - Connector-only agents should poll the same commit-status context through the GitHub connector/API.
6. On failure, follow the status target URL and inspect the failed-step logs and uploaded `single-test-*` artifact. Determine the cause, modify the feature branch, commit/push it, reset the CI branch to the new feature head, create a new request commit, and repeat.
7. Continue this loop until the target behavior is proven and CI is green.

This CI-branch separation is intentional: targeted request commits never update the open PR head, so they do not start the affected PR suite, architecture gate, or other pull-request workflows.

Do not stop after implementing a plausible fix. Continue iterating through CI until the goal is complete or a concrete blocker remains.

## Testing

- Start with the smallest test that proves the behavior being worked on.
- Add or improve regression tests when an invariant was previously untested.
- After targeted validation passes, run the appropriate broader affected tests when warranted.
- Never interpret a CI run that executed zero tests as success.
- Do not claim a test passed unless its GitHub Actions run actually completed successfully.

## Completion

Before declaring the task complete:

- Review the final diff.
- Verify it follows **`CLAUDE.md`** and relevant specs.
- Confirm the relevant CI jobs are green.
- State what was changed and what CI validation actually passed.

**Try in chat**
