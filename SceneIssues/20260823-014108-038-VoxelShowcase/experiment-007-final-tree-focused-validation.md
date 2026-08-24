# Experiment 007 — final-tree focused validation

**Hypothesis** — After reverting all exploratory changes to the unrelated broad landscape test,
the final proposed tree still preserves the capture-specific waterfall and ravine-clearance
invariants.

**What was performed** — Ran
`VoxelEngine.Tests.PlayMode.CastleAccessTests.SceneIssue20260823014108038WaterfallRemainsVisibleAndUnoccluded`
through `tools/unity-run.sh` on source `be2315394e5f000a4093c0c61f71c10b2d1b7630` plus the final
working-tree test addition. The retained test reads the loaded authoritative castle plan, verifies
bounded upper-stream Water and lip Cascade volumes, and checks the three implicated ravine lanes
are Empty. Evidence is in `verification-final-tree-focused.xml` and
`verification-final-tree-focused.txt`.

**Result** — Passed 1/1 in 26.10 NUnit seconds. Unity exited with status 0 in 41 seconds. The XML
SHA-256 is `720c535c4a324467122e46bf8197181346890587adf9e249f7429186555ff611`.

**What was learned** — Hypothesis confirmed. The focused invariant remains green on the exact
final test diff, while the pre-existing broad landscape test is unchanged.

**Next** — Review and commit the regression and capture evidence, then resolve `issue.json` in a
separate commit.
