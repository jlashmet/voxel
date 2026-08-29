# Tasks

## Source / regression gate
- [x] Supersede midpoint-only turn proof with a realized landing-column contract: three separated interior columns per turn retain path floor/headroom, with occupied support under the centre column.
- [x] Make built-player waypoint arrival verify authored vertical elevation using production `CharacterMotor.Position` feet and `Grounded`, not X/Z alone.
- [x] Implement the traversal obstruction fix: scenic/support mass first, reusable `Carve` corridor second, authored walking floor restored last.
- [x] Require 24 voxels / 2.4 m vertical headroom above the 1.8 m production motor body.
- [x] Add semantic occupied-below + clear-above regression across ramps, turns, final ascent, and summit approach.
- [x] Add grounded route expectations: +4.6 m per tier, +27.6 m sixth high point, +28.0 m summit within 0.75 m.
- [x] Merge current `origin/master` before the implementation pass without touching another assignment.

## Bake-cost discriminator
- [x] Separate cold import from bake work and compare with successful source `3059c8c119a7...` (3:57 cold bake).
- [x] Falsify expanded region-layer hypothesis; +24 Y voxels remain inside existing 512-voxel layers.
- [x] Center headroom clearing to 16 voxels / 1.6 m while retaining 24-voxel height and full visible path; carve volume 5,097,000 -> 2,718,400 (-46.7%).
- [x] Add production-program regression for 0.5 m lateral margin per side around the 0.6 m motor and <=2.8M carve voxels.
- [x] Inspect run `33274279301`: centered clearing still hit the 240 s Unity guard after ~62.3 s cold import.
- [x] Suppress non-serialized far-field capture only during offline VoxelShowcase bake and add scoped restoration regression.
- [x] Merge master `355a7ed08915...`, preserving agent-4 work and adopting the 10-minute GitHub job ceiling.
- [x] Submit exact request `6c96334421b3...` for merged feature `d1895b3b5591...`; leave it queued/running without replacement.
- [x] Inspect run `33279296569`: cold import ~60.5 s, bake killed at 241 s / peak ~11.9 GB before the focused test; built-player fallback rejected stale payload because source-matched manifest is absent. Far-field suppression is falsified as sufficient.
- [x] Trace the next hot path to shared `PrimitiveRasteriser`: box carve visits and reads already-empty voxels before discovering `default` is unchanged.
- [ ] Add an output-equivalent fast path for `Carve` + `Box` that skips only blocks explicitly encoded `VoxelReadBlockKind.Empty`; never skip mixed blocks because empty-side boundary samples are authoritative.
- [ ] Add behavioral regression covering implicit-empty box-carve no-op and mixed/boundary safety; include it in the exact final acceptance filter.
- [ ] Re-check blast radius/cost: all non-box carve/fill/paint paths, primitive counts/order, footprint, runtime semantics, and serialized output must remain unchanged.

## Exact-SHA bake / built-player gate
- [ ] Submit one new final exact-SHA request from the post-fast-path feature head using only `ci-test/fixes/agent-4`; do not create another transport or replace queued/running work.
- [ ] Generate and validate source-matched `ShowcaseWorld.bytes` + manifest.
- [ ] Run the exact focused acceptance filter green.
- [ ] Traverse the complete route via production `AutoWalk -> CharacterMotor.Step` with grounded Y proof.
- [ ] Save and human-review approach/base/middle/upper/summit/dialogue captures, including `Hello, I'm Mr. Dragon.`
- [ ] Commit the accepted generated startup payload/manifest and record measured bake/runtime evidence.

## Closure gate
- [ ] After all green gates, complete pending metadata and move only this assignment `open -> pending`.
- [ ] Move only this assignment `pending -> closed`, set `status=fixed` and `resolvedUtc`.
- [ ] Merge latest `origin/master`, push the exact feature head to `origin/master` non-force, fetch/merge/retry if master advanced.