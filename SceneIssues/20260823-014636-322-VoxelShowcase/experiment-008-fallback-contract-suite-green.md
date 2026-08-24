# Experiment 008 — fallback-contract suite green

**Hypothesis** — Updating the obsolete valley octave guard to the resolved SceneIssue 013924
invariant makes the broader fallback suite accurately validate the current terrain and handoff.

**What was performed** — Changed only the stale source-text assertions to require the current
`9/18` gentle octave and reject restoration of `9/70` or `7/24`, then reran all five
`VoxelEngine.Tests.EditMode.ShowcaseTraversalFallbackContractTests` through `tools/unity-run.sh`.

**Result** — The hypothesis was confirmed. Five tests executed and all five passed with zero
failures in 0.072 seconds; the guarded wrapper exited 0 after 12 seconds.

**What was learned** — The broader fallback contract now guards both halves of the visual fix:
continuous grass across near/far representations and the calm terrain relief established by the
previous capture. Together with the four material-role tests, affected EditMode validation is 9/9.

**Next** — Review the final diff and temporary-file state, commit/push the production, test, and
evidence changes, then resolve the manifest in a separate commit.
