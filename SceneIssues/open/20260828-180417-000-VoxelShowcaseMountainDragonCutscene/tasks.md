# Tasks

## Proven acceptance infrastructure / retained regressions
- [x] Compose cube dragon, reusable proximity trigger, reusable cutscene/dialogue, and exact `Hello, I'm Mr. Dragon.` through shared modules rather than scene-local polling/UI.
- [x] Built-player replay uses normal player movement via the public deterministic movement/replay seam; independent seam proof passed run `33391220613`.
- [x] Keep generic Box/Frustum raster fast paths reusable/output-equivalent; independent proof passed run `33357975697`.
- [x] Keep startup bake guards at 240 s / 14 GiB and preserve binary-safe accepted-payload handoff. Run `33371715298` proved the handoff path but its payload is not visually accepted.
- [x] Keep focused module-local Mountain Dragon validation and player-safe shader evidence. Run `33406812093` passed; this does not substitute for production `VoxelShowcase` visual acceptance.
- [x] Record old path/core minimal repro after repeated visual failures (`experiment-010-switchback-core-gap-minimal-repro.md`).
- [x] Reject the old path-driven mountain family after built-player review; terrace/support geometry must not define the landform.

## Reusable mountain redesign
- [x] Replace path-coupled `MountainLandmarkSpec` ownership with semantic parameterized natural-landform inputs only.
- [x] Use one deterministic `MountainLandformSurface` authority for road queries and voxel realization.
- [x] Support materially different mountain families from semantic parameters without scene-specific generator branches.
- [x] Separate semantic climate/presentation policy from shape and concrete material ids.
- [x] Remove legacy mountain-owned path tiers, ramp emission, support masses and path headroom carving from production Mountain Dragon composition.

## Existing road-system integration
- [x] Resolve Mountain Dragon ascent through `WorldRoadIntent` / `WorldRoadResolver` using `IWorldRoadTerrain`; no parallel resolver.
- [x] Use only narrow reusable `MountainLandformRoadTerrain` for mountain + fallback terrain composition.
- [x] Lower through `WorldRoadNetwork` and generic `EmitTerrainCorridor`; no production `EmitRamp` fallback.
- [x] Derive encounter proximity and focused validation from the resolved road geometry.
- [x] After repeated production cut/fill failure, isolate a minimal repro/root cause before another fix. `experiment-016-ridge-road-cutfill-minimal-repro.md` shows the spiral repeatedly crosses secondary radial ridge frusta; base terrain and shared resolver defects are ruled out. Parameter discrimination supports Showcase ridge strength 300 while retaining Ridged macro shape, six ridges, roughness, 1.5-turn spiral, 280 permille grade and 42 dm cut/fill.
- [ ] Prove exact-source production road now resolves within 280 permille / 42 dm bounds and has no freestanding support towers/causeways. Run `33472015921` passed the focused production acceptance, including authoritative resolved road and generic `EmitTerrainCorridor`; exact module-local retry plus final visual review remain.

## Independent reuse / correctness / cost
- [ ] Execute current-head `MountainClimateReuseTests.SameBuilderSupportsMateriallyDifferentShapeAndClimateCombinations`.
- [ ] Execute current-head `MountainLandformTests.SameSpecProducesSameMassesAndSurfaceSamples` and `SemanticShapeInputsProduceMateriallyDifferentMountainFamilies`.
- [ ] Execute current-head `MountainLandformTests.VoxelCatalogueCompilesExactSurfaceMassesWithinPrimitiveBudget`.
- [ ] Execute current-head `MountainRoadIntegrationTests`: legal route remains within semantic grade/cut-fill bounds, over-constrained route rejects in search or grading, and lowering uses shared `EmitTerrainCorridor` with no `EmitRamp`. The independent grade assertion now uses the resolver's nearest-integer planar-distance semantics rather than floor `Math.Sqrt`.
- [ ] Check raster/build cost, memory/bake blast radius, and shared-road behavior; keep global budgets and 240 s / 14 GiB guards unchanged.

## Mountain Dragon composition
- [x] Recompose VoxelShowcase from parameterized natural mountain + shared road ascent + usable summit + supported red cube dragon + reusable proximity/cutscene dialogue.
- [ ] Keep mountain placement clear of unrelated castle/feature ownership while ensuring the road entrance connects to normal accessible terrain.
- [x] Focused behavioral validation uses the production landform/network and checks resolved grade/cut-fill/summit approach.
- [x] Module-validation metadata tracks the redesigned production paths and exact focused filter.
- [x] Migrate `MountainDragonProductionAcceptanceTests` from removed legacy landmark assumptions to current landform/road/placeholder/composition/dialogue contracts.
- [x] Diagnose repeated 60 dm then 50 dm production cut/fill symptom before third fix; lower only Showcase `RidgeStrengthPermille` from 620 to 300 based on experiment 016, leaving shared APIs and road constraints unchanged.
- [x] Correct stale acceptance-test proxies without weakening production policy: semantic mountain size is configured >=1000 dm major diameter with >=80% realized occupancy; all road-grade validations now use the resolver's nearest-integer planar distance.
- [ ] Regenerate `mountain-dragon-evidence-route.json` from the final resolved production road; current legacy switchback/Y-offset route must not count for closure. Run `33472015921` diagnostic captures confirm it still visits castle coordinates instead of Mountain Dragon.
- [ ] Bump startup-bake provenance for the redesigned landform/road realization so rejected old bytes cannot satisfy the new source.

## Latest exact-source CI
- [x] Run `33469216133` completed; requested road suite exposed legitimate `Blocked` rejection and standalone player exposed repeated 50 dm vs 42 dm production cut/fill.
- [x] Run `33471409821` completed; exact ridge-strength source built/replayed, but focused acceptance stopped on a stale raw catalogue-footprint >=1000 proxy. Regression corrected to semantic authored/realized size invariants.
- [x] Run `33471667027` completed; exact source reached a resolved production road, then failed only because the test floored Euclidean run where `WorldRoadResolver` rounds to nearest integer. Standalone replay passed; module validation correctly skipped after focused failure.
- [x] Run `33472015921` completed; focused production acceptance passed and standalone `VoxelShowcase` replay passed. Automatic module validation selected `mountain-dragon` plus integration coverage but its focused scene driver repeated the stale floor-sqrt segment-45 assertion, aborting before marker staging. Driver corrected; production unchanged.
- [ ] Submit/retry the next exact-source production acceptance request through only `ci-test/fixes/agent-4`; never replace it while queued/running.

## Production visual / built-player acceptance
- [ ] Merge then-current `origin/master` before the exact visual-final request.
- [ ] Run exact-source focused + automatically derived module/player validation through only `ci-test/fixes/agent-4`.
- [ ] Capture and human-review exact production `VoxelShowcase` approach as one substantial coherent natural mountain.
- [ ] Human-review path base and representative lower/mid/upper ascent as continuous supported road carved/graded into the landform, with no trench/tunnel/causeway artifacts.
- [ ] Verify normal grounded traversal base -> summit through the final resolved road route without jumps/teleports.
- [ ] Human-review summit: usable natural summit, cube dragon visibly/stably supported, normal approach triggers exact `Hello, I'm Mr. Dragon.` dialogue.
- [ ] Re-check final accepted bake/runtime cost under unchanged 240 s / 14 GiB contracts.

## Checked-in startup payload
- [x] Runtime requires both `ShowcaseWorld.bytes` and matching `ShowcaseWorld.manifest.txt`; current tracked payload is stale and lacks the manifest.
- [x] Mountain-Dragon evidence collection can preserve same-run payload, manifest, source SHA, byte size and SHA-256 under uploaded artifacts.
- [ ] From the final visually accepted run, record exact payload size/SHA-256/content signature and manifest.
- [ ] Replace tracked `Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes` with that exact accepted payload and add the matching manifest through the repository-sanctioned binary path.
- [ ] Validate a clean checkout consumes the exact checked-in accepted payload/manifest and passes required exact-source gates.

## Closure
- [ ] Confirm every `issue.json` acceptance criterion and every checkbox above is complete.
- [ ] Fill `resolutionSummary`, `regressionTest`, `fixCommit`, set `status: fixed` and `resolvedUtc`, and move only this assignment directly `open -> closed` after green exact-SHA built-player + visual acceptance.
- [ ] Fetch/merge then-current `origin/master`, verify ancestry, re-run any exact final-head gate required by policy, and non-force push the exact feature head to `origin/master`; if master advances, fetch/merge/retry.