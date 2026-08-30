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
- [x] Identify the next output-equivalent discriminator: a fully covered non-empty Uniform `Carve + Box` block can use existing authoritative `IRegionMutationStore.SetWholeCellBlock(default)` instead of 512 identical cell iterations.
- [x] Catch and constrain the write-accounting edge case before CI: Mixed blocks may contain sparse payload, so atomic whole-block clearing would alter `RasterResult.VoxelsWritten`; keep Mixed on the existing partial-cell path.
- [x] Restrict the full-8^3 default replacement to fully covered non-empty Uniform `Carve + Box` blocks; retain canonical-empty skip plus all Mixed/partial/non-box behavior.
- [x] Extend real-storage regression: Empty performs no mutation; Uniform full block uses one whole-cell replacement with 512 writes; full Mixed boundary state remains on partial mutation; partial box leaves outside material/boundary state untouched. Keep this regression called by the exact final acceptance filter.
- [x] Re-check blast radius and cost: post-plan candidate changes only `PrimitiveRasteriser` plus the Mountain Dragon storage regression/final-filter call. The existing per-box block-kind lookup is reused; Uniform interior blocks replace up to 512 read/compare/write iterations with one Storage mutation; Mixed/partial and all non-box/fill/paint paths retain their prior mutation semantics, primitive order, footprint, serialized output, and write accounting.

## Fresh-bake regression loop
- [x] Merge current master into the candidate and submit exact request `agent4-mountain-dragon-final-7f522cf-uniformcarve` on CI head `0988cfa0c9d8...` for feature source `7f522cf98183...`, without replacing queued/running work.
- [x] Generate a fresh source-matched startup bake in run `33285254695`; bake completed in ~226 s under the 600 s watchdog (`ShowcaseWorld.bytes` 164,690,080 bytes, digest `b9af6884c926b4f795b8393d49f797910d8e527a`). This clears the prior 241 s bake blocker.
- [x] Inspect the completed focused-test failure and capture artifacts: fresh-bake acceptance reaches semantic traversal validation, then reports first-turn landing floor voxel `(-435,264,-316)` as Empty (`0`) instead of Gravel (`13`); the separate 60 s player capture completed.
- [x] Discriminate the missing landing: source geometry covers the voxel, primitive order is preserved, snapshot/storage reads current semantic state, and the failed cell is not directly eligible for the Uniform full-block fast path. Root cause is composition: castle-owned regions defer generic catalogue features and intentionally discard those deferrals after castle authoring.
- [x] Confirm the concrete overlap: castle landmark center `(256,376)` and minimum 512-voxel ownership reach produce castle regions `x=-1..1, z=-1..1`; the Mountain Dragon footprint currently spans `x=-3..-1, z=-1..1`, and failed voxel `(-435,264,-316)` is region `(-1,0,-1)`.
- [x] Add a focused behavioral regression before production implementation: `MountainDragonCastleRegionOwnershipTests` rejects any Mountain Dragon footprint/castle feature-suppression region overlap and is invoked by the exact final acceptance filter.
- [x] Shift the Mountain Dragon footprint one 512-voxel region west (`OriginX - 512`) so its three X-region columns become `-4..-2`, preserving footprint size, primitive program, and route geometry while removing castle ownership overlap.
- [x] Re-check dependent runtime/evidence coordinates and update only layout-derived artifacts that do not already derive from `ShowcaseMountainDragonLayout`; the issue-owned replay route's absolute X/look-X coordinates moved by exactly 51.2 m.
- [x] Re-check blast radius/cost after the shift: X/Z footprint remains 3x3 region columns, entry terrain height changes 217 -> 218, authored base/summit Y become 219/499, the mountain stays in the same vertical region layer, carve volume/primitive count are unchanged, and the Uniform carve optimization remains intact.
- [ ] Prove all authored landing floor/headroom columns survive with the optimized carve path and no castle-region suppression.

## Exact-SHA bake / built-player gate
- [x] Commit the post-full-block candidate on `fixes/agent-4`, merge current master, then issue the previous exact-SHA request using only existing `ci-test/fixes/agent-4`; no extra transport was created.
- [x] Generate and provenance-validate source-matched `ShowcaseWorld.bytes` + manifest on the Unity runner; run `33285254695` reached the focused test using the fresh payload.
- [ ] Produce/commit the accepted source-matched generated startup payload + manifest before the one remaining final exact-SHA CI request, if repository workflow permits producing it without using the CI transport.
- [ ] Run the exact focused acceptance filter green on the final feature SHA using only `ci-test/fixes/agent-4`.
- [ ] Traverse the complete route via production `AutoWalk -> CharacterMotor.Step` with grounded Y proof.
- [ ] Save and human-review approach/base/middle/upper/summit/dialogue captures, including `Hello, I'm Mr. Dragon.`
- [ ] Record measured bake/runtime evidence and accepted payload provenance.

## Closure gate
- [ ] After all green gates, complete pending metadata and move only this assignment `open -> pending`.
- [ ] Move only this assignment `pending -> closed`, set `status=fixed` and `resolvedUtc`.
- [ ] Merge latest `origin/master`, preserving master semantic repairs/material retention and agent-4 provenance/capture suppression, then push the exact feature head to `origin/master` non-force; fetch/merge/retry if master advanced.
