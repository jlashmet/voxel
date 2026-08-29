# Experiment 002 — scaled grass material clock

**Hypothesis.** The visible packed-grass shader is still receiving `_GrassTime` from `ProceduralVegetationMaterials.ApplyGrassState()`. That path uses scaled `Time.time`, so a dialogue/cutscene pause freezes wind even though the player keeps rendering and screenshots/sky continue changing.

**Action / source.** Inspect exact-player artifact `33244533044` and the production material bridge on tested SHA `6c9219e90d68b939940adca1ca37be1f8961b31d`. The late grass/ground raster is pixel-identical while the application renders hundreds of frames per second. Change only the shared material clock publication from `Time.time` to `Time.unscaledTime` on `fixes/agent-5`; retain packed GPU deformation and the existing batching path. Add a PlayMode regression that pauses `Time.timeScale`, advances real frames, republishes production grass state, and compares material `_GrassTime` values.

**Result.** Source correction committed after the failed visual gate. It removes the remaining scaled-time dependency from the shared material path without changing density, topology, shader formulas, draw count, or allocation behavior. Exact-SHA built validation of this corrected state is still required.

**Verdict.** Strongly supported by the failed artifact and production ownership path, but NOT YET VALIDATED in the built application. The issue remains open.

**Next step.** Do not update `ci-test/fixes/agent-5` under the current no-extra-transport instruction. When a new exact-SHA validation is explicitly authorized, require both the paused-clock regression and time-separated stationary built-player frames with visibly changed blade pixels before promotion.
