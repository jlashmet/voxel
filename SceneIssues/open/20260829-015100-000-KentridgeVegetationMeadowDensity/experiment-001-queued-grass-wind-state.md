# Experiment 001 — queued grass wind state

**Hypothesis.** The shader exists and the packed mesh is being resubmitted, but `_GrassTime` is not being snapshotted with each deferred `Graphics.DrawMesh` submission; mutating the shared material is therefore not reliably reaching the rendered grass.

**Action / source.** On pre-fix source `fca3877669cd48e269badeb11fe7cb37c644b207`, inspect exact-player run `33242524673`, shader build inclusion, `ProceduralVegetationBatchRenderer.DrawNow`, `ProceduralVegetationMaterials.ApplyGrassState`, `ProceduralGrassBatch.Draw`, and the grass vertex shader. Compare late stationary captures at 19.7/29.7/39.7/49.7/59.7 seconds.

**Result.** The grass/ground raster is pixel-identical across multiple 10-second late intervals while sky pixels continue changing. The grass shader is compiled/included, consumes `_GrassTime`, and the batch is submitted every `LateUpdate`. Unity's `Graphics.DrawMesh` contract states queued draws should use `MaterialPropertyBlock` when submission-specific material properties must be preserved.

**Verdict.** Supports a shared draw-state ownership defect, not sparse geometry, shader stripping, or missing per-frame submission. Selected repair: snapshot an unscaled wind clock in one reused property block passed directly to every packed-grass draw.

**Falsifier / next step.** If the exact built-player replay remains visually static after the property-block repair, reject this hypothesis and continue at the compiled shader/material binding boundary rather than adding a second animation system.
