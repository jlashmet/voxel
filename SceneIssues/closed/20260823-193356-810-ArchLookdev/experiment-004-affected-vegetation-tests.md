# Experiment 004 — affected vegetation validation

**Hypothesis** — Exposing the semantic instances for regression coverage and adding the PlayMode
test dependency do not weaken the production renderer, material selection, or API boundary.

**What was performed** — Through `tools/unity-run.sh`, ran the new
`ArchReferenceGrowthTests` together with `VoxelEngine.CI.VegetationRenderingTests` in PlayMode,
then ran `ProceduralVegetationMaterialTests` and `VegetationAssemblyBoundaryTests` in EditMode.
The working tree was based at `847bac34f4c34b8cb6ca1130bb968efcf6f3598d`. Evidence is
`verification-affected-playmode.{txt,xml}` and `verification-affected-editmode.{txt,xml}`.

**Result** — Confirmed: PlayMode passed 2/2 (0 failed, 0 skipped) and EditMode passed 2/2
(0 failed, 0 skipped). The production instanced vegetation submission, ivy vine shader class,
and stable API-only architecture boundary remain intact.

**What was learned** — The focused regression is compatible with the adjacent vegetation
contracts and does not introduce a CPU/GPU meshing dependency.

**Next** — Build the final ordinary ArchLookdev player and replay the saved pose for 25 seconds,
then remove the temporary fixture and review the complete issue diff.
