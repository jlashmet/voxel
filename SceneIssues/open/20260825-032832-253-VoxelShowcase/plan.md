# Plan — eliminate overlapping terrain representations

## Goal

At every rendered location, the showcase must expose exactly one intended terrain representation. Solid voxel LOD handoffs must not double-submit hierarchy coverage, and the analytic/far-field path must not overlap the voxel surface in a way that produces z-fighting or mixed-resolution stripes.

## Scope

- Trace the full standalone `VoxelShowcase` terrain presentation path: `VoxelSurfaceScheduler`, final solid draw staging, LOD transition geometry, and the analytic/far-terrain renderer.
- Distinguish logical residency from render visibility and identify the actual second submitted representation before changing another ownership rule.
- Do not solve the capture by disabling LOD globally, hiding geometry with depth bias, or weakening coverage/convergence behavior.
- Keep changes limited to the smallest proven terrain-visibility boundary and its focused regressions.

## Acceptance criteria

- A deterministic regression covers the proven duplicate/overlap path and is red before the correcting production change.
- The fixed render path has an explicit non-overlap invariant at the boundary between the two representations involved.
- The focused regression passes through the assigned `ci-test/fixes/agent-4` targeted-CI branch in under five minutes.
- Replaying `20260825-032832-253-VoxelShowcase` in the real standalone player at the captured 1364x836 viewpoint shows one terrain resolution in every marked region.
- The original screenshot remains unchanged and all experiment/replay evidence stays with this capture.

## Tasks

- [x] Trace the initial hierarchy-overlap path and add a focused visible-ownership regression.
- [x] Implement hierarchy-exclusive final solid draw staging and validate that focused regression (`66768a1a93fb92c468590925f3a07603e1dbdec6`, success).
- [x] Map the exact saved fixture into the shared standalone-player capture path at the original framing.
- [x] Replay that first production fix in the real standalone player (`32892693260`) and record that the visual defect remains.
- [x] Trace all remaining terrain submissions and rule out analytic far terrain for the marked regions; the settled replay has a 365.9 m far-field hole while the live voxel handoff is at 57.6 m.
- [ ] Add and prove red a focused flagship-scene regression for the prematurely contracted fine terrain band.
- [ ] Restore the full configured 96 m fine band and validate the focused regression through `ci-test/fixes/agent-4`.
- [ ] Make the exact replay fixture batchmode-safe and rerun the saved standalone view.
- [ ] Record final replay evidence, set terminal `issue.json` bookkeeping, and move the entire capture to `SceneIssues/closed/` in a separate commit.

## Current verification state

- Pre-fix targeted run `32887385236` / request `86d3e4325f3536bb90bf5454081b68d1da6f66fe` was red because `SurfaceLodVisibleOwnership` did not yet exist.
- Post-fix targeted run `32887616593` / request `66768a1a93fb92c468590925f3a07603e1dbdec6` passed the exact focused hierarchy-ownership regression.
- First exact-view replay run `32890369760` / request `e83d91c68aaf90a257a5a81a8dd69d795f2368a9` was inconclusive because the runner was occupied and the replay filter was not yet mapped into the shared standalone-player path.
- Mapped exact-view replay run `32892693260` / request `7c3899aed7cc2e3e5df96f8c2149e64140e4e3cb` produced five real-player screenshots but falsified the hierarchy-only fix: the striped/mixed-resolution terrain remains visible. Its settled diagnostics report voxel bands `0-57.6`, `57.6-115.2`, `115.2-172.8`, `172.8-409.6` metres and a far-terrain hole of `365.9m`, which rules the analytic far field out for the marked foreground/mid-ground regions.
- Exploratory far-boundary run `32929889298` / request `5a6d289120782e77982a3bb616ce7746b6226a65` never executed NUnit; Unity crashed in Burst import with exit 138, so it is infrastructure-inconclusive and supplies no red behavioral evidence.
- The next gate is a deterministic EditMode regression showing that the flagship scene's serialized `0.6` detail scale contracts the intended 96 m fine band to 57.6 m. Do not close the issue until the focused regression is green and a fresh real-player replay at the saved viewpoint is visually clean.
