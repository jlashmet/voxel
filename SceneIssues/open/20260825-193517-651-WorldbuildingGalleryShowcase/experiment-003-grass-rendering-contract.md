# Experiment 003 — grass rendering contract

**Hypothesis**

The marked gallery grass reads poorly because the shared foliage renderer already provides instanced card geometry and alpha clipping, but its shader still lacks the authored variation/motion/lighting mechanisms named in the capture: quantized animation, spatially varying multi-sample wind, stable per-instance variation, view-space sway, character displacement, and softened toon lighting bands.

**What was performed**

Source branch head before this experiment: `87d3f102e84d7eb780f22f3b0d39dd988cfaf493`.

- Traced `Assets/Scenes/WorldbuildingGalleryShowcase.unity` to the production driver `Assets/Scenes/Showcase/WorldbuildingGalleryShowcase.cs` using the gallery binding regression and the driver-introduction commit history.
- Confirmed the production driver creates `GalleryLifePopulation` after world population, and that `GalleryLifePopulation` publishes semantic vegetation through `IVegetationBatchRenderer` rather than owning a scene-local grass implementation.
- Inspected `ProceduralVegetationBatchRenderer`: it already uses shared multi-card growth-form meshes, `Graphics.DrawMeshInstanced`, alpha-cutout materials, seeded yaw, and bounded batches. Gallery density is already intentionally high (1.1 m sampling over an 80 m radius, capped at 14,000 instances), so increasing scatter density would not address the rendering defect.
- Inspected `ProceduralVegetationFoliage.shader`: it currently uses continuous sine/cosine sway and a smooth lighting ramp. It has no quantized animation-time helper, multi-sample world-noise wind, stable shader-side instance variation, view-space sway helper, character-interaction array/displacement helper, or hybrid toon-band helper.
- Added `ProceduralVegetationGrassStyleTests.FoliageShaderImplementsAuthoredGrassMotionAndToonVariationContract`, a deterministic EditMode regression that encodes those requested mechanisms while retaining the existing shared instanced renderer architecture.

**Result**

The regression is authored before any production rendering edit. On the current shader source it is expected to fail immediately on `QuantizedAnimationTime` (and the subsequent absent contract markers). No Unity test execution is claimed in this experiment; the next experiment will be the targeted `ci/single-test` attempt on `ci-test/fixes/agent-8` and will record the observed CI result.

**What was learned**

The defect is not missing grass population or missing instancing. It is a shared foliage-shading quality gap: the existing renderer has the correct batching/geometry seam, but the foliage shader does not yet implement the richer temporal, spatial, interaction, and toon-lighting behavior requested by the capture.

**Next**

Run the new regression through the repository's targeted CI branch. Preserve the failing result as experiment evidence, then implement the smallest shared vegetation renderer/shader change that satisfies the contract and re-run the same test before replay verification.
