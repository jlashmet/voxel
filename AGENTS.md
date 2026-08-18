Voxel Development Skill
Use this skill whenever working on **`jlashmet/voxel`**.

## Instructions

- Read the repository root **`CLAUDE.md`** before doing work.
- Treat its architectural constraints and referenced specs as binding.
- Inspect the relevant implementation, tests, and plans before making changes.
- Prefer fixing proven causes and invariants over speculative changes.

## Validation loop

Assume you cannot execute Unity, **`tools/ci-test`**, or manually dispatch GitHub workflows.

Use the repository's push-triggered targeted-test mechanism.

For each iteration:

1. Make the code/test changes.
2. Update **`.github/test-request.json`** with the smallest relevant Unity test:
   - `platform`: `EditMode` or `PlayMode`
   - `test`: the fully qualified test name or exact filter
   - `request_id`: a new unique string for every requested run so the file changes and a push is generated
3. Commit and push the changes.
4. Monitor commit status **`ci/single-test`** on the pushed commit until it reaches a terminal state.
   - A missing status means the self-hosted job is queued/not started yet.
   - `pending` means the workflow has started.
   - `success` means the requested test actually passed.
   - `failure`/`error` means the requested test or its setup failed.
   - If a shell with authenticated `gh` is available, `tools/ci-wait` polls this status continuously (5 seconds by default).
   - Connector-only agents should poll the same commit-status context through the GitHub connector/API.
5. On failure, follow the status target URL and inspect the failed-step logs and uploaded `single-test-*` artifact. Determine the cause, modify the code, update the request id, commit, and push again.
6. Continue this loop until the target behavior is proven and CI is green.

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
