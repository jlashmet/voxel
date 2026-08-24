# Experiment 005 — focused waterfall regression passes

**Hypothesis** — Bounded material-volume assertions plus exact ravine-clearance samples provide a
stable direct regression for the waterfall portion of SceneIssue 014108.

**What was performed** — Refined
`CastleAccessTests.SceneIssue20260823014108038WaterfallRemainsVisibleAndUnoccluded` to read the
loaded authoritative castle plan, count Water in a bounded upper-stream slice, count Cascade at
the lip, and assert three authored air lanes remain Empty. Ran the single PlayMode test through
`tools/unity-run.sh` at source `be2315394e5f000a4093c0c61f71c10b2d1b7630` plus the working-tree
test. Evidence is in `verification-direct-waterfall.xml` and
`verification-direct-waterfall.txt`.

**Result** — Passed 1/1 in 26.06 NUnit seconds; Unity exited successfully in 41 seconds.

**What was learned** — Hypothesis confirmed. Current world state contains the intended upper
stream and waterfall volume, and the ravine lanes implicated by the capture are empty. Combined
with experiment 001's exact replay and experiment 002's 3/3 GPU boundary/parity pass, no additional
production change is justified.

**Next** — Review and commit the capture-specific regression and evidence, then resolve the issue
separately using the existing GPU boundary-ownership production fix SHA.
