# Experiment 004 — ground-cover binding green regression

**Hypothesis** — Assigning grass to the far fallback's low-surface role restores parity with the
authoritative terrain binding.

**What was performed** — Changed `GameShowcaseMaterials.Default.TerrainLowSurface` from dirt to
grass and updated the two direct role expectations. Re-ran
`Game.Materials.Tests.GameShowcaseMaterialTests.NearAndFarTerrainAgreeOnGroundCover` through
`tools/unity-run.sh` on the working tree based at `87bfc27d7`.

**Result** — The hypothesis was confirmed. The focused test executed once and passed once with
zero failures in 0.022 seconds; the guarded Unity wrapper exited 0 after 10 seconds.

**What was learned** — Near and far representations now select the same ground cover throughout
the guarded height range. The one-line production change repairs the proven handoff invariant
without changing authored dirt roles for roads, fields, banks, or excavations.

**Next** — Run the complete material-binding fixture, then rebuild and replay the exact saved pose
to verify that incomplete coverage remains green while streaming converges.
