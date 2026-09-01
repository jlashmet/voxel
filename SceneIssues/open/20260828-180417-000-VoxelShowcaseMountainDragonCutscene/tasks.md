# Tasks

## Proven acceptance infrastructure / retained regressions
- [x] Compose cube dragon, reusable proximity trigger, reusable cutscene/dialogue, and exact `Hello, I'm Mr. Dragon.` through shared modules rather than scene-local polling/UI.
- [x] Built-player replay uses normal player movement via the public deterministic movement/replay seam; independent seam proof passed run `33391220613`.
- [x] Keep generic Box/Frustum raster fast paths reusable/output-equivalent; independent proof passed run `33357975697`.
- [x] Keep startup bake guards at 240 s / 14 GiB and preserve binary-safe accepted-payload handoff. Run `33371715298` proved the handoff path but its payload is not visually accepted.
- [x] Keep focused module-local Mountain Dragon validation and player-safe shader evidence. Run `33406812093` passed; this does not substitute for production `VoxelShowcase` visual acceptance.
- [x] Record old path/core minimal repro after repeated visual failures (`experiment-010-switchback-core-gap-minimal-repro.md`).
- [x] Reject the old revision-6/8/9 path-driven mountain family after built-player review. The road terraces/support geometry define the landform and do not read as a natural mountain.
- [x] Record reconstruction CI failures: `33447340071` failed compile due omitted proven dependencies; source `a109bf20...` restored them. Retry `33449145780` compiled and standalone replay succeeded but requested focused filter matched zero tests, so it is not acceptance evidence.

## Reusable mountain redesign
- [x] Replace path-coupled `MountainLandmarkSpec` ownership with a semantic parameterized mountain-landform contract: placement, footprint/aspect, height, summit character, deterministic seed, macro shape/ridge/asymmetry, and bounded roughness only. No road, switchback, traversal, dragon, or Showcase policy in the generic landform.
- [x] Implement one deterministic mountain surface authority used both for `HeightAt`/surface queries and for voxel realization, so routing evidence and rendered/collision geometry cannot diverge.
- [x] Support materially different mountain shapes from parameters (at minimum broad/massif versus narrow/asymmetric/ridged) without separate generators or scene-specific branches.
- [x] Separate reusable climate/presentation policy from shape: semantic altitude/slope bands choose rock/ground-cover/snow-like roles while concrete material ids remain caller-owned. Independent `MountainClimateReuseTests` passed run `33462667493` on source `4b4abc3e...`.
- [x] Remove old mountain-owned path tiers, ramp emission, path support masses, and path headroom carving from the production Mountain Dragon composition once the road-backed path is active. The legacy catalogue remains repository code but is no longer in `ShowcaseCatalogue.Build` production composition.

## Existing road-system integration
- [x] Resolve the Mountain Dragon ascent through `WorldRoadIntent` / `WorldRoadResolver` using the mountain surface as `IWorldRoadTerrain`; do not create a parallel mountain-road resolver.
- [x] Add only the narrow reusable terrain-composition adapter required for road resolution to sample authored mountain surface plus base terrain outside its footprint. `MountainLandformRoadTerrain` composes mountain + fallback terrain without changing road policy.
- [x] Lower the resolved ascent through `WorldRoadNetwork` + `WorldRoadNetworkVoxelCatalogue` / generic terrain-corridor rasterization so existing road grade, cut/fill, shoulder, clearance, and presentation semantics remain authoritative. The WorldBuilder wrapper only hides backend voxel settings/material plumbing.
- [x] Derive encounter proximity and focused validation from the same resolved road geometry rather than duplicated legacy switchback coordinates.
- [ ] Prove the production road cuts/fills into the natural mountain within the configured 280 permille / 42 dm bounds and does not create freestanding support towers/causeways. Run `33468581318` exposed a real 60 dm cut/fill failure at point 33; source now keeps the same contracts and same 1.5-turn ascent while doubling semantic control resolution from 13 to 25 so the shared resolver follows the ridged contour. Exact-source retry `33469216133` is queued and must not be replaced.

## Independent reuse / correctness / cost
- [ ] Execute current-head `MountainClimateReuseTests.SameBuilderSupportsMateriallyDifferentShapeAndClimateCombinations`; the independent non-Showcase two-shape/two-climate fixture is already implemented.
- [ ] Execute current-head `MountainLandformTests.SameSpecProducesSameMassesAndSurfaceSamples` and `SemanticShapeInputsProduceMateriallyDifferentMountainFamilies`; the deterministic/different-shape assertions are already implemented.
- [ ] Execute current-head `MountainLandformTests.VoxelCatalogueCompilesExactSurfaceMassesWithinPrimitiveBudget`; it already checks every generated mass against the emitted voxel catalogue and the feature primitive budget.
- [ ] Execute current-head `MountainRoadIntegrationTests`: legal route must remain within semantic grade/cut-fill bounds, over-constrained route must reject, and lowering must use shared `EmitTerrainCorridor` with no `EmitRamp`. Run `33468581318` executed 2/3; its remaining failure was an invalid `>8` point-density assumption, now replaced with semantic route/cut-fill assertions.
- [ ] Check raster/build cost, memory/bake blast radius, and shared-road behavior; keep existing global budgets and 240 s / 14 GiB guards unchanged.

## Mountain Dragon composition
- [x] Recompose VoxelShowcase from: parameterized natural mountain + existing road ascent + usable summit + supported red cube dragon + existing proximity/cutscene dialogue.
- [ ] Keep mountain placement clear of unrelated castle/feature ownership while ensuring the road entrance connects to normal accessible terrain.
- [x] Update focused behavioral regressions to exercise the new production WorldBuilder mountain + road path rather than old path-tier internals. `MountainDragonValidationSceneDriver` builds the production landform/network and validates resolved grade/cut-fill/summit approach.
- [x] Repair focused validation registration: `mountain-dragon.module-validation.json` now tracks the redesigned Showcase/landform/road production files and retains the exact PlayMode filter.
- [x] Migrate `MountainDragonProductionAcceptanceTests` from removed `CreateLandmark`/legacy `MountainLandmarkSpec` assumptions to the current landform, resolved road, shared terrain-corridor, summit placeholder, production composition, and exact dialogue flow.
- [ ] Regenerate `mountain-dragon-evidence-route.json` from the final resolved production road. Current file is legacy switchback/Y-offset evidence and must not be used for closure.
- [ ] Bump startup-bake provenance for the redesigned landform/road realization so rejected old bytes cannot satisfy the new source.

## CI blocker
- [ ] External prerequisite: exact-source request `33469216133` (CI `8656913e...`, source parent `c2bfa596...`) is queued on the authorized `ci-test/fixes/agent-4` transport with no runner assigned (`runner_id=0`). Repeated checks show no repository workflow in progress. Preserve it; do not replace it while queued/running. Current feature head has documentation-only commits after that source, so a final exact-head gate will still be required after this request completes.

## Production visual / built-player acceptance
- [ ] Merge then-current `origin/master` before the exact visual-final request; current master `71e5b6b1...` is one commit ahead of the feature merge-base.
- [ ] Run exact-source focused + automatically derived module/player validation through only `ci-test/fixes/agent-4`; never replace a queued/running request.
- [ ] Capture and human-review the exact production `VoxelShowcase` approach. It must read first as one substantial coherent natural mountain, not terraces/support structures.
- [ ] Human-review path base and representative lower/mid/upper ascent. The existing road must read as carved/graded into the landform, with continuous supported walking surface and no trench/tunnel/causeway artifacts.
- [ ] Verify normal grounded traversal base -> summit through the final resolved road route without jumps/teleports and within the production replay contract.
- [ ] Human-review summit: usable natural summit, cube dragon visibly/stably supported, normal approach triggers exact `Hello, I'm Mr. Dragon.` dialogue.
- [ ] Re-check final accepted bake/runtime cost under unchanged 240 s / 14 GiB contracts.

## Checked-in startup payload
- [x] Runtime requires both `ShowcaseWorld.bytes` and matching `ShowcaseWorld.manifest.txt`; existing tracked payload is stale. Current branch has only the 11,074,525-byte legacy `ShowcaseWorld.bytes` and no manifest.
- [x] Mountain-Dragon-only evidence collection preserves fresh same-run payload, manifest, source SHA, byte size, and SHA-256 under uploaded artifacts.
- [ ] From the final visually accepted run, record exact payload size/SHA-256/content signature and manifest.
- [ ] Replace tracked `Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes` with that exact accepted payload and add the matching manifest through the repository-sanctioned binary Git-object path.
- [ ] Validate a clean checkout consumes the exact checked-in accepted payload/manifest and passes required exact-source gates.

## Closure
- [ ] Confirm every `issue.json` acceptance criterion and every checkbox above is complete.
- [ ] Fill `resolutionSummary`, `regressionTest`, `fixCommit`, set `status: fixed` and `resolvedUtc`, and move only this assignment directly `open -> closed` after green exact-SHA built-player + visual acceptance.
- [ ] Fetch/merge then-current `origin/master`, verify ancestry, re-run any exact final-head gate required by policy, and non-force push the exact feature head to `origin/master`; if master advances, fetch/merge/retry.
