# Experiment 003 — final CI compile scope

**Hypothesis**
The final far-field regression is behaviorally sound, but its direct import of `Game.Composition.Materials` is outside the PlayMode test assembly's declared references.

**Action / source**
Inspect failed exact request `e19de2866b4b90bb4c7dcc83bab9f823a9f6e163` for source `98b7b0fbd1f5349664fcd514bebb42b3e0c269ff`, including bake and real-player build logs. Refresh and merge current master, then keep the regression on the public `ShowcaseMaterialSet`/`VoxelFarTerrain.ResolveFarSurfaceMaterial` contract with a direct test-only `VoxelEngine.Composition.Api` reference.

**Result**
Both bake and player build stopped before execution with only `CS0234` at the regression's `Game.Composition.Materials` import. No production compile error or runtime result was reached. The test now supplies explicit material roles through the engine API; the PlayMode asmdef gains only that API dependency.

**Verdict**
Product test-scope failure confirmed and fixed at `c10ddafc589fc08320d0eeab6b47d449e18d82b1`. Runtime/player dependencies and the production fix are unchanged. Re-run the same focused behavior plus exact scene replay on the new exact source SHA.
