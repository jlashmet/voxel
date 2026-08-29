# Experiment 004 — exposed grass surface grounding

**Hypothesis.** Packed grass is generated and submitted, but Kentridge roots it inside the top terrain voxel, hiding the blade silhouette. `ShowcaseWorld.SurfaceHeight()` returns the topmost occupied voxel index: `MaterialAt(y, surface)` is empty only when `y > surface`, and above-ground reference construction starts at `ground + 1`. `KentridgeRegionLife.TryGround()` instead publishes `height * VoxelSize`.

**Action.** Move only Kentridge ecology ground samples to the exposed top face `(height + 1) * VoxelSize`. Preserve X/Z coordinates, terrain normal sampling, semantic density, seed, exclusions, packed renderer, shader amplitudes and draw path. Add a production-scene regression that samples generated semantic grass and verifies every checked root matches `(world.SurfaceHeight(vx, vz) + 1) * VoxelSize`.

**Expected result.** The same built Kentridge replay should visibly resolve individual packed blades above terrain; with the existing engine-managed `_Time.y` wind, late stationary frames should then show changed blade silhouettes while counts/leakage remain stable.

**Falsifier.** If final built frames show exposed blades but their silhouettes remain byte-identical, the geometry-placement hypothesis explains visibility but not animation; per workflow, isolate the visible grass draw in a minimal render repro before any further production change.
