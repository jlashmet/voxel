# Plan — 20260826-135433-808 WorldbuildingGallery grass

## Evidence
- Capture note says the grass is still unacceptable and points to Dylearn's 3D Pixel Art Grass Demo. The capture has one marked region at normalized centre (0.4802, 0.6678), radius 0.0690, from the saved `Worldbuilding Gallery Camera` pose.
- The issue it cites (`20260825-032832-253-VoxelShowcase`) fixed Transvoxel transition normals, not the vegetation renderer.
- Gallery grass is production `GalleryLifePopulation -> IVegetationBatchRenderer -> ProceduralVegetationBatchRenderer -> ProceduralVegetationFoliage.shader`. Population is already dense (1.1 m candidates over an 80 m radius, bounded at 14,000).
- The current renderer already falsifies the “single static card edge-on” explanation: tuft instances are seven radial cards with seeded yaw. The remaining mismatch is presentation. Dylearn uses camera-facing pixel-art cards with low-rate quantized animation and rotational sway; ours renders a seven-card 3D starburst with an analytic smooth mask and translational sway.
- The connector preserves the original PNG/blob but cannot decode that 2 MB repository image here; final acceptance will inspect the exact saved-camera CI replay with the same one-circle overlay rather than claim unseen pixels were inspected.

## Hypotheses / discriminator
1. **Primary — tuft presentation is the defect.** Flatten grass-like `_Shape == 0` cards into a camera-facing layered billboard, quantize the mask to a pixel grid, and keep the base anchored while wind changes blade angle. A framebuffer regression must retain silhouette area across a 90° camera azimuth change.
2. **Alternative — scatter density is too low.** Falsified by the existing 1.1 m sampling/14k budget and prior population inspection; changing density would add cost without addressing the reference-style mismatch.

## Fix / blast radius / cost
- Change only the foliage shader’s grass-like shape path; flowers, fronds, fungi, shrubs, surfaces, vines, placement, and instance counts remain unchanged.
- Reuse the existing seven-card mesh as one flattened layered billboard, so draw calls, batches, mesh vertex count, and semantic world state do not grow.
- Add a PlayMode framebuffer regression through the production foliage shader. Final exact-SHA CI will run that test plus the saved scene-issue replay.
