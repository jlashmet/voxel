# Plan

## Acceptance
Human review requires the exact built VoxelShowcase to show a substantial grounded mountain/readable winding ascent, normal movement from base to summit, a supported dragon placeholder, and proximity dialogue `Hello, I'm Mr. Dragon.` Durable approach/base/switchback/summit/dialogue captures and a source-matched checked-in startup bake are mandatory.

## Proven traversal fix
The reusable mountain program orders scenic/support fills -> `Carve` clearance -> restored path floors. The production motor is 0.6 m wide / 1.8 m high with 0.3 m step height; the centered clear lane is 16 voxels / 1.6 m and vertical clearance is 24 voxels / 2.4 m. Route proof requires grounded feet at +4.6 m tiers and +28.0 m summit.

## Bake-cost evidence
Earlier Mountain Dragon source `3059c8c119a7...` completed the same cold bake in 3:57. Centering the clearance lane cut carve volume 5,097,000 -> 2,718,400 voxels (-46.7%), but run `33274279301` still hit the 240 s Unity guard. Offline `FarFieldStructureStore.CaptureRegion` suppression was then added without changing serialized semantics.

After merging master commit `355a7ed08915...` (10-minute GitHub job ceiling), exact request `6c96334421b3...` / run `33279296569` for feature `d1895b3b5591...` still failed before the focused test: cold import took ~60.5 s and the bake was killed at 241 s, peak ~11.9 GB. The built-player fallback then rejected the stale checked-in payload because its source-matched provenance manifest is absent. Far-field suppression is therefore **falsified as sufficient**.

## Next discriminator
Current `PrimitiveRasteriser` scans every voxel in every box-carve block, reads the logical cell, constructs `default`, and only then skips already-empty cells. Mountain headroom adds 13 large box-carve primitives; the last successful source predates that clearance phase.

Implement a narrowly scoped fast path: for `PrimitiveMode.Carve` + `PrimitiveShape.Box` only, skip a logical 8^3 block only when `RegionReadView.TryGetWorldBlock` reports `VoxelReadBlockKind.Empty`. Do **not** skip mixed blocks, even if occupancy-empty, because empty-side authored boundary samples are authoritative. Non-box carve, fill, paint, runtime semantics, primitive order, footprint, and serialized output remain unchanged. Add a behavioral regression proving implicit-empty box carve is a no-op while mixed/boundary state is preserved by the existing path. Falsified if the next exact cold bake still reaches 240 s.

## Remaining gates
After the code/regression change, submit one new exact final request using only `ci-test/fixes/agent-4`. Require source-matched bake + manifest, green focused acceptance, full grounded built-player route, and human-reviewed captures before open -> pending -> closed. Then merge latest master and push the exact feature head to master non-force.