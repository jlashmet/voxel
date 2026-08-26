# Plan — align authored Kentridge pieces without gaps

## Goal

Resolve `20260824-221508-896-VoxelShowcase`: four marked locations in the saved VoxelShowcase view show authored pieces that do not meet cleanly or leave visible gaps.

The fix must identify the shared placement / boundary-ownership rule behind those seams. It must not patch the four screenshot coordinates or add presentation geometry only for this camera.

## Investigation / validation

- [x] Replay the exact saved camera against the current committed VoxelShowcase bake and compare all four marked locations with the original capture.
- [x] If the defect still reproduces, identify the physical pieces visible at each mark and trace them to their authoritative generator/catalogue definitions.
- [x] Determine whether the initial plaza endpoint mismatch comes from placement arithmetic / footprint semantics.
- [x] Add the smallest regression that proves the inclusive authored plaza-boundary contract.
- [x] Apply production attempt 1: include the authored positive X/Z endpoint in both plaza layers.
- [x] Run the focused regression and `KentridgeMarketPiazzaTests`; both are green.
- [x] Regenerate the showcase startup bake and replay the exact saved camera for production attempt 1.
- [x] Attempt a current-head exact replay before attempt 2. Two repository-supported CI mailbox requests were pushed on `ci-test/fixes/agent-1`; neither produced a workflow run or `ci/single-test` status, so this is recorded as a CI transport failure rather than a visual result. Temporary replay wiring was removed.
- [x] Trace the surviving seam in the failed fresh bake to final authoritative voxel state: the marked rays land on solid near-field cells, so the remaining defect is presentation/mesh ownership rather than missing authored storage.
- [x] Narrow renderer ownership: the affected cells are Planar hard-surface cells inside chunks that also contain Smooth/Rounded topology. Pure faceted chunks use snapshot occupancy for face exposure; mixed chunks use the sampled presentation-material lattice.
- [ ] Add a focused renderer regression proving that a mixed Smooth+Planar chunk must keep a Planar cap when the authoritative neighbor is air even if the presentation-material sample carries a solid material.
- [ ] Prove the regression red on current mixed-path behavior before production attempt 2.
- [ ] If red for the expected reason, make mixed faceted face exposure use authoritative snapshot occupancy while leaving continuous density/topology presentation semantics intact.
- [ ] Run the focused regression and smallest affected renderer suite on `ci-test/fixes/agent-1`.
- [ ] Regenerate the showcase startup bake and replay the exact saved camera from corrected source.
- [ ] Remove temporary diagnostics/workflow wiring and restore any borrowed one-shot CI file exactly.
- [ ] Mark `issue.json` fixed only when structural and visual checks both pass.

## Findings

The exact replay collapses the three lower marks to one straight exposed row near `Z = 590 dm`. Market Square is centred at `Z = 520 dm` with semantic depth `140 dm`, making `590 dm` its authored positive-Z endpoint.

Kentridge roads convert inclusive authored spans to counted voxel footprints with `max - min + 1`. Both the graded Market Square and hard piazza originally used `plaza.SizeDm * scale` while placing at the authored minimum. Engine footprint/raster contracts are half-open/count-based, so the original 140-voxel depth owned Z=450..589 and omitted the authored Z=590 row. The same mismatch existed on +X.

The active VoxelShowcase composition uses the same `KentridgeVerticalProfile` for the town surface, hard piazza, and vertically adapted dressing, so the earlier height-source hypothesis is rejected for this scene.

The focused regression was proven red on source commit `e7789e5e28bc570ec4e2de25457b845db0c8f7fa`: CI run `32851636363` executed exactly one test, `VoxelEngine.Tests.EditMode.KentridgeMarketPiazzaTests.HardAndGradedPiazzaOwnTheSameInclusiveAuthoredBoundary`, and failed on the hard piazza +X endpoint (`Expected: 1280`, `But was: 1279`). After production commit `971cf8371d95be29fff59675d2c31c2f4d94af65`, the same regression and the full `KentridgeMarketPiazzaTests` class both passed.

However, fresh-bake exact-view run `32853747303` disproved the endpoint mismatch as the complete visual cause. The workflow regenerated `ShowcaseWorld.bytes` from the fixed source, successfully replayed the saved frozen pose in a standalone player, and the resulting frame still shows the broad light-blue seam through all three lower marked regions plus exposure around the market-stall foot. The endpoint fix is therefore structurally correct but visually insufficient. See `experiment-003-fresh-bake-positive-edge-fix.md`.

Subsequent artifact inspection mapped the marked rays into that failed fresh `ShowcaseWorld.bytes`. The marked cells are solid at the surface; the long light-blue line is effectively a missing top-surface strip over solid storage. At representative seam coordinates, the top cells are Planar hard-surface material while the cell directly above is authoritative air.

The renderer has two distinct faceted exposure paths. Pure faceted chunks use `SnapshotFacetedMaskJob`, which derives neighbor occupancy from authoritative snapshot/storage state. Mixed continuous+faceted chunks use `FacetedMaskJob`, whose solidity decisions are based on the sampled presentation-material lattice used by continuous density/topology. That lattice intentionally may carry a nearby solid material on an air-centered negative-density sample for smooth-surface presentation. Material identity therefore is not authoritative occupancy. In a mixed Smooth+Planar chunk this can suppress a legitimate Planar cap even though the storage neighbor is air. This explains why most of the piazza can remain intact while narrow holes occur where Planar hard surface shares a render chunk with Smooth/Rounded terrain.

This renderer-ownership hypothesis is now the gate for attempt 2. Do not make another Kentridge geometry expansion unless the focused mixed-chunk regression disproves it.

## Three-attempt rule

Production-fix attempts: **1 / 3**. Attempt 1 corrected the real plaza endpoint mismatch but failed fresh visual verification. Diagnostics, exact replays, measurement probes, and CI-only stage isolation do not increment the count. After three unsuccessful production fixes, stop modifying the full scene and build a minimal reproduction before another production change.

## Acceptance

- All four marked seams/gaps in the saved view are absent after a fresh bake and exact-pose replay.
- Pieces that are intended to meet share a clear boundary contract: no visible hole, overlap, or unsupported sliver at the join.
- Nearby unmarked structures retain their intended spacing and silhouette.
- No camera-, screenshot-, or hard-coded issue-coordinate special case is introduced.
- A focused regression proves the causal alignment invariant for the final owner.
- A fresh standalone exact-camera replay from the regenerated startup bake visually passes.
