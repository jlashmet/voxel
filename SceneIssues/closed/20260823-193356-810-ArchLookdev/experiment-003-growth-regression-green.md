# Experiment 003 — focused growth regression green

**Hypothesis** — With the stable vegetation API referenced directly, the focused regression will
prove that the authored reference growth covers the left pier, crown, and right counterweight and
survives a component lifecycle transition.

**What was performed** — Added `VoxelEngine.Vegetation.Api` to the PlayMode test assembly and
reran only
`VoxelEngine.Tests.PlayMode.ArchReferenceGrowthTests.AuthoredGrowthCoversLeftPierCrownAndRightCounterweight`
through `tools/unity-run.sh` on the working tree based at
`847bac34f4c34b8cb6ca1130bb968efcf6f3598d`. Evidence is
`verification-growth-test.txt` and `verification-growth-test.xml`.

**Result** — Confirmed: 1/1 test passed in 0.029 seconds (0 failed, 0 skipped). The direct
assertions prove all 60 semantic instances are submitted, including the expected left-pier,
crown, and right-side ivy/flower populations and hero-distance flower scale. Clearing on disable
and restoration of all 60 submissions on re-enable also passed.

**What was learned** — The current authored distribution and renderer lifecycle directly guard
the failure seen in the capture. The visual fix no longer depends solely on manual screenshot
review.

**Next** — Run the adjacent production vegetation rendering/material/boundary tests, then rebuild
and replay the exact saved camera once more from the final clean source.
