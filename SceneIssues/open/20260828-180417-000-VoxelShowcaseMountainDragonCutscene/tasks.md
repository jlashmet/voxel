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
- [x] Inspect both prior final-CI attempts and separate cold import from actual bake work: retry imported in ~63 s, then spent ~177 s in bake before the 240 s Unity guard killed it.
- [x] Compare with earlier Mountain Dragon source `3059c8c119a7...`: the same CI bake completed in 3:57, only 3 s under the guard.
- [x] Falsify the expanded-footprint-region hypothesis: +24 Y voxels do not add a 512-voxel region layer at the observed mountain base; X/Z footprint is unchanged.
- [x] Isolate the post-3:57 production delta: natural supports already existed in the successful candidate; `83c50f94...` added full-width headroom carve prisms and `54f2088a...` only bounded them in existing region layers.
- [x] Reduce headroom raster volume without weakening 24-voxel vertical clearance, the normal-movement route, full visible path width, or shared WorldBuilder semantics: centered clear lane is 16 voxels / 1.6 m.
- [x] Add focused production-program regression proving the clear lane leaves 0.5 m lateral margin per side around the 0.6 m motor and caps total carve volume at 2.8M voxels.
- [x] Re-check static blast radius/cost: 13 carve primitives and 76 total primitives unchanged; footprint unchanged at 1200 x 306 x 1200; carve raster volume 5,097,000 -> 2,718,400 voxels (-46.7%); no steady-state runtime work added.
- [x] Inspect exact-SHA run `33274279301`: the optimized source still hit the external 4-minute bake-process wall after ~62.3 s cold import, so carve voxel count is not the dominant remaining bake cost.
- [x] Remove the dominant non-serialized presentation work from only the offline VoxelShowcase bake: `FarFieldStructureStore.CaptureRegion` is suppressed by an instance-scoped `IDisposable` during `GenerateForBakeBlocking`, while runtime capture defaults enabled and `LoadBake` still rebuilds far-field metadata after restoration. Change-journal publication remains because it is constant-time and not the observed scan cost.
- [x] Add the suppression/restoration regression to `MountainDragonNaturalSupportProgramTests` and invoke it from the exact final acceptance filter; nested scope behavior is covered so runtime capture cannot be accidentally re-enabled early.
- [x] Re-check blast radius: semantic radius-8 generation, castle/features, startup snapshot payload, gallery bake, runtime streaming, region footprint, and Mountain Dragon primitive counts are unchanged; only discarded far-field scan work is skipped during the one-time VoxelShowcase offline generation path.

## Exact-SHA bake / built-player gate
- [x] Inspect completed prior request `ee738bc8511160139eb3d7ea39fbde81d8d21877` and its one retry; both validate superseded source `7b5393736485e4411083bf06fd3257e42702b4bb` and cannot satisfy the new source gate.
- [x] Diagnose the repeated bake timeout as a product-side cost regression and implement the bounded centered-clearance fix plus regression before requesting validation again.
- [ ] Submit one fresh final exact-SHA request from the current feature head using only `ci-test/fixes/agent-4`; do not create another transport or replace queued/running work.
- [ ] Generate and validate source-matched `ShowcaseWorld.bytes` + manifest.
- [ ] Run the exact focused acceptance filter green.
- [ ] Traverse the complete route via production `AutoWalk -> CharacterMotor.Step` with grounded Y proof.
- [ ] Save and human-review approach/base/middle/upper/summit/dialogue captures, including `Hello, I'm Mr. Dragon.`
- [ ] Commit the accepted generated startup payload/manifest and record measured bake/runtime evidence.

## Closure gate
- [ ] After all green gates, complete pending metadata and move only this assignment `open -> pending`.
- [ ] Move only this assignment `pending -> closed`, set `status=fixed` and `resolvedUtc`.
- [ ] Merge latest `origin/master`, push the exact feature head to `origin/master` non-force, fetch/merge/retry if master advanced.