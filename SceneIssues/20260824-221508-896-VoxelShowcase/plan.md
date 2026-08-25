# Plan — align authored Kentridge pieces without gaps

## Goal

Resolve `20260824-221508-896-VoxelShowcase`: four marked locations in the saved VoxelShowcase view show authored pieces that do not meet cleanly or leave visible gaps.

The fix must identify the shared placement / boundary-ownership rule behind those seams. It must not patch the four screenshot coordinates or add presentation geometry only for this camera.

## Investigation / validation

- [ ] Replay the exact saved camera against the current committed VoxelShowcase bake and compare all four marked locations with the original capture.
- [ ] If the defect still reproduces, identify the physical pieces visible at each mark and trace them to their authoritative generator/catalogue definitions.
- [ ] Determine whether the gaps come from placement arithmetic, footprint/envelope mismatch, facade alignment, road/plot boundary ownership, or voxel rasterisation at touching boundaries.
- [ ] Add the smallest regression that fails on the causal alignment invariant.
- [ ] Implement the smallest production fix in the owning planner/compiler rather than screenshot-specific coordinates.
- [ ] Run the focused regression and the smallest affected Kentridge/worldgen suite.
- [ ] Regenerate the showcase startup bake if the fix changes baked geometry, then replay the exact saved camera from the committed bake.
- [ ] Remove temporary diagnostics/workflow wiring and restore any borrowed one-shot CI file exactly.
- [ ] Mark `issue.json` fixed only when structural and visual checks both pass.

## Three-attempt rule

Count only genuine production fixes as attempts. Diagnostics, exact replays, measurement probes, and CI-only stage isolation are not production attempts. After three unsuccessful production fixes, stop modifying the full scene and build a minimal reproduction before another production change.

## Acceptance

- All four marked seams/gaps in the saved view are either absent in current repository state or corrected by a reusable generator invariant.
- Pieces that are intended to meet share a clear boundary contract: no visible hole, overlap, or unsupported sliver at the join.
- Nearby unmarked structures retain their intended spacing and silhouette.
- No camera-, screenshot-, or hard-coded issue-coordinate special case is introduced.
- A focused regression proves the causal alignment invariant.
- A fresh standalone exact-camera replay from the committed startup bake visually passes.
