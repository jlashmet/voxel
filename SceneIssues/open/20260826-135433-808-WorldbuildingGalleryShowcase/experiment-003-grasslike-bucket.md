# Experiment 003 — full grass-like foliage bucket

**Hypothesis:** The saved region mixes semantic Grass with other ground-cover species that share foliage shape `0`; shapes `0` and `5` must both use the compact pixel-grass presentation.

**Action / source:** Extended the camera-facing 16×16 procedural three-blade sprite path to both shape `0` and semantic-Grass shape `5`, while leaving flowers/fronds/shrubs/fungi and non-foliage shader classes unchanged. Exact source `6b05ee9db8157f7d26b1d343d210e4dbf15f51c8`; request `4814488bcd792ebd8f83439e463311f9666804e5`; run `33044687964`.

**Result:** `VoxelEngine.Tests.PlayMode.ProceduralGrassBillboardTests.GrassSilhouetteRemainsCompactAndReadableAcrossCameraAzimuths` passed 1/1. The original saved pose replayed successfully. Direct inspection of the marked region shows the tall dark radial-card bars are gone and the vegetation reads as compact stepped multi-blade pixel foliage.

**Verdict:** Confirmed. No placement/count/draw-call budget increase was required.
