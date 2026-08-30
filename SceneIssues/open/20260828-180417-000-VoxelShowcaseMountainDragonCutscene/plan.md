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
`IRegionMutationStore.SetWholeCellBlock` already provides an authoritative whole-8^3-block replacement with storage-owned uniform collapse and allocation release. For `PrimitiveMode.Carve` + `PrimitiveShape.Box`, a fully covered **non-empty Uniform** storage block has one exact final state under the current 512-voxel loop: every one of its 512 identical solid cells becomes `default`. Replacing that whole block once with the default cell therefore preserves both serialized state and the existing `RasterResult.VoxelsWritten == 512` accounting.

Do not apply the atomic path to Mixed blocks. Although whole-cell replacement would produce the same final voxel payload, Mixed blocks may contain only sparse occupancy or authored empty-side boundary samples; the existing rasterizer counts only cells that actually change. Keeping Mixed on the cell loop preserves exact boundary behavior and write metrics. Canonical Empty remains an immediate skip.

Implement the narrowly scoped path only when the clipped x/y/z range spans the entire 8^3 logical block and `RegionReadView.TryGetWorldBlock` reports non-empty `VoxelReadBlockKind.Uniform`. Call `SetWholeCellBlock(worldBlock, default, false)`, add 512 logical writes when storage reports a change, and continue. Every Mixed or partially covered edge block remains on the existing per-voxel path. Non-box carve, fill, paint, primitive order, path footprint, runtime semantics, and serialized output remain unchanged.

Extend the real-storage regression to prove: canonical Empty does no mutation; a fully covered Uniform solid uses exactly one whole-cell replacement and reports 512 writes; a fully covered Mixed empty-side-boundary block does **not** use the atomic path and is still cleared by the existing partial mutation; and a partial box preserves cells/boundary state outside the carve.

This is materially stronger than the falsified empty-skip because the 2.7184M-voxel headroom workload intersects large uniform-solid interiors of the authored mountain/support masses, eliminating up to 512 read/compare/write iterations per fully covered solid block without changing sparse/Mixed accounting. Falsified if the next exact cold bake still reaches the 240 s guard.

## Master overlap / closure
Current `master` has advanced since `355a7ed...` and overlaps shared Showcase files. The final merge must preserve both sides: agent-4 startup-bake provenance validation plus master baked-castle semantic repairs in `ShowcaseWorld.GeneratedContent`, and agent-4 offline far-field capture suppression plus master lowered-terrain material retention in `FarFieldStructureStore`. Do not merge by moving refs or discard either semantic change.

## Remaining gates
After the code/regression change, issue the next final exact request using only the existing `ci-test/fixes/agent-4` transport; no queued/running request currently exists and no extra transport is allowed. Require source-matched bake + manifest, green focused acceptance, full grounded built-player route, and human-reviewed captures before open -> pending -> closed. Then merge latest master and push the exact feature head to master non-force.