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

- [x] Trace active LOD coverage into render-visible solid chunk publication and identify the overlap path.
- [x] Record each diagnostic/replay/CI experiment in a numbered `experiment-*.md` file as results are obtained.
- [x] Add a focused regression that is red before the ownership mechanism exists.
- [x] Implement the smallest invariant-preserving fix: keep residency overlap, but give final solid draw staging exclusive hierarchy ownership.
- [x] Push production/test work to `fixes/agent-4` and validate the exact ownership regression with targeted CI (`66768a1a93fb92c468590925f3a07603e1dbdec6`, success).
- [ ] Replay the original capture in the real standalone player and verify all three marked regions.
- [ ] Record final replay evidence, set terminal `issue.json` bookkeeping, and move the entire capture to `SceneIssues/closed/` in a separate commit.

## Current verification state

- Pre-fix targeted run `32887385236` / request `86d3e4325f3536bb90bf5454081b68d1da6f66fe` was red because `SurfaceLodVisibleOwnership` did not yet exist.
- Post-fix targeted run `32887616593` / request `66768a1a93fb92c468590925f3a07603e1dbdec6` passed the exact focused ownership regression.
- First exact-view replay run `32890369760` / request `e83d91c68aaf90a257a5a81a8dd69d795f2368a9` was inconclusive: the runner was occupied by an interactive Unity editor before tests started, and the new replay filter had not yet been mapped into the shared standalone-player capture script.
- Next step is a fresh replay request using the shared player path with the saved issue fixture and original 1364x836 framing. Do not close the issue unless that replay is clean.
