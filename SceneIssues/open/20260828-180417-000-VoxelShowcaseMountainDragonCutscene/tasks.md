# Tasks

## Proven acceptance infrastructure / retained regressions
- [x] Compose cube dragon, reusable proximity trigger, reusable cutscene/dialogue, and exact `Hello, I'm Mr. Dragon.` through shared modules rather than scene-local polling/UI.
- [x] Built-player replay uses normal player movement via the deterministic waypoint replay harness; its grounded/Y-offset traversal predicate and test were restored after a later branch deletion regressed this issue's evidence path. Earlier seam proof passed run `33391220613`; current-head replay still requires revalidation.
- [x] Keep generic Box/Frustum raster fast paths reusable/output-equivalent; independent proof passed run `33357975697`.
- [x] Keep startup bake guards at 240 s / 14 GiB and preserve binary-safe accepted-payload handoff. Run `33371715298` proved the handoff path but its payload is not visually accepted.
- [x] Keep focused module-local Mountain Dragon validation and player-safe shader evidence. Run `33406812093` passed; this does not substitute for production `VoxelShowcase` visual acceptance.
- [x] Record old path/core minimal repro after repeated visual failures (`experiment-010-switchback-core-gap-minimal-repro.md`).
- [x] Reject the old path-driven mountain family after built-player review; terrace/support geometry must not define the landform.
- [x] After the repaired standalone route timed out again, isolate the repeated replay symptom before another fix. `experiment-017-turn-entry-arrival-radius-minimal-repro.md` proves `resolved-49` was accepted ~1.23 m early under the global 1.25 m radius before the next steering chord stalled against the constrained switchback; use only the existing per-waypoint radius seam for the issue-owned repair.

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
- [x] Prove exact-source production road resolves within 280 permille / 42 dm bounds through the authoritative resolver and generic terrain corridor. Run `33472689582` passed focused production acceptance plus automatic mountain-dragon module validation on exact source `dc10c20f...`. Freestanding support/causeway/trench visual rejection remains part of final human review rather than structural proof.

## Independent reuse / correctness / cost
- [x] Execute current-head `MountainClimateReuseTests.SameBuilderSupportsMateriallyDifferentShapeAndClimateCombinations`; exact-source reuse run `33473157863` passed both climate/shape reuse tests.
- [x] Execute current-head `MountainLandformTests.SameSpecProducesSameMassesAndSurfaceSamples` and `SemanticShapeInputsProduceMateriallyDifferentMountainFamilies`; run `33473157863` passed both plus climate semantic checks.
- [x] Execute current-head `MountainLandformTests.VoxelCatalogueCompilesExactSurfaceMassesWithinPrimitiveBudget`; run `33473157863` passed.
- [x] Execute current-head `MountainRoadIntegrationTests`: legal route remains within semantic grade/cut-fill bounds, over-constrained route rejects in search or grading, and lowering uses shared `EmitTerrainCorridor` with no `EmitRamp`. Exact-source run `33473157863` passed all three road integration tests using resolver-nearest-integer planar-distance semantics.
- [ ] Check raster/build cost, memory/bake blast radius, and shared-road behavior; keep global budgets and 240 s / 14 GiB guards unchanged.

## Mountain Dragon composition
- [x] Recompose VoxelShowcase from parameterized natural mountain + shared road ascent + usable summit + supported red cube dragon + reusable proximity/cutscene dialogue.
- [ ] Keep mountain placement clear of unrelated castle/feature ownership while ensuring the road entrance connects to normal accessible terrain. Current evidence setup places the player south-west of the mountain through the generic pre-replay setup seam, then requires ordinary grounded movement through the exterior approach and path base; built-player proof remains pending.
- [x] Focused behavioral validation uses the production landform/network and checks resolved grade/cut-fill/summit approach.
- [x] Module-validation metadata tracks the redesigned production paths and exact focused filter.
- [x] Migrate `MountainDragonProductionAcceptanceTests` from removed legacy landmark assumptions to current landform/road/placeholder/composition/dialogue contracts.
- [x] Diagnose repeated 60 dm then 50 dm production cut/fill symptom before third fix; lower only Showcase `RidgeStrengthPermille` from 620 to 300 based on experiment 016, leaving shared APIs and road constraints unchanged.
- [x] Correct stale acceptance-test proxies without weakening production policy: semantic mountain size is configured >=1000 dm major diameter with >=80% realized occupancy; all road-grade validations now use the resolver's nearest-integer planar distance.
- [x] Regenerate `mountain-dragon-evidence-route.json` from the final resolved production road. Run `33475807726` emitted the authoritative 94-point route; the fixture follows every resolved ascent point, anchors grounded vertical evidence at path base, and uses resolver-derived Y offsets at representative captures through the summit proximity point. After run `33480426658` showed the 100 s budget was consumed by unrelated castle-clear transit, the route now uses generic `initialPlayerPlacement` before replay and keeps every asserted approach/ascent segment on normal `CharacterMotor.Step` movement.
- [x] Keep the constrained `resolved-49` turn-entry point on the authoritative route but require `arrivalRadius: 0.35` before redirecting to `mid-turn`; route-wide tolerance and production motor/road policy stay unchanged.
- [ ] Bump startup-bake provenance for the redesigned landform/road realization so rejected old bytes cannot satisfy the new source.

## Latest exact-source CI
- [x] Run `33469216133` completed; requested road suite exposed legitimate `Blocked` rejection and standalone player exposed repeated 50 dm vs 42 dm production cut/fill.
- [x] Run `33471409821` completed; exact ridge-strength source built/replayed, but focused acceptance stopped on a stale raw catalogue-footprint >=1000 proxy. Regression corrected to semantic authored/realized size invariants.
- [x] Run `33471667027` completed; exact source reached a resolved production road, then failed only because the test floored Euclidean run where `WorldRoadResolver` rounds to nearest integer. Standalone replay passed; module validation correctly skipped after focused failure.
- [x] Run `33472015921` completed; focused production acceptance passed and standalone `VoxelShowcase` replay passed. Automatic module validation selected `mountain-dragon` plus integration coverage but its focused scene driver repeated the stale floor-sqrt segment-45 assertion, aborting before marker staging. Driver corrected; production unchanged.
- [x] Run `33472689582` completed success from exact feature source `dc10c20f...`: focused production acceptance, automatically required module validation, selected validation players, and standalone SceneIssue replay all passed. Visual closure was still blocked by the then-stale evidence route.
- [x] Run `33473157863` completed success from exact feature source `dc10c20f...`: all 10 requested independent EditMode reuse/correctness tests passed; automatic mountain-dragon module validation and selected validation players also remained green.
- [x] Run `33475137516` completed failure before tests because the route-dump test omitted `using Game.WorldBuilder.Voxel`; production did not run. Exact compile cause fixed.
- [x] Run `33475807726` completed success after the compile fix; the requested serializer emitted all 94 resolved production road points and automatic module validation remained green. SceneIssue replay was deliberately omitted because this was geometry extraction, not visual evidence.
- [x] Run `33480426658` completed failure from feature head `a1b75096...`: focused production acceptance passed and module-local Mountain Dragon validation passed. Automatically derived Kentridge player build hit a Unity/Roslyn compiler-host `Method not found` while opening unchanged `CutsceneExecution.cs` (infrastructure candidate). Standalone regenerated SceneIssue replay separately reached waypoint 53/97 then timed out at 100 s; that evidence cause was repaired by generic pre-replay placement plus removal of unrelated staging transit.
- [x] Run `33492599541` completed failure from exact feature source `e511c520...`: focused production acceptance and automatic module validation passed; standalone replay reached `resolved-49` ~1.23 m early under the 1.25 m route radius, then remained 4.762 m from `mid-turn` until timeout with settled streaming/no exception. Experiment 017 isolates the turn-entry fixture root cause before the next repair.
- [ ] Run exact current feature head with the experiment-017 turn-entry precision repair through only `ci-test/fixes/agent-4`; require focused acceptance, automatically derived module/player validation, and standalone SceneIssue replay. Never replace a queued/running request.

## Production visual / built-player acceptance
- [ ] Merge then-current `origin/master` before the exact visual-final request. The previously recorded master `ef5240c7b24550dab86d0ed75388d6c99a44d47b` could not merge cleanly into the then-feature head: GitHub PR `#202` (`master` -> `fixes/agent-4`) reported non-mergeable across 126 concurrent changed files. Re-fetch both refs/mergeability before relying on this blocker; do not synthesize a merge tree.
- [ ] Independently validate the regenerated route on the current feature head: require `WAYPOINT_REPLAY` initial-setup/arm/reached/vertical/complete logs, no exceptions, and inspect returned screenshots. This does not substitute for the post-merge final visual gate.
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
