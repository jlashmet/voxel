# Experiment 003 — near/far fog parity implementation

## Hypothesis
Applying the detailed terrain's near-field fog envelope to the far clipmap before the existing long-range haze removes the blue tint discontinuity without changing LOD ownership or geometry.

## What was performed
On production fix commit `856f2bdf983d37434343d6076a81330df5765d26`, changed only `Assets/VoxelEngine/Rendering/Runtime/Shaders/FarTerrain.shader` after the regression fixture was already committed.

The far shader now:
- computes the same `SkyColour` gradient from `_SkyHorizon` / `_SkyZenith`,
- applies `smoothstep(60.0, 300.0, distance) * 0.40`,
- applies the same `32.0..72.0` low-altitude modulation with `lerp(0.82, 1.12, lowAltitude)`,
- blends to `SkyColour(viewDirection)` at the near/detail handoff,
- then preserves the existing squared `_AerialDistance` / `_AerialColour` long-range haze.

No LOD distances, clipmap geometry, streaming policy, terrain material IDs, or unrelated shaders were changed.

## Result
**Source-level hypothesis supported; scene result not yet verified.** The post-fix shader contains every near-field marker guarded by `FarTerrainFogParityTests` while retaining the old long-range haze expression. The feature branch's `.github/test-request.json` remains the repository template and was not edited.

Because targeted Actions did not start, this experiment cannot claim shader compilation/test success or a visual pass.

## What was learned
The smallest responsible code path is the far terrain fragment fog policy; a geometry/streaming change is unnecessary for the diagnosed tint mismatch. Runtime proof remains gated on CI/replay availability.

## Next
Run the focused regression on the exact fix commit and replay the original saved camera before any fixed bookkeeping.
