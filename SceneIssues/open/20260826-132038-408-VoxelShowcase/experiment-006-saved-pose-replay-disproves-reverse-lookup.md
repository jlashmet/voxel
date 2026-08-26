# Experiment 006 — saved-pose replay disproves reverse material lookup

## Hypothesis
Reusing the near-surface texture array and world/base-voxel UV math in `FarTerrain.shader`, while recovering material policy from far-mesh vertex albedo, would remove the reported stretched second grass presentation.

## What was performed
- Production candidate: `506d4b37a42639bb1b9d48f1796e7794446d3c40` (`Unify far terrain grass texture sampling`).
- Exact saved-capture real-player replay: workflow run `32994156552`, artifact `9616855094`.
- Replay used `SceneIssues/open/20260826-132038-408-VoxelShowcase/issue.json`, the recorded camera pose/FOV, and the original 1928x836 aspect.
- Real-player replay step completed successfully and captured four presented frames; `showcase-003-t044.7s-stationary.png` was visually reviewed.

## Result
**Failed visual gate.** The saved view still shows two visibly different grass presentations. The right/far field samples the grass image, but the blades are dramatically oversized/stretched relative to the tighter grass on the left/near field. Surface diagnostics in the replay remained healthy (`missingVisible=0`), so this is presentation policy rather than missing geometry.

The workflow's later attempt to push its evidence commit failed because temporary capture-script/package-lock edits were left unstaged; that persistence failure does not invalidate the successfully completed real-player render or artifact.

## What was learned
The attempt-1 hypothesis is disproven. Sharing the texture array and UV function is insufficient while far terrain reconstructs semantic material from interpolated RGB. Material identity is already known as a byte in `VoxelFarTerrain.RebuildRingFromCachedHeights`, but is discarded after converting it to vertex albedo. Reverse lookup can therefore select a different material row (and UV scale/texture policy) across coarse triangles.

## Next
Strengthen the focused regression before attempt 2 so far meshes must carry the exact semantic material ID to the shader and `FarTerrain.shader` must consume that channel directly instead of `ResolveMaterialFromAlbedo`. Then obtain a red targeted-CI result before changing production code.