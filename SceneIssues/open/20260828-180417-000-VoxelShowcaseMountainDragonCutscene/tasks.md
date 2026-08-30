# Tasks

## Source / regression gate
- [x] Supersede midpoint-only turn proof with a realized landing-column contract: three separated interior columns per turn retain path floor/headroom, with occupied support under the centre column.
- [x] Make built-player waypoint arrival verify authored vertical elevation using production `CharacterMotor.Position` feet and `Grounded`, not X/Z alone.
- [x] Implement the traversal obstruction fix: scenic/support mass first, reusable `Carve` corridor second, authored walking floor restored last.
- [x] Require 24 voxels / 2.4 m vertical headroom above the 1.8 m production motor body.
- [x] Add semantic occupied-below + clear-above regression across ramps, turns, final ascent, and summit approach.
- [x] Add grounded route expectations: +4.6 m per tier, +27.6 m sixth high point, +28.0 m summit within 0.75 m.
- [x] Merge current `origin/master` before the earlier implementation pass without touching another assignment.

## Bake-cost discriminator
- [x] Separate cold import from bake work and compare with successful source `3059c8c119a7...` (3:57 cold bake).
- [x] Falsify expanded region-layer hypothesis; +24 Y voxels remain inside existing 512-voxel layers.
- [x] Center headroom clearing to 16 voxels / 1.6 m while retaining 24-voxel height and full visible path; carve volume 5,097,000 -> 2,718,400 (-46.7%).
- [x] Add production-program regression for 0.5 m lateral margin per side around the 0.6 m motor and <=2.8M carve voxels.
- [x] Inspect run `33274279301`: centered clearing still hit the 240 s Unity guard after ~62.3 s cold import.
- [x] Suppress non-serialized far-field capture only during offline VoxelShowcase bake and add scoped restoration regression.
- [x] Merge master `355a7ed08915...`, preserving agent-4 work and adopting the 10-minute GitHub job ceiling.
- [x] Submit exact request `6c96334421b3...` for merged feature `d1895b3b5591...`; inspect its completed failure rather than replacing queued/running work.
- [x] Inspect run `33279296569`: cold import ~60.5 s, bake killed at 241 s / peak ~11.9 GB before the focused test; built-player fallback rejected stale payload because source-matched manifest is absent. Far-field suppression is falsified as sufficient.
- [x] Trace the next hot path to shared `PrimitiveRasteriser`: box carve visits and reads already-empty voxels before discovering `default` is unchanged.
- [x] Add an output-equivalent fast path for `Carve` + `Box` that skips only blocks explicitly encoded `VoxelReadBlockKind.Empty`; never skip mixed blocks because empty-side boundary samples are authoritative.
- [x] Add real-storage behavioral regression covering canonical-empty boxed-carve no-op plus Mixed empty-side boundary clearing, and include it in the exact final acceptance filter.
- [x] Re-check blast radius/cost for the empty-skip change: rasterizer + Mountain Dragon regression only; non-box carve/fill/paint logic and primitive order/footprint unchanged.
- [x] Submit exact request `agent4-mountain-dragon-final-ef0beaf-emptycarve` on CI head `d6077213a499...` for feature source `ef0beaf69baa...`; no queued/running request remains.
- [x] Inspect run `33280962999` attempt 2: bake again killed at 241 s / ~11.7 GB before focused acceptance; stale fallback payload remains unusable. Canonical-empty skipping is falsified as sufficient.
- [x] Confirm `RegionReadView.TryGetWorldBlock` correctly distinguishes canonical Empty from Mixed, so the failed timing result does not invalidate the fast path's semantics.
- [x] Identify the next output-equivalent discriminator: fully covered `Carve + Box` blocks can use existing authoritative `IRegionMutationStore.SetWholeCellBlock(default)` instead of 512 cell iterations; partial edge blocks must retain the current path.
- [ ] Implement the full-8^3-block default replacement only for fully covered `Carve + Box` blocks, retaining canonical-empty skip and all partial/non-box behavior.
- [ ] Extend real-storage regression: full Mixed boundary block clears/collapses with 512 logical writes, while a partial box leaves outside cells/boundary state untouched.
- [ ] Re-check blast radius and cost after the full-block change; no non-box/fill/paint semantics, primitive order, footprint, or serialized output may change.

## Exact-SHA bake / built-player gate
- [ ] Commit the post-full-block candidate on `fixes/agent-4`, then issue the next final exact-SHA request using only existing `ci-test/fixes/agent-4`; do not create another transport or replace queued/running work.
- [ ] Generate and validate source-matched `ShowcaseWorld.bytes` + manifest.
- [ ] Run the exact focused acceptance filter green.
- [ ] Traverse the complete route via production `AutoWalk -> CharacterMotor.Step` with grounded Y proof.
- [ ] Save and human-review approach/base/middle/upper/summit/dialogue captures, including `Hello, I'm Mr. Dragon.`
- [ ] Commit the accepted generated startup payload/manifest and record measured bake/runtime evidence.

## Closure gate
- [ ] After all green gates, complete pending metadata and move only this assignment `open -> pending`.
- [ ] Move only this assignment `pending -> closed`, set `status=fixed` and `resolvedUtc`.
- [ ] Merge latest `origin/master`, preserving master semantic repairs/material retention and agent-4 provenance/capture suppression, then push the exact feature head to `origin/master` non-force; fetch/merge/retry if master advanced.