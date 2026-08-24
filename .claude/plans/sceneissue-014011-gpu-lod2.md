# SceneIssue 014011 — coarse LOD material attribution

## Goal
Eliminate the residual blueish/spiraly coarse-terrain bands in the saved VoxelShowcase capture without changing authoritative voxel state or unrelated LOD topology.

## Evidence / current narrowing
- The LOD2 coarse-phase correction improved the captured view, but a residual artifact remains.
- An exact saved-camera CPU-only replay at source commit `62c305cabe91d47ae2ce14d396442e5f9c22598c` stabilized with 414 visible CPU chunks.
- That CPU replay is visually equivalent to the GPU replay at the same saved view apart from HUD/timing-level pixel noise, proving the remaining artifact is shared by CPU and GPU rather than GPU-specific.
- A GPU Planar/Sharp inclusion mismatch was identified separately. It is real parity debt, but it cannot explain the residual `014011` artifact because the CPU replay contains the same artifact.
- Current lead: for `SourceStep > 1`, coarse sample material/surface attribution searches exposed crossings along all six axes. On sloped layered terrain, that can potentially select a nearer lateral or buried material instead of the material exposed on the reconstructed top surface.

## Scope
- Shared coarse sampling/material attribution plus focused EditMode coverage.
- Build the next test as a bare-bones sloped/layered reproduction so this investigation complies with the SceneIssues three-attempt rule regardless of how prior full-scene attempts are counted.
- Do not broaden GPU LOD cutover as part of this issue.
- Keep step-4/step-8 behavior unchanged unless a focused regression proves the shared helper intentionally applies there.

## Acceptance criteria
1. A minimal sloped/layered fixture proves or disproves the current material-attribution hypothesis before production code changes.
2. If proven, the production fix uses deterministic integer logic and preserves the single authoritative voxel truth.
3. The focused regression passes together with relevant existing coarse density/material/topology regressions.
4. The exact saved VoxelShowcase view no longer contains the circled artifact on the shared CPU path and GPU path.
5. Only after replay verification, mark the SceneIssue fixed in a separate bookkeeping commit.

## Work
- [x] Apply and verify the coarse-phase correction.
- [x] Replay the exact saved view with GPU cutover disabled and compare against GPU rendering.
- [x] Rule out GPU-only topology/cutover as the cause of the residual artifact.
- [ ] Locate the shared coarse material-attribution implementation and existing tests.
- [ ] Build a minimal failing sloped/layered reproduction.
- [ ] Run that regression through targeted Unity CI and require a red result before production changes.
- [ ] If the regression proves the hypothesis, implement the smallest deterministic fix.
- [ ] Re-run the focused regression and relevant existing coarse regressions.
- [ ] Replay the exact saved view on CPU and GPU.
- [ ] Mark `issue.json` fixed in a separate bookkeeping commit only after visual verification.

## Failed / deprioritized hypotheses
- **GPU-only LOD2 cutover/topology** — disproven for the residual artifact by the exact CPU/GPU replay.
- **GPU Planar/Sharp inclusion mismatch** — real parity bug, but not causal for the residual `014011` artifact.
