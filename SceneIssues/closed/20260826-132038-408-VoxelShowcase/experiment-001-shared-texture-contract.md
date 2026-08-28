# Experiment 001 — shared far/near texture contract

## Hypothesis

The reported stretched grass is the far clipmap's coarse interpolated vertex colour presenting as a second grass-texturing path. The near voxel surface uses the renderer-owned texture array and authoritative per-material sampling/scale tables, while `FarTerrain.shader` currently uses only vertex colour.

## Performed

Source before the regression: `92e54eadb94981f6d6e8cce75e41577adedafb93`.

- Confirmed `SmoothSurface.shader` derives its texture coordinates from world position in base-voxel units and consumes `_MaterialSampling`, `_MaterialSurface`, and `_AlbedoTextures`.
- Confirmed `_VoxelSize` is the constant renderer base voxel size (0.1 m), not the current LOD cell size; the initial LOD-dependent coordinate-scale hypothesis is therefore falsified.
- Confirmed `VoxelFarTerrain` resolves the same semantic surface material as the near world but stores only that material's albedo as vertex colour, and `FarTerrain.shader` has no texture-array/material-sampling inputs.
- Added `FarTerrainSharedTexturePresentationTests.FarTerrainReusesVoxelSurfaceTextureSamplingContract` before any production shader edit. It requires the far shader to recover semantic material from the authoritative albedo table and use the shared texture array, material sampling/scale tables, world-space base-voxel coordinate basis, and the same 350 m texture-distance attenuation used at the near-field handoff.

## Expected result

On the current production shader this regression should fail first on `ResolveMaterialFromAlbedo`, because the far shader has no semantic-material recovery or texture-sampling path yet.

## Next

Run this exact regression through `ci-test/fixes/agent-5` and preserve the observed red result before changing production behavior.
