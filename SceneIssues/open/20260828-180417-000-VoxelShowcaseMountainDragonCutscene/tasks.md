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
- [x] After the late `resolved-89` stop survived the experiment-022 summit transition change, freeze traversal changes and isolate authoritative-route identity before another fix. Experiments 023-024 prove the terminal fixture is stale after summit-supported, but current resolved point 89 is unchanged; the collision symptom therefore still requires a realized-corridor root cause.

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
- [x] Check raster/build cost, memory/bake blast radius, and shared-road behavior; keep global budgets and 240 s / 14 GiB guards unchanged. Run `33715318543` baked the production payload in 167.186 s under the unchanged guard at 15,697,105 bytes; current-source runs emitted the same-size payload with signature `7554A9C4` / SHA-256 `44cb5af102a90ce84d9d51e9a40f9a5bf779bc9d1ad881fe9a04fd1a2d825632`. Exact shared-road integration remained green in run `33473157863`.

## Mountain Dragon composition
- [x] Recompose VoxelShowcase from parameterized natural mountain + shared road ascent + usable summit + supported red cube dragon + reusable proximity/cutscene dialogue.
- [ ] Keep mountain placement clear of unrelated castle/feature ownership while ensuring the road entrance connects to normal accessible terrain. Evidence starts south-west through generic pre-replay placement, then requires ordinary grounded movement; fresh-payload built-player proof remains pending.
- [x] Focused behavioral validation uses the production landform/network and checks resolved grade/cut-fill/summit approach.
- [x] Use convention/asmdef-derived module validation; remove the obsolete issue-owned `mountain-dragon.module-validation.json` registration after exact run `33653746253` proved merged CI correctly rejects it.
- [x] Migrate `MountainDragonProductionAcceptanceTests` from removed legacy landmark assumptions to current landform/road/placeholder/composition/dialogue contracts.
- [x] Diagnose repeated 60 dm then 50 dm production cut/fill symptom before third fix; lower only Showcase `RidgeStrengthPermille` from 620 to 300 based on experiment 016, leaving shared APIs and road constraints unchanged.
- [x] Correct stale acceptance-test proxies without weakening production policy: semantic mountain size is configured >=1000 dm major diameter with >=80% realized occupancy; road-grade validation uses resolver planar-distance semantics.
- [x] Regenerate `mountain-dragon-evidence-route.json` terminal points from the current 96-point authoritative production road. Run `33719954172` supplied the authoritative tail; the net fixture diff from pre-regeneration head `0ba0ef69...` to `dadf9f8d...` is one file (+14/-2), updating only points 91-94 while preserving grounded/vertical/capture semantics. Fixture correction is evidence only, not the traversal fix.
- [x] Keep constrained `resolved-49` turn-entry point on authoritative route with only `arrivalRadius: 0.35`; route-wide tolerance and production motor/road policy stay unchanged.
- [x] Restore startup-bake provenance for redesigned landform/road source: reusable byte/signature helper, Showcase revision 10 source signature, and runtime manifest validation now reject stale bytes.
- [ ] Isolate a minimal realized terrain-corridor/collision mismatch around current resolved points 88-91 before any further route-control, motor/tolerance, grade/cut-fill, or summit-placement change. Run `33754305666` proves segment 90->91 needs only 3-10 dm of cut while its emitted corridor allows 42 dm and clears 24 dm above; insufficient cut depth is ruled out. Run `33806764602` proves the production p90->p91 winner transition is continuous (`s135p0` -> `s136p0`) with smooth targets and full player-scale coverage, so shared terminal overlap/order is also ruled out. Run `33815788264` executed Experiment 027 on exact source `ebbeec4c...` but the diagnostic's explicit 16,384-brick test pool exhausted inside `GenerateRegionBlocking` before any realized-column sample was emitted. Commit `c48d114d...` changes only that diagnostic to 65,536 test bricks because the blocking helper intentionally bypasses eviction; production streaming policy and the 14 GiB allocation guard are unchanged. Exact-source rerun is required before any production fix.

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
- [x] Run `33719954172` executed module-local `MountainDragonResolvedRouteDiagnosticTests.CurrentProductionRouteSerializesForSummitRootCauseIsolation`: current route has 96 points and diverges from the fixture only after summit-supported; current resolved point 89 remains `(-1080,468,280)` dm. The same module emitted a fresh bake manifest (`7554A9C4`, SHA-256 `44cb5af1...`). Overall CI still failed in unrelated `VoxelEngine.Rendering.Tests.EditMode`, so replay was skipped.
- [x] Run `33746226437` generated a fresh current-source payload and ordinary grounded replay cleared old `resolved-89`, reached `summit-supported`, then stalled targeting `resolved-91` with stable feet about `(-108.50,47.10,27.50)` m, grounded, 3.808 m remaining and zero one-second movement until the 100 s timeout. Payload was 15,697,105 bytes, signature `7554A9C4`, SHA-256 `44cb5af102a90ce84d9d51e9a40f9a5bf779bc9d1ad881fe9a04fd1a2d825632`. Later renderer disposal failure followed the product timeout and is not an infrastructure retry reason.
- [x] Run `33749922739` did not execute the new terminal-corridor diagnostic because the Showcase EditMode test asmdef lacked the existing Structures API reference; fixed only that test dependency in `ed2bcf56...` rather than retrying a deterministic compile failure.
- [x] Run `33754305666` executed and passed `CurrentProductionTerminalCorridorSerializesForCollisionIsolation`. It measured analytic terrain over segment 90->91 at `+3/+3/+5/+6/+7/+8/+9/+9/+10` dm above road target while emitted `EmitTerrainCorridor` allows 42 dm cut and 24 dm clear-above. Overall workflow later failed unrelated GPU renderer oracles, so the targeted mountain evidence is retained and the run is not retried as infrastructure.
- [x] Run `33802313426` did not execute the terminal-winner diagnostic because its test assembly needed an explicit `Unity.Collections` reference for `FeatureDefinition.Name`/`FixedString64Bytes`; commit `0d0999ba...` fixed only that deterministic test dependency.
- [x] Run `33806764602` executed and passed `CurrentProductionTerminalWinnerSerializesForCollisionIsolation` on exact source `152fc7f8...`: p89/p90/p91 are `(-1080,468,280)`, `(-1089,471,288)`, `(-1120,482,260)` dm; the built stall `(-1085,275)` is not p90; winner changes smoothly from `s135p0` to `s136p0` with centre targets `473,474,475,476,478,479,480,481,483` dm and full lateral coverage. A later Unity Test Framework temporary init-scene restoration failure aborted the workflow after the requested test passed, so no product retry is justified.
- [x] Merge requested latest master `f5593cc1236ba3963fc5713a11df35292628e97d` into `fixes/agent-4`; merge head `3b88858895abd192d312966697e504b0658bceeb` was verified behind master by 0 before subsequent issue-doc commits.
- [x] Run `33815788264` selected exact source `ebbeec4c...` and executed `CurrentProductionRealizedStallFootprintSerializesForCollisionIsolation`, but failed deterministically in 9.719 s because the diagnostic's explicit 16,384-brick pool exhausted while the blocking production feature build rasterized the region. No realized stall samples were emitted, so no collision conclusion is drawn from that run.
- [ ] Re-run Experiment 027 `CurrentProductionRealizedStallFootprintSerializesForCollisionIsolation` on the exact current feature SHA after the diagnostic-only brick headroom correction, through only `ci-test/fixes/agent-4`; do not replace a queued/running request.
- [ ] Run exact current feature head through only `ci-test/fixes/agent-4` requesting `ShowcaseStartupBakeArtifactTests.CurrentSourceBakeExportsPayloadAndMatchingManifest`, with the same SceneIssue replay. Require current-source bake + manifest, automatically derived module gates, and standalone replay in that same checkout. Never replace a queued/running request.

## Production visual / built-player acceptance
- [x] Merge latest master once during current visual/root-cause preparation; branch synchronized through master `f5593cc1236ba3963fc5713a11df35292628e97d`. Final promotion still requires the then-current master merge per closure workflow.
- [ ] Independently validate the regenerated route on a fresh current-source startup payload: require `WAYPOINT_REPLAY` setup/arm/reached/vertical/complete, no exceptions, and inspect screenshots. Run `33746226437` is fresh but does not qualify because traversal timed out before complete.
- [ ] Capture and human-review exact production `VoxelShowcase` approach as one substantial coherent natural mountain. Run `33746226437` is explicitly rejected: approach reads as multiple exposed/segmented masses rather than one coherent natural mountain.
- [ ] Human-review path base and representative lower/mid/upper ascent as continuous supported road carved/graded into the landform, with no trench/tunnel/causeway artifacts. Run `33746226437` upper-road/summit views show abrupt terrain faces and are not accepted.
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
- [ ] Fetch/merge then-current `origin/master`, verify ancestry, re-run any exact final-head gate required by policy, then promote `fixes/agent-4` to `master` only through a PR with auto-merge enabled. Do not push the exact branch head directly to `origin/master`; if master advances, fetch/merge/revalidate as required.
