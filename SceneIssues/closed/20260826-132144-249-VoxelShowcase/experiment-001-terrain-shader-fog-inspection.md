# Experiment 001 — terrain shader fog-path inspection

## Hypothesis
The reported blue detailed-terrain band is a shading/fog discontinuity at the detailed/far terrain handoff rather than a geometry or streaming defect.

## Method
Inspected the post-merge fragment paths in:
- `Assets/VoxelEngine/Rendering/Runtime/Shaders/SmoothSurface.shader`
- `Assets/VoxelEngine/Rendering/Runtime/Shaders/FarTerrain.shader`

Compared their distance- and altitude-dependent sky blending at the capture's visible handoff.

## Result
Hypothesis supported.

On the merged baseline, `SmoothSurface.shader` computes:
- `smoothstep(60.0, 300.0, hitDistance) * 0.40`
- a low-altitude multiplier using `1.0 - smoothstep(32.0, 72.0, hitVoxel.y * _VoxelSize)`, scaling fog by `0.82..1.12`
- then blends detailed terrain toward `SkyColour(viewDirection)`.

`FarTerrain.shader` instead computes only `saturate(distance / max(1.0, _AerialDistance))`, squares it, scales it by `0.82`, and blends toward `_AerialColour`. With the default `_AerialDistance` of 9000 m, far-terrain haze near the detailed-terrain handoff is very small while detailed terrain can already receive roughly a 40% sky blend. This matches the reported blue-vs-less-fogged tint break.

## Decision
Proceed with the minimal shader-policy fix: give far terrain the same near-field `SkyColour` fog and low-altitude modulation as detailed terrain while preserving its existing additional long-range `_AerialColour` haze. Do not change LOD geometry or handoff distances.
