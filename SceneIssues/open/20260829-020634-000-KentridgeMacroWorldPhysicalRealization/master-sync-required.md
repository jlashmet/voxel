# Required master synchronization

After Agent 1's validated GPU renderer fix is merged to `master`, this assignment must synchronize before continuing substantive work:

1. Fetch `origin`.
2. Merge the then-current `origin/master` into `fixes/agent-6` (do not rebase or cherry-pick another agent's branch).
3. Resolve only conflicts necessary to preserve this assignment and the newly published master fixes.
4. Re-evaluate any blocker that depended on renderer/master state.
5. Continue the next unchecked non-blocked task in this SceneIssue.
6. Keep `ci-test/fixes/agent-6` as the only targeted-CI transport; never replace queued/running CI.

This synchronization does not close, restart, or change acceptance for the assigned SceneIssue.