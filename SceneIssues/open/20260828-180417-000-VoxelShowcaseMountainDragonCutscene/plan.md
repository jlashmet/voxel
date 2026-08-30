# Plan

## Acceptance
Human review requires the exact built VoxelShowcase to show a substantial grounded mountain/readable winding ascent, normal movement from base to summit, a supported dragon placeholder, and proximity dialogue `Hello, I'm Mr. Dragon.` Durable approach/base/switchback/summit/dialogue captures and a source-matched checked-in startup bake are mandatory.

## Proven traversal fix
The reusable mountain program orders scenic/support fills -> `Carve` clearance -> restored path floors. The production motor is 0.6 m wide / 1.8 m high with 0.3 m step height; the centered clear lane is 16 voxels / 1.6 m and vertical clearance is 24 voxels / 2.4 m. Route proof requires grounded feet at +4.6 m tiers and +28.0 m summit.

## Bake-cost evidence
Earlier Mountain Dragon source `3059c8c119a7...` completed the same cold bake in 3:57. Centering the clearance lane cut carve volume 5,097,000 -> 2,718,400 voxels (-46.7%), but run `33274279301` still hit the 240 s Unity guard. Offline `FarFieldStructureStore.CaptureRegion` suppression was then added without changing serialized semantics.

After merging master commit `355a7ed08915...` (10-minute GitHub job ceiling), exact request `6c96334421b3...` / run `33279296569` for feature `d1895b3b5591...` still failed before the focused test: cold import took ~60.5 s and the bake was killed at 241 s, peak ~11.9 GB. The built-player fallback then rejected the stale checked-in payload because its source-matched provenance manifest is absent. Far-field suppression is therefore **falsified as sufficient**.

The next implementation skipped canonical-empty logical blocks for `Carve + Box` while preserving the per-voxel path for Mixed blocks. Exact request `agent4-mountain-dragon-final-ef0beaf-emptycarve` on CI head `d6077213a499...` (feature source `ef0beaf69baa...`) completed two failed attempts in run `33280962999`; attempt 2 again reached the 241 s bake guard at roughly 11.7 GB before the focused test. The fallback again had only the stale checked-in payload. Canonical-empty block skipping is therefore **falsified as sufficient**, although its real-storage regression proves the optimization itself is semantically valid.

The Uniform full-block discriminator was then implemented: only fully covered non-empty Uniform `Carve + Box` blocks use `SetWholeCellBlock(default)` and preserve 512 logical writes; Mixed/partial blocks retain the old cell path. After merging current master, exact request `agent4-mountain-dragon-final-7f522cf-uniformcarve` (CI head `0988cfa0c9d8...`, feature `7f522cf98183...`) produced a fresh source-matched bake in run `33285254695`. The bake completed in roughly 226 seconds under the 600-second watchdog, producing a 164,690,080-byte payload with digest `b9af6884c926b4f795b8393d49f797910d8e527a`. The previous bake-time blocker is therefore **cleared**.

## Fresh-bake acceptance regression
Run `33285254695` progressed into the focused acceptance test and exposed a narrower correctness failure that earlier timed-out runs could not reach. `MountainDragonPathHeadroomBakeTests.AssertWalkableLanding` found the first-turn walking-floor voxel `(-435,264,-316)` empty (`0`) rather than Gravel (`13`). The independent 60-second built-player capture completed, but the exact acceptance filter is red and the feature remains open.

`IRegionMutationStore.SetWholeCellBlock` explicitly promises complete logical-cell replacement while preserving surface/boundary semantics, so do not assume the atomic storage operation is the cause merely because the failure appeared after the performance change. Discriminate three hypotheses with source/runtime evidence: (1) whole-block replacement differs from the partial mutation path in a state that the existing storage regression missed; (2) rasterizer read/mutation ordering makes a later primitive observe stale or differently collapsed block state; or (3) the mountain program's landing geometry/order has an off-by-one/gap that was always latent and only became visible once a fresh bake could finish.

Before implementation, add a focused behavioral regression that reproduces the missing landing mechanism using the production program/storage path. The smallest acceptable fix must keep the successful Uniform fast path for the large interior carve workload unless evidence directly falsifies its semantics. Re-run the authored landing/headroom contract across all switchbacks and the final ascent; do not optimize by weakening the floor-material or grounded traversal acceptance.

## Blast radius / cost
Keep the correction limited to the proven failing layer: ideally the mountain authoring program/test if geometry is wrong, or the rasterizer/storage call site plus its existing real-storage regression if atomic mutation is semantically wrong. Do not change non-box carve, fill/paint behavior, world footprint, other scene assignments, or CI workflow. Preserve the measured bake improvement; a correctness fix that returns the cold bake to the prior timeout is not acceptable.

## Master overlap / closure
The current merged candidate preserves both sides of shared Showcase overlap: agent-4 startup-bake provenance validation plus master baked-castle semantic repairs in `ShowcaseWorld.GeneratedContent`, and agent-4 offline far-field capture suppression plus master lowered-terrain material retention in `FarFieldStructureStore`. Any later master refresh must preserve both semantic changes.

## Remaining gates
After the focused regression is fixed, merge latest master if it advanced and issue the next exact request using only the existing `ci-test/fixes/agent-4` transport; the prior run is completed, so no queued/running request would be replaced. Require source-matched bake + manifest, green focused acceptance, full grounded built-player route, and human-reviewed captures before open -> pending -> closed. Then final-sync latest master and push the exact feature head to master non-force.
