# Plan — unify near/far grass presentation

## Reported defect
At the saved VoxelShowcase camera, grass on the right/far field reads as a second texturing system: the blades are dramatically larger/stretched than the tighter near-field grass on the left.

## Evidence and attempt history
1. `SmoothSurface.shader` samples the renderer-owned texture array using shared material sampling/surface tables and world position in base-voxel units.
2. `_VoxelSize` is the constant base voxel size (`0.1m`), not an LOD-dependent scale; the initial LOD-scale hypothesis was falsified.
3. `VoxelFarTerrain` resolves the same application-owned semantic material byte as the near world, but historically discarded it after converting the material to vertex albedo. The far shader therefore had a separate presentation path.
4. Attempt 1 reused `_AlbedoTextures`, `_MaterialSampling`, `_MaterialSurface`, `SurfaceUV`, base-voxel world coordinates, and the same distance attenuation, but reconstructed material identity from interpolated RGB. Its focused source-contract regression went red→green.
5. Exact saved-pose real-player replay of attempt 1 still showed oversized/stretched far grass with healthy surface coverage. That disproved RGB reverse lookup as a sufficient material key.
6. The far mesh already knows the authoritative material byte at each sampled vertex. Attempt 2 will preserve that identity explicitly instead of guessing it back from colour.

## Minimal attempt-2 fix
- Reuse one managed `Vector2[]` scratch buffer for far material IDs alongside the existing position/color buffers.
- Write the exact semantic material byte for each sampled far vertex and publish it through `mesh.uv2`; do the same for the startup fallback.
- Read that dedicated channel in `FarTerrain.shader` and forward a non-interpolated material ID to the fragment stage.
- Select `_MaterialSampling` / `_MaterialSurface` directly from that ID and remove `ResolveMaterialFromAlbedo`.
- Keep vertex color as the authoritative fallback/base tone, and retain the shared world/base-voxel texture basis and distance attenuation from attempt 1.

## Verification
- Preserve the strengthened focused regression red result before production code.
- Run the same exact regression green against the attempt-2 production commit.
- Run the smallest relevant existing far-field regression if needed for mesh publication safety.
- Replay the original `issue.json` camera/FOV at its 1928x836 aspect in the real player and visually verify the right/far grass no longer has the stretched second presentation.
- Remove all temporary replay wiring/fixtures before terminal bookkeeping and master promotion.
