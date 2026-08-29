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
- [ ] Final exact-SHA run proves Orc exclusion passes and both Southern Ridge route constraints remain geography-constrained.
- [ ] Final exact-SHA run proves all 16 generic buildings pass placement/program grounding and final timber/roof storage probes.

## Built-player / visual evidence
- [x] Gate remote captures on four consecutive complete current-camera near-surface passes; preserve normal collision/streaming and `Time.timeScale=1` CharacterMotor motion.
- [x] Prior evidence proved camera-target residency is within the normal 3-region (~153.6 m) radius; do not change residency to make evidence pass.
- [x] Full-resolution run `33279138597` confirms the clean Rossdam basin/road response is more readable.
- [x] Full-resolution run `33279138597` rejects a camera-only explanation for missing towns: Fairy/Orc survey cameras are close enough that present building-scale shells would be obvious, yet none render.
- [ ] If all-16 final storage is green but built-player settlements remain absent, investigate the production streaming/render path; do not mask it with camera changes.
- [ ] Exact built `KentridgePlayableSlice` reaches a usable rendered state without startup/runtime exceptions.
- [ ] Full-resolution evidence visibly shows four readable blockouts at Moordell, Rossdam, Fairy Village, and Orc Village.
- [ ] Full-resolution evidence shows continuous roads/network without large holes, substantial lake and ridge/pass response, and representative CharacterMotor traversal.

## Blast radius / cost
- [x] Static scope: no other SceneIssue, no feature-branch `.github/test-request.json`, no custom workflow/CI transport, no CharacterMotor/renderer/streaming-radius change.
- [x] Terrain-relief sampling cost is bounded: 25 samples x 16 generic buildings = 400 deterministic catalogue-build queries; definition/placement counts are unchanged.
- [x] Southern Ridge remediation changes one Kentridge region extent only; graph nodes/routes, settlement coordinates, feature counts, and runtime systems are unchanged.
- [ ] Record final route solve/tile/building/feature counts and built-player CPU/GPU/frame/memory/streaming telemetry against existing budgets.
- [ ] Re-check final feature diff against current master before promotion.

## Acceptance / closure
- [ ] (1) Source-backed macro graph remains authoritative through shared WorldBuilder APIs.
- [ ] (2) Every settlement has readable physical presence, including >=4 grounded generic blockouts where no richer generator owns it.
- [ ] (3) Every settlement is physically reachable from Kentridge over contiguous generated hard-route surfaces.
- [ ] (4) Roads are terrain-aware and blocked geography requires explicit semantic solutions.
- [ ] (5) Reusable geographic authoring/query covers required region kinds, extents/elevation, relationships, deterministic variation, terrain output, route/placement constraints.
- [ ] (6) Built world visibly contains a substantial lake + ridge and a geography-constrained hard route.
- [ ] (7) Regional terrain visibly reads as differentiated countryside rather than a flat debug plane.
- [ ] (8) No second scene-local graph/direct voxel-writing/static destination hierarchy.
- [ ] (9) Focused production regressions cover determinism, reachability, roads, settlements, constraints, and blocked-route failure.
- [ ] (10) Exact built-player evidence covers settlements, roads, geography, constrained route, and CharacterMotor traversal.
- [ ] (11) Blast radius and world-build/route/CPU/GPU/memory/streaming cost measured against budgets.
- [ ] Final exact-SHA focused CI and built-player evidence are closure-quality green.
- [ ] Every checkbox above is complete before `open -> pending`.
- [ ] Complete pending metadata (`status=pending`, `resolutionSummary`, `regressionTest`, `fixCommit`) only after green exact gates.
- [ ] Move only this assignment `open -> pending`, then after closure gate `pending -> closed`, set `status=fixed` and `resolvedUtc`.
- [ ] Merge current `origin/master` into `fixes/agent-6`, resolve no unrelated conflicts, and non-force push the exact feature head to `origin/master`; retry merge if master advances.
