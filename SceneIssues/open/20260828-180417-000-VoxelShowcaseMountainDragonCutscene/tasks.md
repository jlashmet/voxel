# Tasks

## Proven acceptance infrastructure / retained regressions
- [x] Compose cube dragon, reusable proximity trigger, reusable cutscene/dialogue, and exact `Hello, I'm Mr. Dragon.` through shared modules rather than scene-local polling/UI.
- [x] Built-player replay uses normal player movement via the deterministic waypoint replay harness; its grounded/Y-offset traversal predicate and test were restored after a later branch deletion regressed this issue's evidence path. Earlier seam proof passed run `33391220613`; current-head replay still requires revalidation.
- [x] Keep generic Box/Frustum raster fast paths reusable/output-equivalent; independent proof passed run `33357975697`.
- [x] Keep startup bake guards at 240 s / 14 GiB and preserve binary-safe accepted-payload handoff. Run `33371715298` proved the handoff path but its payload is not visually accepted.
- [x] Keep focused module-local Mountain Dragon validation and player-safe shader evidence. Run `33406812093` passed; this does not substitute for production `VoxelShowcase` visual acceptance.
- [x] Record old path/core minimal repro after repeated visual failures (`experiment-010-switchback-core-gap-minimal-repro.md`).
- [x] Reject the old path-driven mountain family after built-player review; terrace/support geometry must not define the landform.
- [x] After repeated `resolved-49 -> mid-turn` failures, isolate root cause before another traversal fix. Experiments 017-020 progressively ruled out early waypoint acceptance, fixture drift, and missing telemetry; experiment 021 now proves horizontal arrival succeeds and the remaining failed predicate is real vertical ascent.
- [x] After the late `resolved-89` stop survived the experiment-022 summit transition change, freeze traversal changes and isolate authoritative-route identity before another fix. Experiment 023 records the first exact-source diagnostic-selection failure and the module-local correction.

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
- [x] After repeated production cut/fill failure, isolate a minimal repro/root cause before another fix. `experiment-016-ridge-road-cutfill-minimal-repro.md` supports Showcase ridge strength 300 while retaining Ridged macro shape, six ridges, roughness, 1.5-turn spiral, 280 permille grade and 42 dm cut/fill.
- [x] Prove exact-source production road resolves within 280 permille / 42 dm bounds through the authoritative resolver and generic terrain corridor. Run `33472689582` passed focused production acceptance plus automatic Mountain Dragon validation on exact source `dc10c20f...`.

## Independent reuse / correctness / cost
- [x] Execute current-head `MountainClimateReuseTests.SameBuilderSupportsMateriallyDifferentShapeAndClimateCombinations`; exact-source reuse run `33473157863` passed both climate/shape reuse tests.
- [x] Execute current-head `MountainLandformTests.SameSpecProducesSameMassesAndSurfaceSamples` and `SemanticShapeInputsProduceMateriallyDifferentMountainFamilies`; run `33473157863` passed both plus climate semantic checks.
- [x] Execute current-head `MountainLandformTests.VoxelCatalogueCompilesExactSurfaceMassesWithinPrimitiveBudget`; run `33473157863` passed.
- [x] Execute current-head `MountainRoadIntegrationTests`: legal route remains within semantic grade/cut-fill bounds, over-constrained route rejects, and lowering uses shared `EmitTerrainCorridor` with no `EmitRamp`. Exact-source run `33473157863` passed all three road integration tests.
- [ ] Check raster/build cost, memory/bake blast radius, and shared-road behavior; keep global budgets and 240 s / 14 GiB guards unchanged.

## Mountain Dragon composition
- [x] Recompose VoxelShowcase from parameterized natural mountain + shared road ascent + usable summit + supported red cube dragon + reusable proximity/cutscene dialogue.
- [ ] Keep mountain placement clear of unrelated castle/feature ownership while ensuring the road entrance connects to normal accessible terrain. Evidence starts south-west through generic pre-replay placement, then requires ordinary grounded movement; fresh-payload built-player proof remains pending.
- [x] Focused behavioral validation uses the production landform/network and checks resolved grade/cut-fill/summit approach.
- [x] Use convention/asmdef-derived module validation; remove the obsolete issue-owned `mountain-dragon.module-validation.json` registration after exact run `33653746253` proved merged CI correctly rejects it.
- [x] Migrate `MountainDragonProductionAcceptanceTests` from removed legacy landmark assumptions to current landform/road/placeholder/composition/dialogue contracts.
- [x] Diagnose repeated 60 dm then 50 dm production cut/fill symptom before third fix; lower only Showcase `RidgeStrengthPermille` from 620 to 300 based on experiment 016, leaving shared APIs and road constraints unchanged.
- [x] Correct stale acceptance-test proxies without weakening production policy: semantic mountain size is configured >=1000 dm major diameter with >=80% realized occupancy; road-grade validation uses resolver planar-distance semantics.
- [x] Regenerate `mountain-dragon-evidence-route.json` from the final resolved production road and keep representative grounded vertical evidence through summit proximity.
- [x] Keep constrained `resolved-49` turn-entry point on authoritative route with only `arrivalRadius: 0.35`; route-wide tolerance and production motor/road policy stay unchanged.
- [x] Restore startup-bake provenance for redesigned landform/road source: reusable byte/signature helper, Showcase revision 10 source signature, and runtime manifest validation now reject stale bytes.

## Latest exact-source CI
- [x] Run `33472689582` completed success: focused production acceptance, automatic module validation, selected validation players, and standalone SceneIssue replay passed before later evidence-route corrections.
- [x] Run `33473157863` completed success: all 10 independent EditMode reuse/correctness tests passed; automatic Mountain Dragon validation remained green.
- [x] Run `33475807726` completed success: requested serializer emitted authoritative production road points and automatic module validation remained green.
- [x] Run `33480426658` completed failure: focused/module validation passed; unrelated Kentridge compiler-host infrastructure failed; standalone replay timeout motivated generic initial placement.
- [x] Run `33492599541` completed failure: focused/module validation passed; replay reached `resolved-49` early and experiment 017 isolated arrival precision.
- [x] Run `33510365863` reproduced the same transition after precision repair; experiment 018 isolated named `mid-turn` drift and aligned it to resolved point 50.
- [x] Run `33532092736` reproduced the same transition after both fixture repairs; experiment 019 froze traversal changes and required motor telemetry.
- [x] Run `33639437668` reproduced the route symptom but runner preflight had stale Unity and telemetry failed to activate; experiment 020 repaired only diagnostic activation.
- [x] Run `33653746253` on exact source `cccfbd85...` produced valid telemetry: motor reaches `mid-turn` within centimetres, feetY remains ~22.10 m vs path-base 21.60 m, grounded true. Collision/steering are rejected; vertical ascent is genuinely absent. The run also proved obsolete module-registration failure. See experiment 021.
- [x] Run `33655077271` used a fresh source-matched startup payload and climbed through lower/mid/upper ascent, then exposed a new grounded late stop at `resolved-89`.
- [x] Run `33715318543` on exact source `88b43bac...` disproved the first summit-transition fix: bake passed in 167.186 s with matching 15,697,105-byte payload/SHA/signature, while replay reproduced the identical `resolved-89` stop; unrelated rendering tests remained the module blocker.
- [x] Run `33718723662` correctly selected exact source `8b32f78d...` but did not execute the top-level route serializer because required persistent module validation failed earlier in unrelated rendering; no route-identity conclusion is drawn from this run.
- [ ] Execute the module-local `MountainDragonResolvedRouteDiagnosticTests.CurrentProductionRouteSerializesForSummitRootCauseIsolation` on exact current feature source and compare its authoritative terminal points against the checked-in evidence route before any further traversal fix.
- [ ] Run exact current feature head through only `ci-test/fixes/agent-4` requesting `ShowcaseStartupBakeArtifactTests.CurrentSourceBakeExportsPayloadAndMatchingManifest`, with the same SceneIssue replay. Require current-source bake + manifest, automatically derived module gates, and standalone replay in that same checkout. Never replace a queued/running request.

## Production visual / built-player acceptance
- [x] Merge latest master once during final visual preparation; PR #218 merged master `b1b69290...` into agent-4. Master has advanced since and must be merged again only for final promotion per closure workflow.
- [ ] Independently validate the regenerated route on a fresh current-source startup payload: require `WAYPOINT_REPLAY` setup/arm/reached/vertical/complete, no exceptions, and inspect screenshots.
- [ ] Capture and human-review exact production `VoxelShowcase` approach as one substantial coherent natural mountain.
- [ ] Human-review path base and representative lower/mid/upper ascent as continuous supported road carved/graded into the landform, with no trench/tunnel/causeway artifacts.
- [ ] Verify normal grounded traversal base -> summit through the final resolved road route without jumps/teleports.
- [ ] Human-review summit: usable natural summit, cube dragon visibly/stably supported, normal approach triggers exact `Hello, I'm Mr. Dragon.` dialogue.
- [ ] Re-check final accepted bake/runtime cost under unchanged 240 s / 14 GiB contracts.

## Checked-in startup payload
- [x] Confirm tracked `Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes` is stale (11,074,525 bytes) and has no matching manifest.
- [x] Restore runtime requirement that VoxelShowcase bytes have a matching source-signature/SHA-256 provenance manifest; stale/missing sidecars cannot silently suppress current source.
- [x] Add an issue-owned one-shot Editor acceptance bridge that invokes the real baker, writes a matching Resources manifest for same-run standalone validation, and exports both bytes/manifest under `Artifacts/SingleTest/AcceptedShowcaseBake` without adding scene policy to shared CI.
- [ ] From the final visually accepted run, record exact payload size/SHA-256/content signature and manifest.
- [ ] Replace tracked `Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes` with that exact accepted payload and add the matching manifest through the repository-sanctioned binary path.
- [ ] Make the normal Showcase editor bake permanently emit the matching manifest after the one-shot acceptance path proves the restored contract.
- [ ] Validate a clean checkout consumes the exact checked-in accepted payload/manifest and passes required exact-source gates.

## Closure
- [ ] Confirm every `issue.json` acceptance criterion and every checkbox above is complete.
- [ ] Fill `resolutionSummary`, `regressionTest`, `fixCommit`, set `status: fixed` and `resolvedUtc`, and move only this assignment directly `open -> closed` after green exact-SHA built-player + visual acceptance.
- [ ] Fetch/merge then-current `origin/master`, verify ancestry, re-run any exact final-head gate required by policy, and non-force push the exact feature head to `origin/master`; if master advances, fetch/merge/retry.
