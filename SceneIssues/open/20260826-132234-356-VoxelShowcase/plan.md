# Plan — 20260826-132234-356-VoxelShowcase

## Goal
Remove the visibly jagged dirt/grass boundary highlighted in the captured VoxelShowcase view without changing authoritative voxel data.

## Scope
- Terrain surface material classification/blending responsible for the dirt/grass seam.
- Focused regression covering the captured boundary behavior.
- Scene issue documentation and required CI/bookkeeping only.

## Constraints
- Preserve determinism, single source of truth, and server authority.
- Keep the fix presentation-only if possible; do not change gameplay voxel semantics.
- Do not invoke Unity directly; validate with targeted CI.
- Do not create a new scene capture.
- Keep targeted CI under five minutes.

## Acceptance
- Both marked dirt/grass boundaries are addressed by the same deterministic presentation rule.
- A focused regression fails on the previous behavior and passes with the fix.
- `ci/single-test` is green on the requested commit.
- The issue is moved to `closed` with terminal fields referencing the production fix SHA.

## Checklist
- [x] Verify assignment exists on master and branch is current with master.
- [ ] Inspect screenshot/circled regions and relevant terrain surface code.
- [ ] Record experiment 001 and add a focused regression.
- [ ] Implement the smallest fix.
- [ ] Push production/test commit.
- [ ] Reset CI branch, submit targeted test request, and verify green.
- [ ] Record experiment result and finalize findings.
- [ ] Commit terminal bookkeeping and move open → closed.
- [ ] Integrate to current master and verify remote state.

## Findings
- Assignment began from `bfccb29f34f2373ae7cafac5a38e21a7c2e9ba86`, matching master at assignment time.
- Capture note identifies jagged dirt/grass contacts in two marked regions.
- Rendering contains dedicated material-presentation and vertex-attribute paths, so the investigation will avoid changing terrain authority unless evidence requires it.
