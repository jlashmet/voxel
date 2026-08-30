# Tasks — Kentridge macro-world physical realization

## Implemented feature foundation
- [x] Inspect assignment; `captures: []`, so feature note is the repro/acceptance contract.
- [x] Preserve the source-backed `TopDownWorldLayout` as topology authority and one-shot selection into production Kentridge composition.
- [x] Add reusable regional intent for water, mountain/ridge, valley/pass, plains/meadow, woodland, generic regions, extents/elevation, deterministic variation, relationships, and route solutions.
- [x] Add deterministic terrain-aware hard-route solving with slope limits and explicit crossing/pass/around semantics; reject unsolved blocked hard routes.
- [x] Physically realize every macro settlement; preserve richer Kentridge/Hightown and provide four-building generic blockouts plus streets for Moordell, Rossdam, Fairy Village, and Orc Village.
- [x] Emit continuous generated route surfaces and validate representative real CharacterMotor traversal.
- [x] Add bounded carved/non-solid Rossdam basin and production ridge/pass geography.
- [x] Preserve ecology ownership and streaming/LOD semantics; no eager remote GameObjects or streaming-radius increase.

## Regressions / production remediation
- [x] Add broad production acceptance for determinism, all-settlement reachability, continuous roads, strict <=6 voxel/3 m rise, constraints, explicit route solutions, and blocked-route failure.
- [x] Preserve standalone generic WaterBody behavior but give Kentridge production Rossdam water a single carved-basin owner.
- [x] Ground every generic building with a bounded 5x5 terrain-relief sample; foundation absorbs local relief and timber/roof remain above sampled high terrain.
- [x] Expand production-storage acceptance to all 16 generic buildings and assert timber/roof material reaches final region storage.
- [x] Add all-building WaterBody/separative-region footprint regression.
- [x] Run `33279138597` for exact request `a3f2d6d6652abac2dcf9061f9dda51b9e6ecb52b`; nested production acceptance passed (20 routes, 16 buildings, 5 constrained routes, max rise 2), then footprint regression found `orc-village building 3` overlapping `southern-ridge`.
- [x] Reject Rossdam/infrastructure hypotheses for that failure; exact NUnit evidence proves an Orc/ridge semantic conflict.
- [x] Bound the modern Southern Ridge blockout (`halfExtentZDm 270 -> 120`) so fixed-seed Orc plot 3 clears it while the direct Orc/Logan corridors still intersect the ridge and retain their authored `GoAround` / designated-pass solutions.
- [x] Exact-SHA request `c1a21b76cdc548436a32bd0866f26a2448a67286`, run `33283034449`, is green for source `0bbc9150f36281c0f951d9c75a60b318842fba46`; final persisted macro-world storage contains non-air payload/ownership across all expected regions and the authored traversable route set.
- [x] Final exact-SHA storage evidence proves the Southern Ridge remediation no longer blocks production storage generation and all-building settlement material probes complete successfully.

## Built-player / visual evidence
- [x] Gate remote captures on four consecutive complete current-camera near-surface passes; preserve normal collision/streaming and `Time.timeScale=1` CharacterMotor motion.
- [x] Prior evidence proved camera-target residency is within the normal 3-region (~153.6 m) radius; do not change residency to make evidence pass.
- [x] Full-resolution run `33279138597` confirms the clean Rossdam basin/road response is more readable.
- [x] Full-resolution run `33279138597` rejects a camera-only explanation for missing towns: Fairy/Orc survey cameras are close enough that present building-scale shells would be obvious, yet none render.
- [x] Inspect final full-resolution artifact `9723674189` from run `33283034449`: Fairy Village and Orc Village show roads/terrain but no readable settlement shells; Moordell shows fewer than four obvious shells; Rossdam is dominated by lake/terrain with no four-building settlement read.
- [x] Because final storage is green while built-player settlements remain absent, classify the remaining defect as production streaming/render-path visibility rather than camera framing or storage generation.
- [x] Trace persisted `SettlementStructure` payload through the production path and identify the first failing boundary: `ShowcaseWorld.FinishRegion()` publishes terrain before separately queued feature realization, allowing renderer-only coverage to report stable before `CompleteFeatureBuild()` publishes the settlement commit/invalidation.
- [x] Add a reusable current-demand generated-content readiness contract: terrain publication alone is not settled while a demanded region can still receive feature publication/invalidation.
- [x] Add a production behavioral regression at that two-stage boundary proving readiness remains false after terrain publication, becomes true only after feature draining, real feature rasterization increases, and the expected settlement timber shell reaches the authoritative world storage consumed by rendering.
- [x] Gate built evidence on current-demand content readiness followed by four consecutive complete near-surface renderer publications; no fixed delay.
- [x] Fix the reusable production path without scene-local hardcoding, camera masking, eager remote GameObjects, direct scene voxel writes, or streaming-radius expansion.
- [ ] Exact built `KentridgePlayableSlice` reaches a usable rendered state without startup/runtime exceptions.
- [ ] Full-resolution exact-scene evidence proves the committed settlement shells become renderer/mesh-visible and shows four readable blockouts at Moordell, Rossdam, Fairy Village, and Orc Village.
- [ ] Full-resolution evidence shows continuous roads/network without large holes, substantial lake and ridge/pass response, and representative CharacterMotor traversal.

## Blast radius / cost
- [x] Static scope: no other SceneIssue, no feature-branch `.github/test-request.json`, no custom workflow/CI transport, no CharacterMotor/streaming-radius change.
- [x] Terrain-relief sampling cost is bounded: 25 samples x 16 generic buildings = 400 deterministic catalogue-build queries; definition/placement counts are unchanged.
- [x] Southern Ridge remediation changes one Kentridge region extent only; graph nodes/routes, settlement coordinates, feature counts, and runtime systems are unchanged.
- [x] Refresh `fixes/agent-6` from current `origin/master` after the green storage gate; latest merge `2c6c501b4823fbb3b5a33ef30ac0e3ba119b1ab0` incorporates master `d4b31a700bae19b08ef765e874e26026620bde0e` with path-disjoint changes and no agent-6 conflict.
- [x] Keep readiness evaluation scoped to already-maintained current-demand generation state: at normal radius 3 it visits 29 horizontal columns and <=145 region-state checks (four capped surface layers plus at most one distinct camera layer), with no voxel/mesh scan, generation, allocation, duplicate geometry, or extra residency.
- [ ] Measure final remediation cost and record route solve/tile/building/feature counts plus built-player CPU/GPU/frame/memory/streaming telemetry against existing budgets.
- [x] Re-check current feature diff against current master before final CI: branch is ahead only, and the diff remains limited to the assigned macro-world implementation/tests/evidence plus the reusable Showcase readiness boundary and assignment metadata.

## Acceptance / closure
- [ ] (1) Source-backed macro graph remains authoritative through shared WorldBuilder APIs.
- [ ] (2) Every settlement has readable physical presence, including >=4 grounded generic blockouts where no richer generator owns it.
- [ ] (3) Every settlement is physically reachable from Kentridge over contiguous generated hard-route surfaces.
- [ ] (4) Roads are terrain-aware and blocked geography requires explicit semantic solutions.
- [ ] (5) Reusable geographic authoring/query covers required region kinds, extents/elevation, relationships, deterministic variation, terrain output, route/placement constraints.
- [ ] (6) Built world visibly contains a substantial lake + ridge and a geography-constrained hard route.
- [ ] (7) Regional terrain visibly reads as differentiated countryside rather than a flat debug plane.
- [ ] (8) No second scene-local graph/direct voxel-writing/static destination hierarchy.
- [ ] (9) Focused production regressions cover determinism, reachability, roads, settlements, constraints, blocked-route failure, and two-stage generated-content readiness.
- [ ] (10) Exact built-player evidence covers settlements, roads, geography, constrained route, and CharacterMotor traversal.
- [ ] (11) Blast radius and world-build/route/CPU/GPU/memory/streaming cost measured against budgets.
- [ ] Final exact-SHA focused CI and built-player evidence are closure-quality green.
- [ ] Every checkbox above is complete before `open -> pending`.
- [ ] Complete pending metadata (`status=pending`, `resolutionSummary`, `regressionTest`, `fixCommit`) only after green exact gates.
- [ ] Move only this assignment `open -> pending`, then after closure gate `pending -> closed`, set `status=fixed` and `resolvedUtc`.
- [ ] Merge current `origin/master` into `fixes/agent-6`, resolve no unrelated conflicts, and non-force push the exact feature head to `origin/master`; retry merge if master advances.
