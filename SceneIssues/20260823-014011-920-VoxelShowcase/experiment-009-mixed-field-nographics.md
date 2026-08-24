# Experiment 009 — mixed-field oracle under null graphics

**Hypothesis** — The retained single-copy mixed-field CPU/GPU oracle passes on the merged
`fixes` source and provides broader validation of the coarse material-selection repair.

**What was performed** — Source commit `0caa62f48de6d6562a25ed5d6ee1b9fd3f2427a1` was reset
onto the existing `ci-test/fixes`; request commit `cd82ec4f59b59f95e2ca0a8c0dd04af4f92a5297`
requested
`VoxelEngine.Tests.EditMode.GpuSurfaceExtractorOracleTests.MixedSampleFieldMatchesTheCpuJob`.
Evidence is in `verification-mixed-field-nographics-ci.txt` and GitHub Actions run `32747664724`.

**Result** — Inconclusive for CPU/GPU parity. Unity discovered and executed both step-1 and
step-2 cases, but both stopped while constructing `GpuSurfaceExtractor` because the EditMode
single-test workflow adds `-nographics`; `ComputeShader.FindKernel("CSSampleDensity")` therefore
reported that the kernel was unavailable. No density, boundary, or vertex comparison ran.

**What was learned** — The single-test workflow's null-graphics EditMode environment cannot
exercise compute-shader oracle tests. This repeats the previously documented environment
limitation and is not evidence of an HLSL or production mismatch; the same shader/oracle path has
passed in a graphics-enabled Metal run.

**Next** — Do not change production shader code in response. Use local Unity through
`tools/unity-run.sh` for graphics-dependent validation and the exact-camera replay, and use only
graphics-independent tests if another targeted CI request is warranted.
