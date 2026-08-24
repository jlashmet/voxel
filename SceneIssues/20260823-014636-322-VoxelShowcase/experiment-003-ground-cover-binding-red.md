# Experiment 003 — ground-cover binding red regression

**Hypothesis** — The transient is caused by the far fallback using dirt below the base-height split
while the authoritative near terrain uses grass.

**What was performed** — Ran the existing focused EditMode test
`Game.Materials.Tests.GameShowcaseMaterialTests.NearAndFarTerrainAgreeOnGroundCover` through
`tools/unity-run.sh` on source `87bfc27d7`, with no production or test changes.

**Result** — The hypothesis was confirmed. One test executed and failed: at height 196 the near
binding returned material 13 (grass) while the far binding returned material 10 (dirt). Unity
exited 2 after 9 seconds; the XML reports 0 passed and 1 failed.

**What was learned** — The repository already has the direct invariant needed for this capture,
and current source violates it. The failure explains both the brown fallback and why it disappears
precisely when missing near surfaces publish.

**Next** — Change `GameShowcaseMaterials.Default.TerrainLowSurface` to grass, update its obsolete
role expectations, and rerun this focused test green before replaying the saved pose.
