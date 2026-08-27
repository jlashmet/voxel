# Plan — 20260826-135433-808 WorldbuildingGallery grass

## Reopened goal
The previous implementation is rejected. Replace it by porting the approved browser meadow renderer to Unity. Do not revive the old Dylearn/card experiments. `reference-grass-target.jpg` is historical context only; acceptance is the renderer behavior below plus human visual review.

## Required reference implementation
Use `grass-renderer-reference.shader` in this SceneIssue as the starting HLSL/ShaderLab implementation for the production URP shader. It contains the approved GPU math for camera-facing reconstruction, coherent wind, local player bend/recovery, fixed color output, and fog. Port it into the appropriate `Assets/` runtime location rather than leaving production dependent on the SceneIssue file.

CPU construction must pack the blade mesh exactly enough to drive that shader:
- `TEXCOORD0 = (rootOS.x, rootOS.z)`
- `TEXCOORD1 = (rootOS.y, baseLateralOffset)`
- `TEXCOORD2 = (localVerticalOffset, tipFactor)`
- `TEXCOORD3 = (randomPhase, reserved)`
- vertex `COLOR` = final regional/root-to-tip grass color

Per-frame shader inputs are `_GrassTime`, `_GrassPlayerPositionWS`, `_GrassPushRadius`, and `_GrassCameraRightWS`. Do not rewrite grass vertices on the CPU each frame.

## Renderer behavior to preserve
- Solid tapered ribbon blades; no alpha-cutout cards. Blades rotate around world Y toward camera-right so a 360° orbit does not materially change apparent width or color.
- CPU work is construction-only: blade roots, height/width/lean/phase, Perlin placement/density, packed mesh data, and regional colors.
- Use separate low-frequency Perlin/FBM fields for (1) grass coverage/density, (2) broad grass-color regions, and (3) ground shade/value. Coverage must form coherent dense, sparse, and bare regions.
- Preserve the approved green family: dark `(0.21,0.44,0.11)`, medium `(0.34,0.62,0.18)`, fresh `(0.49,0.76,0.25)`, sunny `(0.70,0.90,0.40)`, with the existing root-to-tip toon ramp.
- Ground color must integrate with the same Perlin regions and broad light/dark noise variation.
- Preserve 11 visibly distinct procedural wildflower species, noise-clustered by region with varied scale/height/sway. Daisies include both upward and camera/player-tilted blossoms. Flowers may animate CPU-first; grass must remain GPU-driven.

## Acceptance / regression
1. WorldbuildingGallery reads as a continuous stylized meadow with coherent coverage/color patches and visible flower variety.
2. 360° camera orbit preserves blade silhouette and grass brightness.
3. Player traversal bends only nearby grass away and it recovers afterward.
4. Add regressions for deterministic Perlin placement, packed GPU data, shader parameter/update math, and local interaction behavior; replay the saved pose at native resolution.
5. Record gallery performance cost and reject any implementation that returns to per-frame CPU grass vertex rewrites.
