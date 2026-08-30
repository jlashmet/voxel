# Plan

## Acceptance
Human review requires the exact built VoxelShowcase to show a substantial grounded mountain/readable winding ascent, normal movement from base to summit, a supported dragon placeholder, and proximity dialogue `Hello, I'm Mr. Dragon.` Durable approach/base/switchback/summit/dialogue captures and a source-matched checked-in startup bake are mandatory.

## Proven traversal fix
The reusable mountain program orders scenic/support fills -> `Carve` clearance -> restored path floors. The production motor is 0.6 m wide / 1.8 m high with 0.3 m step height; the centered clear lane is 16 voxels / 1.6 m and vertical clearance is 24 voxels / 2.4 m. Route proof requires grounded feet at +4.6 m tiers and +28.0 m summit.

## Bake-cost evidence
Earlier Mountain Dragon source `3059c8c119a7...` completed the same cold bake in 3:57. Centering the clearance lane cut carve volume 5,097,000 -> 2,718,400 voxels (-46.7%), but run `33274279301` still hit the 240 s Unity guard. Offline `FarFieldStructureStore.CaptureRegion` suppression was then added without changing serialized semantics.

After merging master commit `355a7ed08915...` (10-minute GitHub job ceiling), exact request `6c96334421b3...` / run `33279296569` for feature `d1895b3b5591...` still failed before the focused test: cold import took ~60.5 s and the bake was killed at 241 s, peak ~11.9 GB. The built-player fallback then rejected the stale checked-in payload because its source-matched provenance manifest is absent. Far-field suppression is therefore **falsified as sufficient**.

The next implementation skipped canonical-empty logical blocks for `Carve + Box` while preserving the per-voxel path for Mixed blocks. Exact request `agent4-mountain-dragon-final-ef0beaf-emptycarve` on CI head `d6077213a499...` (feature source `ef0beaf69baa...`) completed two failed attempts in run `33280962999`; attempt 2 again reached the 241 s bake guard at roughly 11.7 GB before the focused test. The fallback again had only the stale checked-in payload. Canonical-empty block skipping is therefore **falsified as sufficient**, although its real-storage regression proves the optimization itself is semantically valid.

## Next discriminator
`IRegionMutationStore.SetWholeCellBlock` already provides an authoritative whole-8^3-block replacement that preserves storage ownership, uniform collapse, allocation release, and full cell semantics. For `PrimitiveMode.Carve` + `PrimitiveShape.Box`, any storage block completely covered by the primitive/sub-volume has one exact final state under the current 512-voxel loop: every cell is `default`. A box has no curved boundary halo, so replacing that fully covered block once with the default cell is output-equivalent even when the source block is Mixed and contains authored empty-side boundary samples.

Implement a narrowly scoped full-block carve path after the existing canonical-empty skip: only when the clipped x/y/z range spans the entire 8^3 logical block, call `SetWholeCellBlock(worldBlock, default, false)` and account for 512 logical voxel writes when storage reports a change. Keep every partially covered edge block on the existing per-voxel path. Non-box carve, fill, paint, primitive order, path footprint, runtime semantics, and serialized output remain unchanged. Extend the real-storage regression to prove a fully covered Mixed boundary block collapses to canonical Empty through the whole-block path and a partial-box edge preserves cells outside the carve.

This is materially stronger than the falsified empty-skip: the 2.7184M-voxel headroom workload contains many occupied or Mixed interior blocks, so the new path removes up to 512 read/compare/write iterations per fully covered block while leaving edge semantics exact. Falsified if the next exact cold bake still reaches the 240 s guard.

## Master overlap / closure
Current `master` has advanced since `355a7ed...` and overlaps shared Showcase files. The final merge must preserve both sides: agent-4 startup-bake provenance validation plus master baked-castle semantic repairs in `ShowcaseWorld.GeneratedContent`, and agent-4 offline far-field capture suppression plus master lowered-terrain material retention in `FarFieldStructureStore`. Do not merge by moving refs or discard either semantic change.

## Remaining gates
After the code/regression change, issue the next final exact request using only the existing `ci-test/fixes/agent-4` transport; no queued/running request currently exists and no extra transport is allowed. Require source-matched bake + manifest, green focused acceptance, full grounded built-player route, and human-reviewed captures before open -> pending -> closed. Then merge latest master and push the exact feature head to master non-force.