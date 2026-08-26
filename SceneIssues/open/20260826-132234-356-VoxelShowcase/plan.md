# Plan — 20260826-132234-356-VoxelShowcase

## Goal
Remove the visibly jagged dirt/grass boundary highlighted in the captured VoxelShowcase view while preserving one deterministic authoritative voxel representation for rendering and collision.

## Scope
- Kentridge road-shoulder voxel authoring responsible for the dirt/grass seam.
- Focused geometry regression covering the captured boundary behavior.
- Scene issue documentation and required CI/bookkeeping only.

## Constraints
- Preserve determinism, single source of truth, and server authority.
- Refine the deterministic CPU-authored shoulder geometry; do not add a rendering-only surface that can disagree with collision/world state.
- Do not invoke Unity directly; validate with targeted CI.
- Do not create a new scene capture.
- Keep targeted CI under five minutes.

## Acceptance
- Both marked dirt/grass boundaries are addressed by the same deterministic road-shoulder rule.
- The grassy shoulder begins flush with the Dirt carriageway, uses one-decimetre cross-slope bands, and no adjacent authored band rises by more than one decimetre at the regression scale.
- The existing 3 m shoulder width and 2 m outer rise are preserved.
- A focused regression fails on the previous five-band behavior and passes with the fix.
- `ci/single-test` is green on the requested commit.
- The issue is moved to `closed` with terminal fields referencing the production fix SHA.

## Checklist
- [x] Verify assignment exists on master and branch is current with master.
- [x] Inspect capture metadata/circled regions and relevant terrain surface code.
- [x] Record experiment 001 identifying road-shoulder quantization.
- [ ] Add a focused regression for shoulder granularity and flush contact.
- [ ] Implement the smallest deterministic authoring fix.
- [ ] Push production/test fix head.
- [ ] Reset CI branch, submit targeted test request, and verify green.
- [ ] Record CI experiment result and finalize findings.
- [ ] Commit terminal bookkeeping and move open → closed.
- [ ] Integrate to current master and verify remote state.

## Findings
- Assignment began from `bfccb29f34f2373ae7cafac5a38e21a7c2e9ba86`, matching master at assignment time; the feature branch remains ahead only by this issue's documentation.
- Capture note identifies jagged dirt/grass contacts in two marked regions near the Kentridge main-spine road corridor.
- Current source is `Assets/Game/WorldBuilder/Generation/Voxel/KentridgeTownSurfaceCatalogue.cs`; it authors five 6 dm Moss bands per side, each 4 dm higher than the previous band. The first grass strip is therefore 4 dm above the Dirt core and the 3 m shoulder has only five cross-slope levels.
- Because this catalogue writes the authoritative generated voxels, a rendering-only blend would violate the single-source-of-truth boundary. The implementation direction is therefore a deterministic CPU authoring refinement: thirty 1 dm bands spanning the same 3 m width, integer-interpolated from 0 dm at the road edge to the existing 20 dm outer rise.
