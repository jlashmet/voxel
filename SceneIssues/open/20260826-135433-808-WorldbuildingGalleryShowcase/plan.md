# Plan — 20260826-135433-808 WorldbuildingGallery grass

## Evidence
- One marked region: normalized centre `(0.4802, 0.6678)`, radius `0.0690`, from the saved `Worldbuilding Gallery Camera` pose. The note explicitly points to Dylearn's 3D Pixel Art Grass Demo as the target.
- The cited older issue fixed Transvoxel transition normals, not grass presentation. Gallery ground cover runs through `GalleryLifePopulation -> ProceduralVegetationBatchRenderer -> ProceduralVegetationFoliage.shader`.
- Density is already high and bounded: 1.1 m sampling over an 80 m radius, capped at 14,000 instances. Adding more instances would add cost without fixing silhouette.
- Existing tuft geometry is seven radial cards, while the reference uses a compact nearest-filtered camera-facing grass sprite with stepped animation, rotational sway, and stylized lighting.
- First candidate replay showed tall dark bars; its test also exposed a separate batchmode-harness error (`WaitForEndOfFrame`). After fixing both, exact-SHA CI/test passed but manual inspection of `verification-final.png` still showed tall dark bars inside the marked circle, so the issue was correctly kept open.
- Catalogue inspection explains that replay: gallery ground can select Clover/Weed/Nettle/Reed/Cattail/DeadGrass/WaterGrass as well as semantic Grass. Those grass-like foliage kinds converge on shader shape `0`; semantic Grass has shape `5`. Flowers/fronds/shrubs/fungi use distinct shapes and surfaces/vines/woody growth use different shaders.

## Hypotheses / discriminators
1. **Grass-like foliage presentation — confirmed.** Shapes `0` and `5` must use the compact pixel sprite; regression renders the production shape-0 Clover path and requires compact three-blade silhouette plus 90° camera-azimuth invariance.
2. **Low scatter density — falsified.** Existing sampling/count budget is already dense; no placement or count change is needed.
3. **All vegetation needs restyling — falsified.** Only the foliage shader's grass-like buckets change; flowers, fronds, shrubs, fungi, surface growth, vines, woody growth, trees, and semantic placement remain on existing paths.

## Fix / blast radius / cost
- Reconstruct grass-like radial cards as one camera-facing 16×16 procedural three-blade pixel sprite with base-anchored stepped sway and upward stylized lighting.
- No new assets, instances, batches, draw calls, or placement work; existing mesh/instance budgets are unchanged.
- PlayMode framebuffer regression uses explicit `Camera.Render()` for batchmode and now exercises shape `0`, the path the saved replay proved was missing.
- Final acceptance requires green exact-SHA regression plus saved-camera replay whose marked region no longer contains tall dark radial-card bars.
