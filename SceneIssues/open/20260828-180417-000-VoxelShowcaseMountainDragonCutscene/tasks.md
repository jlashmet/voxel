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
- [x] Generate a fresh source-matched startup bake in run `33285254695`; bake completed in ~226 s under the 240 s Unity subprocess watchdog (`ShowcaseWorld.bytes` 164,690,080 bytes, digest `b9af6884c926b4f795b8393d49f797910d8e527a`). This cleared the prior bake blocker for the partially suppressed layout.
- [x] Inspect the completed focused-test failure and capture artifacts: fresh-bake acceptance reaches semantic traversal validation, then reports first-turn landing floor voxel `(-435,264,-316)` as Empty (`0`) instead of Gravel (`13`); the separate 60 s player capture completed.
- [x] Discriminate the missing landing: source geometry covers the voxel, primitive order is preserved, snapshot/storage reads current semantic state, and the failed cell is not directly eligible for the Uniform full-block fast path. Root cause is composition: castle-owned regions defer generic catalogue features and intentionally discard those deferrals after castle authoring.
- [x] Confirm the concrete overlap: castle landmark center `(256,376)` and minimum 512-voxel ownership reach produce castle regions `x=-1..1, z=-1..1`; the Mountain Dragon footprint spans `x=-3..-1, z=-1..1`, and failed voxel `(-435,264,-316)` is region `(-1,0,-1)`.
- [x] Add a focused behavioral regression before production implementation: `MountainDragonCastleRegionOwnershipTests` rejects any Mountain Dragon footprint/castle feature-suppression region overlap and is invoked by the exact final acceptance filter.
- [x] Shift the Mountain Dragon footprint one 512-voxel region west (`OriginX - 512`) so its three X-region columns become `-4..-2`, preserving footprint size, primitive program, and route geometry while removing castle ownership overlap.
- [x] Re-check dependent runtime/evidence coordinates and update only layout-derived artifacts that do not already derive from `ShowcaseMountainDragonLayout`; the issue-owned replay route's absolute X/look-X coordinates moved by exactly 51.2 m.
- [x] Confirm geometric/Y blast radius after shift: footprint remains 3x3, terrain 217 -> 218, base/summit Y 219/499, same vertical region layer, same primitive count and nominal carve volume.
- [x] Submit the completed final request only through existing `ci-test/fixes/agent-4`: CI SHA `517acdd1e7498f703b1a8355f2dc026261f33e97`, exact feature parent `40e01319e0ddc05cd135d5c6d2577d71a398492c`, run `33287514117`.
- [x] Inspect run `33287514117` attempts 1 and 2 without replacing the request: both source-matched fresh bakes were killed at 241 s before focused acceptance; attempt 1 peak RSS was 10.5 GB, below the 14 GB ceiling; fallback player failure was only missing fresh manifest provenance.
- [x] Correct the cost model: moving out of castle ownership changes effective mountain authoring from 6 active footprint regions to all 9 (+50%); unchanged nominal footprint/primitive count does not imply unchanged bake cost.
- [x] Locate the newly exercised hot path responsible for the 9-region layout exceeding the 240 s bake contract: the mountain emits many convex frusta, and fully interior canonical-empty 8^3 blocks still execute 512 identical contains/read/write iterations each.
- [x] Add a focused behavioral/cost regression before the next production optimization: `FrustumFillAtomicallyAuthorsFullyInteriorEmptyBlockWithoutOverfillingBoundaryBlock` requires one authoritative whole-cell mutation for a fully interior empty block, exact 512 logical writes, and preserves the per-cell boundary-block path.
- [x] Implement an output-equivalent optimization preserving serialized voxel truth and observable write/primitive semantics: commit `090d0032456e...` atomically fills only complete canonical-empty Frustum Fill blocks whose eight extreme voxel centres are all contained.
- [x] Re-check optimization blast radius statically: partial/non-empty frustum blocks, boundary halo, all other primitive modes/shapes, primitive order, footprint, material/surface semantics, and logical write accounting remain on their prior behavior.
- [x] Measure the revised full 9-region bake cost with exact request `5cc2ef4e03ce...` / run `33288586966`: cache miss, ~63.1 s cold import, then killed at 241 s / 10.1 GB before the focused test. The interior-fill fast path is not sufficient.
- [x] Inspect the failed run without replacing it: focused acceptance was skipped; the built-player fallback built successfully but exited 23 because the stale payload lacked the source-matched provenance manifest; all six fallback captures are therefore non-acceptance evidence.
- [x] Identify the next shared hot path: `RasteriseBoundaryHalo` still computes curved signed distance, including integer square-root work, for every voxel in expanded frustum bounds and only then rejects deep-interior samples beyond the 2-voxel halo.
- [x] Add the next focused semantic regression before production implementation: commit `b17d748f149b...` requires a deep-interior frustum cell to remain without boundary metadata while preserving positive solid-side and negative empty-side near-surface halo samples.
- [x] Implement the output-equivalent deep-interior halo skip in commit `da45c64e9658...`: only complete `Fill + Frustum` blocks whose eight extreme voxel centres all have signed boundary distance >32 Q4 bypass the per-voxel halo scan.
- [x] Re-check halo optimization blast radius/cost against the emitter formula: frustum radius is monotonic on the extrusion axis, radial distance is maximal at radial-plane block corners, and cap clearance is minimal at axial endpoints. Qualifying blocks cannot contain a halo sample; up to 512 integer-sqrt distance evaluations become eight corner checks. Boundary/partial/outside blocks and all non-frustum/non-fill modes stay on the existing path.
- [x] Re-measure the full 9-region bake after the halo optimization with exact request `cda51cb0a62a...` / source `408a0fdf8c2b...` / run `33289023179`: ~65.5 s Asset Pipeline refresh, then the fresh bake was killed at the same 241 s watchdog with roughly 10.5 GB peak RSS before focused acceptance.
- [x] Inspect run `33289023179` without replacing it: fallback player build succeeded but rejected the stale world for missing source-matched provenance; all six captures remain non-acceptance evidence.
- [x] Correct the frustum-mode discriminator from source: the single mountain core is `Fill`, while the three asymmetric shoulders and 20+ segmented path-support frusta are `FillIfEmpty`; the two previous `Fill`-only optimizations therefore missed the dominant support authoring mode.
- [x] Verify storage semantics for a safe `FillIfEmpty` block path: canonical Empty may receive the exact whole-cell result; any non-empty Uniform block is an exact no-op because every cell is solid; Uniform stores material only with implicit zero surface/boundary semantics, so no broader overwrite is safe; Mixed must stay per-cell.
- [ ] Add a focused real-storage regression before production implementation covering canonical-Empty interior atomic `FillIfEmpty`, Uniform-solid interior no-op, Mixed preservation, and a boundary block that remains on the per-cell path with authored halo semantics.
- [ ] Implement only the proven full-block `FillIfEmpty + Frustum` cases and extend the deep-interior halo skip to `FillIfEmpty` under the same eight-corner >32 Q4 proof.
- [ ] Re-check the `FillIfEmpty` optimization blast radius/cost; Mixed/partial/boundary blocks, other shapes/modes, primitive order, surface/boundary semantics, and exact logical write accounting must remain unchanged.
- [ ] Re-measure the full 9-region bake under the 240 s subprocess contract.
- [ ] Prove all authored landing floor/headroom columns survive without castle-region suppression on the source-matched bake.

## Exact-SHA bake / built-player gate
- [x] Use only existing `ci-test/fixes/agent-4` for targeted CI; no extra transport and no feature-branch `.github/test-request.json` edit.
- [x] Submit exact-parent request `5cc2ef4e03ce...` for source `bf376936ae66...` only after latest-master ancestry was confirmed; run `33288586966` completed red and was not replaced while queued/running.
- [x] Submit exact-parent request `cda51cb0a62a...` for source `408a0fdf8c2b...` only after the prior request was completed red and latest-master ancestry was confirmed; run `33289023179` completed red and was not replaced while queued/running.
- [ ] Produce/commit the accepted source-matched generated startup payload + manifest before final closure, if repository workflow permits producing it without violating the CI transport contract.
- [ ] After the next revised source and latest-master integration, issue a new exact-parent final request using only `ci-test/fixes/agent-4`.
- [ ] Run the exact focused acceptance filter green on the final feature SHA.
- [ ] Traverse the complete route via production `AutoWalk -> CharacterMotor.Step` with grounded Y proof.
- [ ] Save and human-review approach/base/middle/upper/summit/dialogue captures, including `Hello, I'm Mr. Dragon.`
- [ ] Record measured bake/runtime evidence and accepted payload provenance.

## Closure gate
- [ ] After all green gates, complete pending metadata and move only this assignment `open -> pending`.
- [ ] Move only this assignment `pending -> closed`, set `status=fixed` and `resolvedUtc`.
- [ ] Merge latest `origin/master`, preserving master semantic repairs/material retention and agent-4 provenance/capture suppression, then push the exact feature head to `origin/master` non-force; fetch/merge/retry if master advanced.
