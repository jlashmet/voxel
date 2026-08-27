# Plan — 20260826-135433-808 WorldbuildingGallery grass

## Evidence
- One marked region: normalized centre `(0.4802, 0.6678)`, radius `0.0690`, from the saved `Worldbuilding Gallery Camera` pose. The note explicitly points to Dylearn's 3D Pixel Art Grass Demo as the target.
- The cited older issue fixed Transvoxel transition normals, not grass presentation. Gallery grass runs through `GalleryLifePopulation -> ProceduralVegetationBatchRenderer -> ProceduralVegetationFoliage.shader`.
- Density is already high and bounded: 1.1 m sampling over an 80 m radius, capped at 14,000 instances. Adding more instances would add cost without fixing silhouette.
- Existing tuft geometry is already seven radial cards, so a single authored card accidentally going edge-on is not the root cause. The reference instead uses a nearest-filtered compact grass sprite, camera-facing billboarding, stepped animation, rotational sway, and stylized lighting.
- First candidate exact-SHA replay reproduced the marked area as tall dark vertical bars. Its test failure was separately traced to `WaitForEndOfFrame` being unsupported in Unity batchmode, so both the implementation and harness required correction.

## Hypotheses / discriminators
1. **Presentation mismatch — confirmed.** Specialize only `VegetationKind.Grass` as a compact camera-facing pixel sprite with a three-leaf procedural mask, base-anchored sway, and upward stylized lighting. Regression must retain compact filled silhouette and three separated blade runs across a 90° camera azimuth change.
2. **Low scatter density — falsified.** Existing sampling/count budget is already dense; no placement or count change is needed.
3. **All tuft-like foliage needs the same treatment — falsified.** Shape `0` is shared by clover/weeds/reeds/etc.; grass now has a dedicated render discriminator so those species keep their prior path.

## Fix / blast radius / cost
- Grass-only shader/material specialization; no placement changes, no added semantic instances, and no new draw batches or source mesh assets.
- Existing tuft cards collapse in shader to the same camera-facing sprite, keeping vertex/instance budgets unchanged.
- PlayMode framebuffer regression uses explicit `Camera.Render()` so it is batchmode-safe and checks the grass-only discriminator, compact multi-blade silhouette, and azimuth invariance.
- Final exact-SHA CI runs that regression plus the saved scene-issue replay; acceptance requires the marked region to read as short leafy pixel grass rather than dark vertical bars.
