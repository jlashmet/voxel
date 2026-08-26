# Experiment 004 — targeted CI baseline is red

**Hypothesis**

The newly added grass-style regression should fail on the pre-fix production foliage shader, proving the test detects the exact missing rendering mechanisms before implementation.

**What was performed**

- Pushed the feature-branch regression head `abc3837ba8c4ee383d9548d54b32a97d8c917532` through the dedicated request branch `ci-test/fixes/agent-8` as request commit `7ba99939f393cffbbad465ac086372df31891e01`.
- Requested EditMode test `VoxelEngine.Tests.EditMode.ProceduralVegetationGrassStyleTests.FoliageShaderImplementsAuthoredGrassMotionAndToonVariationContract`.
- Observed GitHub Actions run `32897750435` and its `ci/single-test` commit status.

**Result**

The targeted CI run completed with `ci/single-test = failure`, as expected. Unity executed exactly one matching test case and failed it at the first missing contract marker:

`Expected: String containing "QuantizedAnimationTime"`

The runner used Unity `6000.5.6f1`, matched one test, and exited with status 2 because the assertion failed. This is a product regression failure, not a zero-test filter or infrastructure failure.

**What was learned**

The regression is sensitive to the current defect and establishes a genuine red baseline. The existing production shader still uses continuous sine/cosine sway and does not satisfy the authored grass rendering contract.

**Next**

Implement the smallest shared foliage shader/material-bridge change that adds quantized noisy wind, stable per-instance variation, view-space sway, softened toon bands, and a bounded grass-interactor displacement path. Then rerun this exact targeted test through `ci-test/fixes/agent-8` and preserve the observed result before any replay/closure bookkeeping.
