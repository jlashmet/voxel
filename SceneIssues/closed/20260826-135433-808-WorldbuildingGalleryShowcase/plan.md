# Plan — 20260826-135433-808 WorldbuildingGallery grass

## Evidence
- One marked region: normalized centre `(0.4802, 0.6678)`, radius `0.0690`, from the saved `Worldbuilding Gallery Camera` pose. The note points to Dylearn's 3D Pixel Art Grass Demo as the target.
- The cited older issue fixed Transvoxel transition normals, not grass presentation. Gallery ground cover runs through `GalleryLifePopulation -> ProceduralVegetationBatchRenderer -> ProceduralVegetationFoliage.shader`.
- Density is already high and bounded: 1.1 m sampling over an 80 m radius, capped at 14,000 instances. Adding instances would add cost without fixing silhouette.
- Existing tuft geometry is seven radial cards; the reference uses compact nearest-filtered camera-facing grass sprites with stepped animation, rotational sway, and stylized lighting.
- First candidate produced tall dark bars. A later semantic-Grass-only candidate passed its synthetic regression but saved-pose inspection still showed bars, proving the gallery region also contains other grass-like kinds.
- Catalogue inspection showed Clover/Weed/Nettle/Reed/Cattail/DeadGrass/WaterGrass converge on foliage shape `0`; semantic Grass uses shape `5`. Flowers/fronds/shrubs/fungi use distinct shapes and surfaces/vines/woody growth use different shaders.

## Hypotheses / results
1. **Grass-like foliage presentation — confirmed.** Shapes `0` and `5` require the compact pixel-sprite path. Final regression renders production shape-0 Clover and requires a compact multi-blade footprint plus 90° camera-azimuth invariance.
2. **Low scatter density — falsified.** Existing sampling/count budget is already dense; placement remains unchanged.
3. **All vegetation needs restyling — falsified.** Flowers, fronds, shrubs, fungi, surface growth, vines, woody growth, trees, and semantic placement stay on their existing paths.

## Fix / verification
- Reconstruct grass-like radial cards as one camera-facing 16×16 procedural three-blade pixel sprite with base-anchored 5 Hz stepped sway and upward stylized lighting.
- No new assets, instances, batches, draw calls, or placement work; mesh/instance budgets are unchanged.
- Exact source `6b05ee9db8157f7d26b1d343d210e4dbf15f51c8`; request `4814488bcd792ebd8f83439e463311f9666804e5`; run `33044687964`.
- PlayMode regression passed 1/1 and the original saved pose replayed successfully. Direct inspection of final marked-region evidence confirms the tall dark radial-card bars are gone and the grass now reads as compact stepped multi-blade pixel foliage.
