# Experiment 005 — targeted grass regression is green

**Hypothesis**

The shared foliage shader/material changes should satisfy the authored grass-style contract without introducing a Unity compile/test failure.

**What was performed**

- Tested feature source through commit `6656a720e45a5f621389478977c9cdb388a7e919` using the dedicated `ci-test/fixes/agent-8` request branch.
- CI request commit: `ca3dbb1d7cd2ab488b579586d833c81a0958dbd0`.
- Requested EditMode test `VoxelEngine.Tests.EditMode.ProceduralVegetationGrassStyleTests.FoliageShaderImplementsAuthoredGrassMotionAndToonVariationContract`.
- Observed GitHub Actions run `32899971545` and inspected the completed job log.

**Result**

`ci/single-test` completed successfully. Unity `6000.5.6f1` executed exactly one matching test case and exited with status 0. The job log reports `Executed 1 test case(s).`

**What was learned**

The production foliage shader and material bridge now compile and satisfy the regression covering quantized animation, world-space/multi-sample noise, stable per-instance variation, view sway, hybrid toon lighting, character displacement, and the fixed 64-interactor shader-array bridge.

**Next**

Replay the original Worldbuilding Gallery capture at its saved camera pose and inspect the marked grass region after the fix. Preserve the replay result as experiment evidence before terminal issue bookkeeping.
