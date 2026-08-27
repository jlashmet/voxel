# Plan — 20260826-132505-873 VoxelShowcase

## Defect / acceptance
Capture note: `there is a floating mailbox`; no circles, so the whole saved pose is acceptance. The camera looks almost directly at the east-market street lamp at `(1530,549)` dm. Accept only when the saved-camera replay shows that lamp grounded on the working-yard shoulder and continuously supported through the lantern head, with no nearby streetscape regression.

## Competing hypotheses / evidence
1. **Wrong authored elevation. Confirmed primary cause.** The lamp used the macro profile near Y `256` while its column is owned by the stepped working-yard shoulder near Y `232`. Street dressing now derives that placement from the same deterministic shoulder math as the district terrace.
2. **A plot sign/mailbox is floating. Falsified.** The saved camera points at the east-market lamp; the market sign is outside the captured view.
3. **Thin smooth pole collapses. Confirmed secondary cause.** Material 6 is Smooth; the 3×3 pole now explicitly uses `SurfaceStyles.Planar`.
4. **Residual one-voxel-looking gap is placement off-by-one. Falsified by production bounds.** The terrace shoulder fills through `targetY - 1`; `surfaceY == targetY` is the first immediately adjacent voxel, and the lamp cylinder begins at local Y 0. The mechanically-green replay at source `5ad460559b1c1e32ea2babaffa7b55c729617699` still showed the gray base visually retracted above that correct occupancy contact.
5. **Ground-contact cylinder inherits smoothing and retracts visually. Selected.** The foundation-stone base cylinder still emitted surface style 0 while the pole was exact. The fix explicitly marks the visible base `SurfaceStyles.Planar` without moving occupancy.

## Fix / regression / blast radius
`KentridgeStreetDressingCatalogue` keeps the corrected working-yard surface ownership, keeps the thin pole Planar, and now emits the lamp's ground-contact cylinder Planar. `ProgramBuilder.Cylinder` accepts an optional surface style; no global cylinder or renderer behavior changes.

`KentridgeStreetLampSupportPlayTests.CapturedEastMarketLampKeepsPlanarSupportUnderLantern` builds both production catalogues and requires: generated working-yard terrain directly below the captured column; lamp origin at the first adjacent voxel; the base cylinder beginning at that exact contact plane with `SurfaceStyles.Planar`; the pole also Planar; and pole/lantern vertical continuity.

Current source: `badfa4e447e5c61616032b0f48f63d51c2bf73c3`.

Blast radius: Kentridge street-lamp presentation only. Placement math remains bounded deterministic integer work; no terrain/road geometry, renderer-wide semantics, storage reads, allocations, jobs, or workflow files changed.

## Verification history / remaining gates
Old exact request `6d16727f3651fc041c50f96b459c8c11634765a5` eventually succeeded but predates the grounding correction. Exact request `5d55cc0e8e0eef11b531e65e696ac00140610812`, run `33028479281`, passed its focused test/replay mechanically, but direct inspection of its fresh artifact rejected visual acceptance because the gray base still appeared suspended.

Request a new exact-head focused PlayMode test + 30 s saved-pose replay from the current feature SHA. Inspect the fresh artifact directly. Only if the visible gap is gone: commit fresh `verification-final.png`, complete metadata, move `open -> pending -> closed`, set `status=fixed`/`resolvedUtc`, refresh/merge latest master if needed, and advance master non-force.
