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
- [x] Inspect both final-CI attempts and separate cold import from actual bake work: retry imported in ~63 s, then spent ~177 s in bake before the 240 s Unity guard killed it.
- [x] Compare with the earlier exact Mountain Dragon candidate `3059c8c119a7...`: the same CI bake completed in 3:57, only 3 s under the guard.
- [x] Falsify the expanded-footprint-region hypothesis: the +24 Y voxels do not add a vertical 512-voxel region layer at the observed mountain base; X/Z footprint is unchanged.
- [x] Isolate the post-3:57 production delta: natural supports already existed in the successful candidate; `83c50f94...` subsequently added full-width headroom carve prisms, while `54f2088a...` only bounded them in the existing region layers.
- [ ] Reduce headroom raster volume without weakening the 24-voxel vertical clearance, normal-movement route, full visible path width, or shared WorldBuilder semantics.
- [ ] Add a focused regression that proves the traversable clearance lane is wider than the 0.6 m motor with lateral margin and bounds total carve volume/cost below the full-width implementation.
- [ ] Re-check primitive count, feature footprint, shared consumers, and one-time bake cost after the optimization.

## Exact-SHA bake / built-player gate
- [x] Use the one authorized final `ci-test/fixes/agent-4` request only once. Request SHA `ee738bc8511160139eb3d7ea39fbde81d8d21877`, source parent `7b5393736485e4411083bf06fd3257e42702b4bb`.
- [x] Use the single permitted retry only on that same workflow job/request SHA; do not create another transport or update the CI ref again.
- [ ] Obtain an authorized exact-SHA validation path for any new source candidate; the already-used CI ref cannot be advanced again under the current workflow rule.
- [ ] Generate and validate source-matched `ShowcaseWorld.bytes` + manifest.
- [ ] Run the exact focused acceptance filter green.
- [ ] Traverse the complete route via production `AutoWalk -> CharacterMotor.Step` with grounded Y proof.
- [ ] Save and human-review approach/base/middle/upper/summit/dialogue captures, including `Hello, I'm Mr. Dragon.`
- [ ] Commit the accepted generated startup payload/manifest and record measured bake/runtime evidence.

## Closure gate
- [ ] After all green gates, complete pending metadata and move only this assignment `open -> pending`.
- [ ] Move only this assignment `pending -> closed`, set `status=fixed` and `resolvedUtc`.
- [ ] Merge latest `origin/master`, push the exact feature head to `origin/master` non-force, fetch/merge/retry if master advanced.
