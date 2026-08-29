# Experiment 001 — queued grass wind state

**Hypothesis.** The shader exists and the packed mesh is being resubmitted, but `_GrassTime` is not being snapshotted with each deferred `Graphics.DrawMesh` submission; mutating the shared material is therefore not reliably reaching the rendered grass.

**Action / source.** On pre-fix source `fca3877669cd48e269badeb11fe7cb37c644b207`, inspect player run `33242524673`, then repair `ProceduralGrassBatch.Draw()` with a reused per-draw property block and validate exact feature SHA `6c9219e90d68b939940adca1ca37be1f8961b31d` in run `33244533044`. Compare late stationary captures and check shader wave frequencies against the 10-second capture cadence.

**Result.** The focused regression passes and confirms the MPB contains an advancing unscaled clock, but the built player still has exactly zero changed pixels in the grass/ground region from 39.9s→49.9s and 49.9s→59.9s while sky pixels change. The shader uses 0.82, 0.46, and 1.06 rad/s wave terms, so exact 10-second recurrence cannot explain the identical ground raster.

**Verdict.** FALSIFIED. Snapshotting `_GrassTime` in the packed draw's property block is insufficient to make the visible built-player grass advance. The scene remains a product failure and cannot promote.

**Next step.** Inspect the production material publication consumed by the shader during paused gameplay rather than adding a second animation system.
