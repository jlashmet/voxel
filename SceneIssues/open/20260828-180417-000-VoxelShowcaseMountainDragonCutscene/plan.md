# Plan

## Acceptance
Human review requires the exact built VoxelShowcase to show a substantial grounded mountain/readable winding ascent, normal movement from base to summit, a supported dragon placeholder, and proximity dialogue `Hello, I'm Mr. Dragon.` Durable approach/base/switchback/summit/dialogue captures and a source-matched checked-in startup bake are mandatory.

## Proven traversal fix
The reusable mountain program orders scenic/support fills -> `Carve` clearance -> restored path floors. The production motor is 0.6 m wide / 1.8 m high with 0.3 m step height; the centered clear lane is 16 voxels / 1.6 m and vertical clearance is 24 voxels / 2.4 m. Route proof requires grounded feet at +4.6 m tiers and +28.0 m summit.

## Bake-cost evidence
Earlier Mountain Dragon source `3059c8c119a7...` completed the same cold bake in 3:57. Centering the clearance lane cut carve volume 5,097,000 -> 2,718,400 voxels (-46.7%), but run `33274279301` still hit the 240 s Unity guard. Offline `FarFieldStructureStore.CaptureRegion` suppression was then added without changing serialized semantics.

After merging master commit `355a7ed08915...` (10-minute GitHub job ceiling), exact request `6c96334421b3...` / run `33279296569` for feature `d1895b3b5591...` still failed before the focused test: cold import took ~60.5 s and the bake was killed at 241 s, peak ~11.9 GB. The built-player fallback then rejected the stale checked-in payload because its source-matched provenance manifest is absent. Far-field suppression is therefore **falsified as sufficient**.

Canonical-empty box-carve skipping also proved insufficient in run `33280962999`, although its real-storage regression remains valid. The next Uniform full-block discriminator was restricted to fully covered non-empty Uniform `Carve + Box` blocks, using `SetWholeCellBlock(default)` while preserving 512 logical writes; Mixed/partial/non-box behavior remains on the prior paths.

After merging current master, exact request `agent4-mountain-dragon-final-7f522cf-uniformcarve` (CI head `0988cfa0c9d8...`, feature `7f522cf98183...`) produced a fresh source-matched bake in run `33285254695`. The bake completed in roughly 226 seconds under the 600-second watchdog, producing a 164,690,080-byte payload with digest `b9af6884c926b4f795b8393d49f797910d8e527a`. The previous bake-time blocker is therefore **cleared** and the Uniform fast path must be preserved unless directly falsified.

## Fresh-bake acceptance regression and root cause
Run `33285254695` progressed into focused semantic acceptance and found the first-turn walking-floor voxel `(-435,264,-316)` empty (`0`) rather than Gravel (`13`). The independent 60-second built-player capture completed, but the exact acceptance filter remained red.

The failure is not a source-level turn gap: the voxel resolves inside the authored first-turn landing floor. `FeatureRegionBuild` preserves primitive order, and the mountain program has no later carve after floor restoration. The failed voxel is also at the bottom of its 8-voxel storage block while landing clearance begins above the floor, so that target is not directly cleared by the Uniform full-block optimization. Storage mutation/read handles share the current NativeArray-backed block refs, and semantic snapshot capture serializes those live refs, so the missing Gravel is genuine final world state rather than a stale test decoder.

The actual loss happens at the showcase composition boundary. `ShowcaseWorld` intentionally defers generic catalogue features for `_castleRegionSet` while the castle is authored and then clears `_deferredFeatureRegions` rather than replaying them. The castle is centered at `(256,376)` and `QueueLandmarks` enforces a minimum ownership reach of one full 512-voxel region, giving owned X/Z region ranges `-1..1`. The current Mountain Dragon footprint (`OriginX=-1200`, `OriginZ=-400`, `FootprintEdge=1200`) spans X regions `-3..-1` and Z regions `-1..1`; failed voxel `(-435,264,-316)` is therefore in owned region `(-1,0,-1)`, where the mountain feature is deliberately omitted.

Before production change, add a regression that derives both production region ranges and rejects any Mountain Dragon footprint/castle feature-suppression overlap. Include that contract in the exact final acceptance entry point. The intended smallest fix is to translate the Mountain Dragon exactly one region west: `OriginX -= ShowcaseWorld.RegionVoxelEdge` (from `-1200` to `-1712`). That changes its X span from regions `-3..-1` to `-4..-2`, eliminating overlap while keeping exactly three footprint X columns, all mountain dimensions, primitive count, carve volume, elevations, and route-relative geometry unchanged.

## Blast radius / cost
Prefer layout translation over changing castle suppression semantics. Replaying deferred generic features over a completed castle would allow arbitrary catalogue fills/carves to overwrite castle-owned surfaces and has a much larger cross-feature blast radius. A one-region Mountain Dragon translation is feature-local and keeps the same 1200x1200 footprint and same per-instance program.

The offline showcase already materializes the full eight-region streaming radius. Shifting a three-column feature footprint west by one region changes which region columns receive mountain authoring rather than increasing the mountain's region count. Verify all driver/evidence coordinates derive from `ShowcaseMountainDragonLayout`; update only any literal evidence data discovered by the audit. Keep the Uniform carve optimization unchanged and require the next fresh bake to remain inside the now-cleared cost envelope.

## Master overlap / closure
The current candidate includes master `d4b31a700bae...` via merge `8162a2d911c...`. It preserves both sides of prior shared Showcase overlap: agent-4 startup-bake provenance validation plus master baked-castle semantic repairs in `ShowcaseWorld.GeneratedContent`, and agent-4 offline far-field capture suppression plus master lowered-terrain material retention in `FarFieldStructureStore`. Any later master refresh must preserve both semantic changes.

## Remaining gates
After the pre-fix overlap regression is committed, apply only the layout translation and necessary derived evidence updates, then source-review the full route/headroom and cost contracts. Re-check current master immediately before the single remaining final exact-SHA CI request and use only existing `ci-test/fixes/agent-4`. Do not replace a queued/running request. Require source-matched bake + manifest, green focused acceptance, full grounded built-player route, and human-reviewed captures before open -> pending -> closed. Then final-sync latest master and push the exact feature head to master non-force.
