# Plan — eliminate overlapping voxel terrain LODs

## Goal

At every rendered location, the solid voxel surface must expose exactly one logical LOD coverage leaf. A refined child set must replace its coarse parent atomically, and a merged parent must replace all active descendants atomically, even while older meshes remain resident for reuse or while asynchronous builds complete.

## Scope

- Trace `VoxelSurfaceScheduler` from desired/completed LOD state through `SurfaceLodActiveCoverage` into the solid chunks that the render pass is allowed to draw.
- Distinguish logical residency from render visibility; do not solve the capture by shrinking rings, disabling LOD, hiding geometry with depth bias, or weakening coverage/convergence behavior.
- Keep changes limited to the smallest solid-surface LOD publication/visibility subsystem and its focused regressions.

## Acceptance criteria

- A deterministic regression reproduces an ancestor/descendant LOD handoff with both meshes resident and proves the renderer-visible set never contains overlapping hierarchy coverage.
- Stale/out-of-order completion cannot reactivate or leave visible a non-active LOD node.
- The focused regression passes through the assigned `ci-test/fixes/agent-4` targeted-CI branch in under five minutes.
- Replaying `20260825-032832-253-VoxelShowcase` at the captured viewpoint shows one terrain resolution in every marked region.
- The original screenshot remains unchanged and all experiment/replay evidence stays with this capture.

## Tasks

- [ ] Trace active LOD coverage into render-visible solid chunk publication and identify the overlap path.
- [ ] Record each diagnostic/replay/CI experiment in a numbered `experiment-*.md` file.
- [ ] Add a focused regression that fails on the overlapping-coverage behavior before the fix.
- [ ] Implement the smallest invariant-preserving fix.
- [ ] Push production/test work to `fixes/agent-4` and validate the exact regression with targeted CI.
- [ ] Replay the original capture and verify all marked regions.
- [ ] Record final evidence, set terminal `issue.json` bookkeeping, and move the entire capture to `SceneIssues/closed/` in a separate commit.
