# Plan — align authored Kentridge pieces without gaps

## Goal

Resolve `20260824-221508-896-VoxelShowcase`: four marked locations in the saved VoxelShowcase view show authored pieces that do not meet cleanly or leave visible gaps.

The fix must identify the shared placement / boundary-ownership rule behind those seams. It must not patch the four screenshot coordinates or add presentation geometry only for this camera.

## Investigation / validation

- [x] Replay the exact saved camera against the current committed VoxelShowcase bake and compare all four marked locations with the original capture.
- [x] Identify the authoritative generators for the Market Square hard surface and market-stall dressing.
- [x] Correct the real inclusive +X/+Z plaza endpoint mismatch (attempt 1); focused tests passed, fresh replay still failed.
- [x] Give the hard piazza one continuous backing slab (attempt 2); focused tests passed, fresh replay still failed.
- [x] Inspect the fresh-bake replay and project the marked seam back into authored world coordinates.
- [x] Trace the surviving seam to zero-thickness authored interfaces rather than missing backing occupancy.
- [ ] Add a focused red regression requiring material-only border paint and physical overlap at market-stall supports.
- [ ] Apply production attempt 3: keep one geometric piazza slab, paint its border material, and sink market stalls one authored decimetre into the shared surface.
- [ ] Run the focused regression and `KentridgeMarketPiazzaTests`.
- [ ] Regenerate the showcase startup bake and replay this assigned issue's exact saved camera.
- [ ] If the replay passes, move the capture to `SceneIssues/closed/` with terminal fixed bookkeeping; if it fails, stop production changes and record terminal blocked bookkeeping because all three attempts are exhausted.
- [ ] Restore/remove any CI-only request or replay wiring as required by the repository workflow.
- [ ] Do not start another capture; the user explicitly prohibited it.

## Findings

Attempt 1 proved and fixed a real count-vs-inclusive-bound mismatch in both hard and graded Market Square footprints. Attempt 2 proved that the piazza previously depended on exactly touching centre/border occupancy and replaced that with a full backing slab. Both changes are structurally valid, but neither changed the fresh standalone replay, so missing piazza volume is not the final visual owner.

The attempt-2 artifact was then inspected directly. The three lower circles are one continuous light-blue crack, and exact camera projection puts that crack at about `Z = 58.4–58.5 m`. The Market Square is centred at `Z = 52.0 m`, has depth `14.0 m`, and its north decorative border begins at `Z = 58.5 m`. The visible line therefore matches the authored dark-border/light-centre material transition, not the plaza's outer +Z endpoint and not a 6.4 m renderer chunk boundary.

The current piazza program emits one full FoundationStone `Fill` slab followed by four coplanar DarkMasonry `Fill` boxes. `PrimitiveMode.PaintSolid` exists specifically to repaint existing solid voxels without changing occupancy. The authored-boundary contract also states that paint is not geometry: a material-only operation must not create a second boundary field over geometry another primitive already owns. Attempt 2 left that coplanar boundary ownership intact even though it strengthened occupancy underneath.

The remaining marked area is at market-stall feet. `KentridgeTownDressingCatalogue` places the stall at the vertically adapted piazza surface, and its four stone shoes begin at local `y = 0`. Thus the shoes merely touch the hard piazza top; they do not penetrate it. The screenshot shows light-blue exposure around those contact patches. The smallest corresponding support contract is to sink the stall placement by one authored decimetre, so the structural feet overlap the supporting surface instead of relying on exact contact between separately authored solids.

Attempt 3 therefore targets one shared rule: **do not model a visual/material junction as an independent coplanar solid, and do not make supported solids depend on a zero-overlap contact plane.** The hard piazza remains the sole geometric owner of its floor; its dark perimeter becomes paint, while market stalls overlap that floor by one decimetre.

## Three-attempt rule

Production-fix attempts: **2 / 3 completed**. Attempt 3 described above is the final allowed substantive production change. Diagnostics, regression authoring, exact replays, and CI-only request wiring do not increment the count. If attempt 3 fails the fresh exact-camera replay, do not make a fourth production fix; record the issue blocked/open with the failed evidence.

## Acceptance

- All four marked seams/gaps in the saved view are absent after a fresh bake and exact-pose replay.
- Piazza border material does not introduce an independent geometric boundary over the continuous slab.
- Market-stall feet overlap their supporting shared surface instead of merely touching it.
- Nearby unmarked structures retain their intended spacing and silhouette.
- No camera-, screenshot-, or hard-coded issue-coordinate special case is introduced.
- A focused regression proves the final authored-interface contract.
- A fresh standalone exact-camera replay from the regenerated startup bake visually passes.
