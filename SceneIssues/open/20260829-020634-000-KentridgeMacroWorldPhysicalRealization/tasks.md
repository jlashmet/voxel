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
- [x] `33259572439` / artifact `9716890862`: focused + built player green; opening left time only for macro-road/Moordell.
- [x] `33260139560` / artifact `9717050641`: all named files emitted but remote screenshots were premature (`coverage=False`); recorded experiment 005.
- [x] `33260866388` / artifact `9717246958`: validation remote prewarm harmed settlement presentation; recorded experiment 006 and removed validation-only prewarm.
- [x] `33261299347` / artifact `9717362552`: exact focused + built player green but town views were terrain-occluded and ridge/overview exceeded 60s; recorded experiment 007.
- [x] `33261744161` / artifact `9717484869`: exact focused + built player green but the sequence still omitted Orc/ridge/network; recorded experiment 008.
- [x] `33263409994` / artifact `9717947090`: focused + built player green and all eight PNGs emitted, but full-resolution inspection rejects closure: Moordell clipped; Rossdam/Fairy/Orc blockouts unreadable; lake only a small sliver with a large translucent plane; overview/road contain large unstreamed holes. `coverage=True` is therefore not sufficient elevated-frustum proof. Recorded experiment 009.
- [x] `33265086481` / artifact `9718439671`: exact focused PlayMode + built player green; every evidence target captured after four consecutive complete published current-camera passes. Full-resolution inspection still rejects closure: Moordell exposes only one recess, Rossdam has no readable town and is dominated by a flat blue/cut surface, Fairy/Orc show roads but no four-building blockouts, and lake/ridge views do not clearly prove a substantial basin/barrier. Recorded experiment 010.

## Frustum-ready evidence work discovered by 33263409994
- [x] Inspect existing rendering/streaming APIs. `RenderingComposition.HasCompletePublishedNearSurfaceCoverage()` is explicitly the current surface pass's `MissingVisibleSolidChunks == 0`; there is no arbitrary-position/frustum coverage query exposed through Composition.
- [x] Reject validation sample-point teleports/prewarm: querying another footprint requires moving the resident/camera and would recreate the presentation regression from experiment 006. Selected approach is bounded resident-sized evidence views plus multiple consecutive complete current-camera surface passes before capture.
- [x] Tighten generic-settlement framing from production blockout bounds: compute the four-building physical envelope, target its centre, keep diagonal camera offsets bounded to 28-36 m from that envelope, and reduce survey height to 22 m.
- [x] Derive Rossdam lake framing from the physical lake extent and the closest constrained-route point; place the validation camera just outside that route point at 24 m instead of using an unrelated route midpoint at 72 m.
- [x] Tighten southern ridge/pass evidence to a 19 m x 17 m offset at 32 m height around the actual route tile inside the ridge.
- [x] Replace the too-wide Kentridge-facing macro overview with a bounded 22 m x 19 m / 40 m-high survey of the source-backed South Fighting Area branch where Logan and Orc routes diverge.
- [x] Gate the macro-road screenshot on four consecutive complete published current-camera surface passes after the real CharacterMotor traversal instead of capturing immediately at the movement endpoint.
- [x] Gate every remote target on four consecutive complete current-camera passes after its minimum settle time; reset stability on each target transition.
- [x] Preserve validation-only timing, real `CharacterMotor` motion at `Time.timeScale=1`, collision/streaming, and all production planner/catalogue/streamer/story/gameplay semantics.
- [x] Self-review implementation commit `a0fc52f67e1a8e495de0012ca4715b7250f9c3a5`: only `KentridgeMacroWorldEvidenceDriver.cs` changed. Full branch diff remains limited to agent-6 Kentridge/WorldBuilder/tests/assignment files; `.github/test-request.json` is absent. Four-frame stability adds only bounded validation latency after convergence and closer cameras reduce view/streaming footprint.

## Physical-visibility discriminator discovered by 33265086481
- [x] Trace generic settlement blockouts from `TopDownWorldSettlementPlan.Buildings` through the physical catalogue into `KentridgeCombinedVoxelCatalogue`. Source trace confirms grounded foundations plus filled timber wall and roof/gable primitives are authored for each generic building and the physical structure pass is composed after macro roads.
- [x] Check production composition/precedence for obvious terrain/road swallowing. The combined catalogue contains the authored generic building definitions and does not intentionally replace them with marker-only records; no production geometry rewrite is justified before final occupancy is sampled.
- [x] Correlate evidence targeting with streaming. `KentridgePlayableSlice.UpdateDynamicResidency` follows `CharacterMotor.EyePosition`, while `KentridgeMacroWorldEvidenceDriver.PinToTarget` puts that motor at `EvidenceTarget.CameraPositionMetres`; `IsTargetReady` also samples around that camera position. Therefore target readiness is not proof that the photographed settlement/lake/ridge focus is resident/published.
- [ ] Add a production-path behavioral regression that resolves a real authored generic settlement building through `KentridgeCombinedVoxelCatalogue` and asserts above-ground wall/roof occupancy in final `VoxelData`, not only planner/definition presence.
- [ ] Decouple evidence-time residency from survey camera position: keep motor/streaming centered on the actual target feature and drive the camera transform independently at the existing offset.
- [ ] Change target storage readiness to probe the actual target feature while retaining current published-surface/fallback rendering gates.
- [ ] Re-run the existing evidence targets unchanged first after the occupancy/residency fix; adjust framing only if confirmed resident production geometry remains visually ambiguous.
- [ ] Re-run exact-SHA focused + built-player validation after the proven minimal fix and reject closure unless full-resolution settlement/lake/ridge evidence is readable.

## Behavioral regression
- [x] Final targeted PlayMode acceptance exists: `VoxelEngine.Tests.PlayMode.KentridgeMacroWorldPhysicalProductionAcceptanceTests.PhysicalMacroWorldHasWalkableRoutesAndADeepStreamedWaterBody`.
- [ ] Final exact-SHA execution verifies fixed-seed macro-to-physical output is deterministic.
- [ ] Final exact-SHA execution verifies every settlement has a physical settlement plan and >=4 non-overlapping grounded blockout buildings when no richer generator owns it.
- [ ] Final exact-SHA execution verifies every settlement is reachable from Kentridge over contiguous generated hard-route surfaces.
- [ ] Final exact-SHA execution verifies strict production slope/obstacle constraints and settlement road arrival/exit connectivity.
- [ ] Final exact-SHA execution verifies lake/ridge constraints alter hard routes via explicit semantic solutions, including Bandit dry-shore and Orc ridge-shoulder route-arounds.
- [ ] Final exact-SHA execution verifies impossible blocked route rejection without explicit crossing/pass/around solution.
- [ ] Final exact-SHA execution verifies richer Kentridge/Hightown output is preserved.
- [ ] Final exact-SHA execution verifies the selected macro graph survives the real combined production catalogue, including carved water and above-ground generic-building occupancy.

## Visual / runtime evidence
- [ ] Exact built `KentridgePlayableSlice` reaches a usable rendered state with no startup/runtime exceptions.
- [ ] Durable player-height evidence shows a continuous generated road and real CharacterMotor traversal with collision/streaming active.
- [ ] Durable close/survey evidence visibly shows Moordell, Rossdam, Fairy Village, and Orc Village physical blockouts.
- [ ] Durable elevated/survey evidence shows the physical road network corresponds to the semantic macro graph without large unstreamed holes.
- [ ] Durable evidence shows a substantial generated lake and mountain/ridge plus a route responding to one constraint.
- [ ] Inspect Rossdam basin for readable shoreline/depth and no collision/rendering anomaly from non-solid water.

## Blast radius / cost
- [ ] Quantify macro plan counts, route-solving work, feature/placement/primitive cost, carved-water cost, and one-shot build cost from the final exact green run.
- [ ] Inspect built-player CPU/GPU/frame/memory/streaming/far-field telemetry against existing device budgets; do not weaken budgets and distinguish startup/teleport validation spikes from steady-state gameplay.
- [x] Static blast-radius review: macro realization is opt-in through existing one-shot `TopDownWorldLayoutSelection`; ordinary callers retain prior cost, no second graph/static destination hierarchy.
- [x] Scope audit: only agent-6 Kentridge/WorldBuilder/tests/assignment files differ from current master; no other SceneIssue and no `.github/test-request.json` on `fixes/agent-6`.
- [x] Planned residency fix is validation-only and reuses the existing radius-1 `ResidencyManager`; no eager remote build, streaming-radius increase, production catalogue change, or extra CI transport.
- [x] Planned occupancy regression is bounded to production-catalogue sampling of a small authored building footprint inside the existing final targeted PlayMode acceptance.
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
