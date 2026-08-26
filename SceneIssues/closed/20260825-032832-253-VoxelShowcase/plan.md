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
- [x] Add and prove red a focused flagship-scene regression for the prematurely contracted fine terrain band (`32932899389`, one requested test failed at 57.6 m vs 96 m).
- [x] Restore the full configured 96 m fine band and validate the focused regression through `ci-test/fixes/agent-4` (`32933287067`, success).
- [x] Make the exact replay fixture batchmode-safe and rerun the saved standalone view (`32933454625`: requested PlayMode fixture passed 1/1 and the real-player replay completed 60 s at the exact saved pose).
- [x] Record final replay evidence, set terminal `issue.json` bookkeeping, and move the entire capture to `SceneIssues/closed/` in a separate commit.

## Final verification state

- Pre-fix targeted run `32887385236` / request `86d3e4325f3536bb90bf5454081b68d1da6f66fe` was red because `SurfaceLodVisibleOwnership` did not yet exist.
- Post-fix targeted run `32887616593` / request `66768a1a93fb92c468590925f3a07603e1dbdec6` passed the exact focused hierarchy-ownership regression.
- First exact-view replay run `32890369760` / request `e83d91c68aaf90a257a5a81a8dd69d795f2368a9` was inconclusive because the runner was occupied and the replay filter was not yet mapped into the shared standalone-player path.
- Mapped exact-view replay run `32892693260` / request `7c3899aed7cc2e3e5df96f8c2149e64140e4e3cb` produced five real-player screenshots but falsified the hierarchy-only fix: the striped/mixed-resolution terrain remained visible. Its settled diagnostics reported voxel bands `0-57.6`, `57.6-115.2`, `115.2-172.8`, `172.8-409.6` metres and a far-terrain hole of `365.9m`, ruling the analytic far field out for the marked regions.
- Exploratory far-boundary run `32929889298` / request `5a6d289120782e77982a3bb616ce7746b6226a65` never executed NUnit; Unity crashed in Burst import with exit 138, so it was infrastructure-inconclusive and supplied no behavioral evidence. The exploratory test was removed after the hypothesis was falsified.
- Focused fine-band red run `32932899389` / request `2ecb18ffd236dde643e71cbcbde6df5db2c20cb7` executed exactly one EditMode test and failed on the intended scene policy assertion: `0.6 * 96m = 57.6m`, below the required full 96 m fine band.
- Focused fine-band green run `32933287067` / request commit `be761040ea82cfd5680ccc27ae0b7c496ee40690` succeeded on production commit `ca89c74b653f21f936218c60464079641f12459f`.
- Exact saved-view run `32933454625` / request commit `c2ecb585ff5e081a52e363ad48358e3cef3a6007` targeted feature source `e47afdab13278d3cfdce79f43805b7feb4f89cac`. The requested PlayMode fixture passed 1/1; the standalone player completed the original 1364x836 saved-camera replay for 60 s with `missingVisible=0`, settled bands `0-96`, `96-192`, `192-288`, `288-409.6` metres, and artifact `9594146911`. The final presented frame is visually clean in all three original marked regions. The job-level conclusion was cancelled only after these requested verification/evidence steps completed and the artifact was uploaded, at the workflow time ceiling; the independent focused targeted regression above is formally green.

Closure decision: the proven stale `m_DetailBandScale = 0.6` presentation regression is corrected by production commit `ca89c74b653f21f936218c60464079641f12459f`, deterministic targeted CI is green, and the exact original standalone replay is visually clean. Close this capture and do not start another one.
