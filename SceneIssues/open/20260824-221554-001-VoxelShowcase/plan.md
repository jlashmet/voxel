# Plan — Scene issue 20260824-221554-001-VoxelShowcase

## Goal

Ensure the captured `VoxelShowcase` staircase retains its intended architectural material instead of receiving the grass surface material.

## Scope and constraints

- Work only on this assigned capture and the smallest responsible showcase authoring/material path.
- Reuse the saved `VoxelShowcase` camera pose as the replay fixture where the remote CI/replay tooling permits.
- Keep authoritative voxel/material state deterministic and CPU-owned.
- Remote validation uses the repository's targeted CI path; do not invoke Unity locally.
- Preserve the original screenshot and capture data unchanged.
- `.github/test-request.json` changes belong only on `ci-test/fixes/agent-3`.
- GitHub confirms the original PNG exists, but the available connector does not expose its image bytes. Do not claim direct screenshot inspection unless a later replay/CI artifact supplies visual evidence.

## Acceptance criteria

- [x] Inspect the saved capture metadata and original evidence as far as remote tooling permits; record the binary-image limitation.
- [ ] Reproduce or deterministically confirm why staircase cells receive grass.
- [ ] Identify the smallest responsible material-ownership invariant.
- [ ] Add a focused regression that demonstrates the failure before the production fix.
- [ ] Implement the smallest production fix.
- [ ] Pass the focused regression through targeted CI.
- [ ] Replay/verify the recorded viewpoint after the fix, or document the strongest deterministic replay evidence available from CI.
- [ ] Document every experiment and validation result.
- [ ] Commit and push production/test work to `fixes/agent-3`.
- [ ] In a separate bookkeeping commit, mark the issue fixed and move the full capture to `SceneIssues/closed/`.

## Findings

- The capture note is: `stairs shouldn't be textured as grass`.
- `Assets/Scenes/VoxelShowcase.unity` enables a procedural surface-material pass with `proceduralBakeSurfaceMaterialId: 4` and `proceduralBakeSurfaceDepthCells: 2`. This is a candidate cause, not yet a proven one.
- The recorded issue contains one camera pose and no circle annotations.
- GitHub exposes the screenshot's repository metadata and blob SHA but not its binary image contents through the available connector, so direct visual inspection cannot currently be claimed.
