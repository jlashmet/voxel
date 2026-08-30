# Tasks

## Completed implementation / investigation
- [x] Trace `VoxelShowcase` cavern composition through shared WorldBuilder/game/engine authoring and identify terrain, route, cavern, ruin/statue, runtime-region, and local-light owners.
- [x] Implement the reusable destination cavern/ancient ruin composition, deep host volume, reachable placement, large cavern, formations, aged ruin, exactly two grounded statues, supported local lights, runtime bake restoration, and affected-region publication.
- [x] Add reusable natural mouth, long descending traversal, deterministic doglegs, semantic traversal waypoints, configurable traversal profile, portability coverage, and normal CharacterMotor production traversal regression.
- [x] Add reusable irregular cavern/ruin/statue visual finish, bounded visual-finish writes, localized lighting, and final circulation reassertion.
- [x] Add the 95-second standalone-player evidence driver that uses normal `AutoWalk -> ShowcaseWorld.MovePlayer -> CharacterMotor -> streaming`, requires ruin-route completion, and emits production frames.
- [x] Keep the generic cave engine, renderer, camera, material catalogue, public `IStructureAuthoringSession`, and shared CI workflow unchanged.
- [x] Diagnose and fix prior compile, brick-pool, route-grade, waypoint, destination-shell, circulation, ordering, and capture-mode failures without weakening movement/device/write/light budgets.
- [x] Replace the old destination box safety carves with reusable overlapping rounded circulation nodes and add deterministic rounded-plan coverage.
- [x] Replace fixed 13-voxel full-route sampling with deterministic variable-spacing primary/side/upper naturalization nodes and add deterministic spacing/side/ceiling-plan coverage.
- [x] Exact run `33284693031` on source `492ea820...`: focused PlayMode green; standalone player reaches waypoint 38/38 with zero harness assertions; `34,798,060` total writes, `4,416,056` naturalization writes/215 nodes, `3,579,396` finish writes, 20 preloaded regions, 6 route / 8 total lights.
- [x] Directly inspect all seven frames from run `33284693031` and reject closure: vertical rib/scallop walls and planar ceiling bands remain; final destination still reads as a rectangular masonry throat and does not clearly present the huge cavern, ruin, and both statues.
- [x] Falsify cadence-only, route-completion, removed-box, capture-window, and material-ID-swap explanations using the exact run plus source.
- [x] Trace the remaining silhouette to primitive topology: generic cave core is a full-height rectangular cross-section; full-route naturalization, doglegs, and destination circulation still add vertical cylinders with vertical walls/flat tops, so their union cannot reliably hide the architectural core.

## Current rounded-vault repair
- [x] Add a reusable deterministic stacked-disc rounded-vault profile in the opt-in underground-cavern runtime layer; no public authoring API or generic cave-core change.
- [x] Guarantee the vault fully masks the rectangular gameplay core between maximum-spaced nodes: minimum wall radius/height preserves route width, wall roughness, ceiling roughness, and CharacterMotor clearance.
- [x] Give the vault multiple wall-radius slices and a tapered crown so the visible passage has sloped/rounded walls and a non-planar roof rather than cylinder ribs/bands.
- [x] Reuse the same rounded-vault brush for full-route naturalization, dogleg carving, and destination circulation so no late cavern passage pass reintroduces the failed cylinder silhouette.
- [x] Preserve existing floor support, dogleg route semantics, destination reachability, localized-light placement, and deterministic world truth at the production authoring boundary.
- [x] Add/strengthen production-computation regressions for deterministic vault radii, multiple wall-radius variants, mathematical adjacent-node core coverage, tapered crown, bounded slice count, and repeated resolution.
- [x] Keep normal production WorldBuilder generation + CharacterMotor traversal/determinism coverage and the existing 15,000,000 naturalization-write / 55,000,000 total-write / eight-light ceilings unchanged.
- [x] Preserve the rounded slice geometry while compiling radial columns into contiguous `FillColumnBulk` spans so the repair stays off the 12,000,000 slow-write path used by per-voxel `Disc` emission.
- [x] Confirm the rounded-vault production/test assemblies compile in the existing PR runner; its subsequent 59 EditMode failures are broad pre-existing/out-of-scope full-run failures and do not involve cavern compilation.

## Final validation / closure
- [ ] Review final diff/blast radius and quantify voxel/chunk work, vertices/indices/draws, light/shadow count, runtime FPS/memory evidence, and supported-device budget impact against the recorded baseline.
- [x] Merge current `origin/master` into repaired `fixes/agent-3` as real two-parent merge `b5d1d9e8...`; master parent `d4b31a7...` only added an unrelated SceneIssue and was preserved unchanged.
- [ ] Build exactly one canonical final request on `ci-test/fixes/agent-3` from the exact feature SHA, with the cavern PlayMode filter and empty `scene_issue` / `replay_seconds`; do not edit feature `.github/test-request.json`, create extra transports, or replace queued/running CI.
- [ ] Obtain green exact-SHA focused targeted CI.
- [ ] Obtain green exact-SHA standalone `VoxelShowcase` traversal/capture with ruin-route completion and no startup/runtime exceptions or harness assertions.
- [ ] Directly inspect every useful built-player frame for the full sequence: daylight natural mouth -> prolonged varied organic descent -> huge dark irregular cavern/formations -> aged reachable ruin -> exactly two grounded readable flanking humanoid statues.
- [ ] Validate localized supported torch lighting, meaningful darkness, circulation/clearance, believable construction/support, no unintended intersections, and no placeholder-quality final art.
- [ ] Record durable final verification evidence beside the assignment and complete every acceptance criterion.
- [ ] Move only this assignment `open` -> `pending` after both exact-SHA gates and direct rendered review are green; fill `status=pending`, `resolutionSummary`, `regressionTest`, and `fixCommit`.
- [ ] After all gates/checks are complete, set `status=fixed` and `resolvedUtc`, move only this assignment `pending` -> `closed`, merge current master again if needed, and push the exact validated feature head to `origin/master` non-force; retry only by fetch+merge if master advanced.
