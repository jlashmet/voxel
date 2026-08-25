# Experiment 002 — focused growth regression compile

**Hypothesis** — A direct PlayMode regression can inspect the semantic `VegetationInstance` list
and prove the saved-view distribution plus renderer lifecycle without relying on pixel thresholds.

**What was performed** — Added read-only access to `ArchReferenceGrowth.Instances` and the focused
`ArchReferenceGrowthTests.AuthoredGrowthCoversLeftPierCrownAndRightCounterweight` test on the
working tree based at `847bac34f4c34b8cb6ca1130bb968efcf6f3598d`. Ran that exact PlayMode
filter through `tools/unity-run.sh`. Evidence is `verification-growth-test-compile.txt`.

**Result** — Inconclusive: Unity stopped at compilation because
`VoxelEngine.Tests.PlayMode.asmdef` did not directly reference `VoxelEngine.Vegetation.Api`
(`CS0234` at the test's namespace import). No test executed.

**What was learned** — The direct semantic regression is correctly scoped, but the PlayMode test
assembly must declare the same stable vegetation API boundary it now consumes. This is test
wiring, not a production rendering failure.

**Next** — Add `VoxelEngine.Vegetation.Api` to the PlayMode test assembly and rerun the unchanged
focused filter; never count this zero-test compile failure as validation.
