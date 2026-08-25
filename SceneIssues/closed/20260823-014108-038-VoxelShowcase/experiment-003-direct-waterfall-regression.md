# Experiment 003 — direct waterfall regression

**Hypothesis** — A capture-specific PlayMode regression can prove the upper stream, cascade, and
cleared ravine lanes directly on current `fixes`, independent of the broad lower-river assertion.

**What was performed** — Added
`CastleAccessTests.SceneIssue20260823014108038WaterfallRemainsVisibleAndUnoccluded` at source
`be2315394e5f000a4093c0c61f71c10b2d1b7630` plus the working-tree test. It loads the showcase,
reconstructs the castle plan from the world seed, and samples the authored stream, cascade, and
three cleared air lanes. Ran it through `tools/unity-run.sh`; NUnit evidence is in
`verification-direct-waterfall-attempt1.xml`.

**Result** — Failed 0/1 at the first assertion. The reconstructed upper-stream centre sample was
Empty (0), not Water (11), so the test did not reach the cascade or clearance assertions.

**What was learned** — The visible current-head frame alone is insufficient to prove authored
waterfall state. Both the older broad test and the new direct test derive a fresh plan rather than
reading the plan retained by the loaded baked world, so plan/bake coordinate drift must be ruled
out before changing waterfall authoring.

**Next** — Read the loaded world's authoritative `_castlePlan`, compare it with the reconstructed
plan, and sample waterfall state from that plan. If they match and remain empty, investigate stale
or incomplete baked-world content.
