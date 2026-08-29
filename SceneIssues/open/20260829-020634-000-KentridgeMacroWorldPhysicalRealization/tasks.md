# Tasks — Kentridge macro-world physical realization

## Required feature work
- [x] Inspect the assigned feature and confirm `captures: []` / zero marked regions.
- [x] Audit the existing top-down macro graph, deterministic planner, one-shot build selection, and current neutral-marker/Manhattan-road voxel realization.
- [x] Add reusable WorldBuilder macro-region intent covering water body, mountain/ridge barrier, valley/pass, plains/meadow, forest/woodland, and generic region kinds.
- [x] Add deterministic region extents/elevation intent plus semantic relationships/constraints usable by placement/routing (`between`, `adjacent`, `contains`, `separates`, pass/crossing/through/around or equivalent).
- [x] Add deterministic terrain-aware hard-route solving that respects settlement envelopes, water/barriers, slope limits, explicit crossings/passes, and rejects an impossible blocked hard route without a solution.
- [x] Realize every macro settlement physically; preserve richer existing Kentridge/Hightown generation and add reusable grounded >=4-building blockouts, internal circulation, and road arrival/exit for unrealized settlements.
- [x] Emit continuous walkable road/trail surfaces for every verified hard route all the way into settlement envelopes.
- [x] Author the first Kentridge macro geography pass through shared definitions: substantial lake, substantial mountain/ridge, differentiated countryside, and routes affected by geography.
- [x] Keep ecology species/density separate while exposing/querying regional kinds/exclusions.
- [x] Keep generation/streaming deterministic and compatible with voxel streaming/LOD; no eager remote GameObject world.
- [x] Discovered: replace the prototype water-only repaint with a bounded streamed carved basin using the existing non-solid water material.
- [x] Discovered: assert strict production road rise <=6 voxels per 30 dm step.
- [x] Discovered: prove `KentridgeDefinition.Build` selects the semantic macro layout before `KentridgeCombinedVoxelCatalogue` consumes it one-shot.
- [x] Discovered: assert the production combined catalogue contains macro roads, all four remote settlement blockouts, ridge geography, and carved water.
- [x] Discovered: built-player evidence traverses a real Moordell macro-road segment with production CharacterMotor/streaming.
- [x] Preserve current-master opening-story/ecology state; stale `StorySpecs.cs` overlay was removed byte-for-byte from current master.

## Product failures repaired by exact-SHA CI
- [x] `33230924543`: evidence driver missing `Game.WorldBuilder.Api`; fixed at `339ca94f593653e84a02fe2d19712971bfd99e20`.
- [x] `33231300309`: acceptance test missing same import; fixed at `e40fb7220af56e096020e105959202eac2b2d70d`.
- [x] `33232755172`: Bandit route intersected Rossdam lake without semantic solution; authored dry-shore `GoAround` + corridor regression.
- [x] `33255557296`: Orc route grazed southern ridge; authored ridge-shoulder `GoAround` + travel-margin regression.
- [x] `33258816868`: stale agent-6 `StorySpecs.cs` removed landed opening-story APIs; restored current-master bytes at `eb6b77cc13bf5b1850a81df9a73a6250f7d2ba5b` and verified the file disappeared from the feature diff.

## Evidence defects and experiments
- [x] `33259572439` / artifact `9716890862`: focused + built player green; opening left time only for macro-road/Moordell. Same artifact provides a valid no-prewarm Moordell building reference.
- [x] `33260139560` / artifact `9717050641`: all named files emitted but remote screenshots were premature (`coverage=False`); recorded `experiment-005-remote-evidence-convergence.md`.
- [x] Add validation-only opening acceleration, normal-time restoration before movement/teardown, and published-near-coverage screenshot gating.
- [x] `33260866388` / artifact `9717246958`: coverage-gated but validation remote prewarm caused settlement feature presentation to disappear and timed out before lake/ridge/overview; recorded `experiment-006-evidence-prewarm-lifecycle.md`.
- [x] Remove only validation-driver remote `GenerateRegionBlocking` prewarm; production streaming/worldgen unchanged.
- [x] Reorder targets Rossdam->lake and Orc->ridge to reuse resident geography.
- [x] `33261299347` / artifact `9717362552`: exact focused + built player green; no-prewarm restores visible Moordell building and captures Rossdam lake, but Rossdam/Fairy/Orc ground-level center-facing views are terrain-occluded and ridge/overview still exceed 60s; recorded `experiment-007-evidence-camera-occlusion.md`.
- [x] `33261744161` / artifact `9717484869`: exact focused + built player green with bounded production metrics and normal-time CharacterMotor motion, but the fixed replay finishes only macro-road/Moordell/Rossdam/Rossdam Lake/Fairy Village; Orc/ridge/network are absent and generic settlement focus still emphasizes circulation centers. Recorded `experiment-008-visual-evidence-scheduling-and-focus.md`.

## Geometry-derived evidence-camera/timing work discovered by 33261299347
- [x] Derive generic-settlement evidence from generated geometry: elevated survey poses use the generated settlement center/four ±190 dm plots and choose the highest of four diagonal terrain viewpoints rather than fixed ground-level offsets.
- [x] Derive Rossdam-lake evidence from the resolved geography: retain the constrained route point and raise the camera to 72 m so the substantial ~104x54 m water body and detour can share the frame.
- [x] Preserve explicit elevated southern ridge/pass and physical-network survey poses with published-near-coverage gating.
- [x] Increase only validation-profile opening acceleration to 12x and trim evidence overhead while preserving normal gameplay/story semantics and CharacterMotor movement at `Time.timeScale=1`.

## Validation-driver work discovered by 33261744161
- [x] Focus each generic-settlement capture on representative actual generated building geometry instead of only the empty circulation/resident center.
- [x] Keep published-near-surface coverage as the renderer-readiness gate and replace redundant 1.25-second per-target dwell with only a 0.35-second camera/stream invalidation floor.
- [x] Compress validation-only movement timing modestly while retaining real production `CharacterMotor` motion, collision/streaming, and `Time.timeScale=1` evidence.
- [x] Reuse the southern-ridge resident/streaming location for the final macro-network overview so the validation harness does not pay an unnecessary remote convergence cycle.
- [x] Self-review the evidence-driver-only diff: implementation commit `752f8c17d414678c587bdfbfeb9cd636d338224f` changes 25 lines only; planner, catalogue, streamer, story, gameplay timing, and macro physical semantics are unchanged.
- [ ] Run one fresh exact-SHA request on the existing `ci-test/fixes/agent-6`; require all seven target PNGs plus macro-road PNG, coverage-ready markers, visible four-town blockouts, readable lake/ridge/network, normal-time traversal, and clean runtime before closure.

## Behavioral regression
- [x] Final targeted PlayMode acceptance exists: `VoxelEngine.Tests.PlayMode.KentridgeMacroWorldPhysicalProductionAcceptanceTests.PhysicalMacroWorldHasWalkableRoutesAndADeepStreamedWaterBody`.
- [ ] Final exact-SHA execution verifies fixed-seed macro-to-physical output is deterministic.
- [ ] Final exact-SHA execution verifies every settlement has a physical settlement plan and >=4 non-overlapping grounded blockout buildings when no richer generator owns it.
- [ ] Final exact-SHA execution verifies every settlement is reachable from Kentridge over contiguous generated hard-route surfaces.
- [ ] Final exact-SHA execution verifies strict production slope/obstacle constraints and settlement road arrival/exit connectivity.
- [ ] Final exact-SHA execution verifies lake/ridge constraints alter hard routes via explicit semantic solutions, including Bandit dry-shore and Orc ridge-shoulder route-arounds.
- [ ] Final exact-SHA execution verifies impossible blocked route rejection without explicit crossing/pass/around solution.
- [ ] Final exact-SHA execution verifies richer Kentridge/Hightown output is preserved.
- [ ] Final exact-SHA execution verifies the selected macro graph survives the real combined production catalogue, including carved water.

## Visual / runtime evidence
- [ ] Exact built `KentridgePlayableSlice` reaches a usable rendered state with no startup/runtime exceptions.
- [ ] Durable player-height evidence shows a continuous generated road and real CharacterMotor traversal with collision/streaming active.
- [ ] Durable close/survey evidence visibly shows Moordell, Rossdam, Fairy Village, and Orc Village physical blockouts.
- [ ] Durable elevated/survey evidence shows the physical road network corresponds to the semantic macro graph.
- [ ] Durable evidence shows a substantial generated lake and mountain/ridge plus a route responding to one constraint.
- [ ] Inspect Rossdam basin for readable shoreline/depth and no collision/rendering anomaly from non-solid water.

## Blast radius / cost
- [ ] Quantify macro plan counts, route-solving work, feature/placement/primitive cost, carved-water cost, and one-shot build cost from the final exact green run.
- [ ] Inspect built-player CPU/GPU/frame/memory/streaming/far-field telemetry against existing device budgets; do not weaken budgets and distinguish startup/teleport validation spikes from steady-state gameplay.
- [x] Static blast-radius review: macro realization is opt-in through existing one-shot `TopDownWorldLayoutSelection`; ordinary callers retain prior cost, no second graph/static destination hierarchy.
- [x] Scope audit: only agent-6 Kentridge/WorldBuilder/tests/assignment files differ from current master; no other SceneIssue and no `.github/test-request.json` on `fixes/agent-6`.
- [ ] Re-check final branch diff after final CI/metadata and before promotion.

## Acceptance audit / closure
- [ ] Acceptance (1): source-backed macro graph remains authoritative through reusable WorldBuilder/shared APIs.
- [ ] Acceptance (2): every settlement has physical presence with required blockout quality or richer generation.
- [ ] Acceptance (3): every settlement is physically reachable from Kentridge over contiguous generated hard-route surfaces.
- [ ] Acceptance (4): roads are terrain-aware and blocked geography requires semantic solutions.
- [ ] Acceptance (5): reusable geographic-region authoring/query covers required kinds, extent/elevation, relationships, deterministic variation, terrain output, and constraints.
- [ ] Acceptance (6): built world visibly contains substantial lake + ridge and a geography-constrained hard route.
- [ ] Acceptance (7): regional terrain visibly reads as differentiated countryside rather than a flat debug plane.
- [ ] Acceptance (8): no scene-local second graph/direct voxel-writing/static destination hierarchy.
- [ ] Acceptance (9): focused production-path regressions cover determinism, reachability, roads, settlements, constraints, and blocked-route failure.
- [ ] Acceptance (10): exact built-player evidence covers settlements, roads, geography, constrained route, and CharacterMotor traversal.
- [ ] Acceptance (11): blast radius and world-build/route/CPU/GPU/memory/streaming cost measured against budgets.
- [ ] Every checkbox above is complete before `open -> pending`.
- [ ] Final exact-SHA focused CI and built-player evidence are closure-quality green.
- [ ] Complete pending metadata and move only this feature `open -> pending`.
- [ ] Move only this feature `pending -> closed`, set `status=fixed` and `resolvedUtc`.
- [ ] Merge current `origin/master` into `fixes/agent-6`, verify exact head, and non-force push that head to `origin/master`; retry if master advances.
