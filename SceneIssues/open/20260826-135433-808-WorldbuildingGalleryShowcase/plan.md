# Plan — 20260826-135433-808 WorldbuildingGallery grass

## Reopened goal
The previous implementation is rejected. Replace it by porting the approved browser meadow renderer to Unity. Do not revive the old Dylearn/card experiments. `reference-grass-target.jpg` is historical context only; acceptance is the renderer behavior below plus human visual review.

## Renderer to port
Port behavior, not Babylon-specific APIs.

- Grass is solid tapered ribbon geometry, not alpha-cutout cards. Blades keep a stable silhouette by rotating around world Y to face the camera; orbiting the camera must not change apparent width or color.
- CPU work is construction-only: generate blade roots, height/width/lean/phase, Perlin coverage, and regional colors. Per-frame grass deformation belongs in a Unity vertex shader/GPU path.
- Pack per-blade/per-vertex data suitable for GPU reconstruction: root XZ, root Y, lateral silhouette offset, local vertical offset, tip factor, and random phase.
- Shader inputs per frame: time, player position, interaction radius, and camera-right direction projected to XZ.
- Wind is coherent waves plus small phase variation; bend strength increases root-to-tip. Nearby grass bends away from the player/capsule and recovers when the player leaves.
- Use separate low-frequency Perlin/FBM fields for (1) grass coverage/density, (2) broad grass-color regions, and (3) ground shade/value. Coverage must create real dense, sparse, and bare patches rather than salt-and-pepper removal.
- Preserve the approved green family: dark `(0.21,0.44,0.11)`, medium `(0.34,0.62,0.18)`, fresh `(0.49,0.76,0.25)`, sunny `(0.70,0.90,0.40)`, with the light root-to-tip toon ramp. Grass shading must be camera-invariant; avoid normal/light changes that darken blades during orbit.
- Ground color should visually integrate with the same Perlin regions and include broad noise-driven lighter/darker shade variation.
- Preserve procedural wildflowers: 11 visibly distinct species/colors, noise-clustered by region with varied height/scale/sway. White daisies should include both upward-facing and player/camera-tilted blossoms. Flower animation may be ported CPU-first, but grass animation must be GPU-driven.

## Acceptance / regression
1. In WorldbuildingGallery gameplay, the meadow reads as continuous stylized grass with coherent color/coverage patches and visible flower variety.
2. A 360° camera orbit does not materially change blade silhouette or grass brightness.
3. Player traversal produces local bend-away and recovery without moving the whole field.
4. Add focused regression coverage for procedural patch determinism, GPU parameter/data generation, and localized interaction math; verify the saved SceneIssue pose at native resolution.
5. Record performance cost for the gallery scene and reject any implementation that returns to per-frame CPU vertex rewrites.