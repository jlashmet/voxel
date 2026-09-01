# Tasks — Kentridge macro-world physical realization

## Implemented foundation
- [x] Inspect assignment; `captures: []`, so `issue.json` is the acceptance contract.
- [x] Preserve source-backed `TopDownWorldLayout` / `MountingForceTopDownWorldDefinition` as topology authority through shared WorldBuilder APIs.
- [x] Add reusable deterministic geography, terrain-aware hard-route solving, generic settlement blockouts/streets, continuous hard-route surfaces, Rossdam basin, Southern Ridge/pass, and real CharacterMotor traversal.
- [x] Production/storage regressions cover determinism, reachability, roads, settlements, blocking geography, explicit route solutions, bounded slopes, all 16 generic building shells, water realization, and two-stage presentation readiness.
- [x] Prove physical-planner reuse with an independent non-Kentridge blocked-water fixture; no Kentridge policy in shared planner/catalogue APIs.
- [x] Integrate shared spatial reservations while retaining geography/route ownership in `TopDownWorldPhysicalPlanner`; independent non-Kentridge fixture rejects road/building conflicts (`33441865025`).
- [x] Feature-aware vertical residency/readiness publishes authored upper Y-layers without widening horizontal interest or changing device/scheduler policy.
- [x] Replace demonstrated-cost solid fallback blockouts with bounded hollow wall shells and retain independent cost regression.
- [x] Isolate and correct the Southern Ridge/Orc arrival conflict in scene composition without weakening shared strict route semantics.
- [x] Correct focused storage probes to sample authored perimeter timber and roof rather than the intentionally hollow centre; exact source `b500683...` passed focused storage/module/player validation in `33464366092` attempt 3.
- [x] Run supported seven-target replay for 180 seconds; exact source `3e34713...` passed `33469175243`, all targets complete by ~80s.
- [x] Prove signed negative-Z feature streaming/publication retains Fairy authored timber (`33474641146`).
- [x] Prove the real playable three-catalogue composition retains Fairy authored timber; exact source `cdc48695...` passed `33476499718`.

## Current built-player root cause
- [x] Identify the prior end-frame settlement diagnostic as invalid because it sampled intentionally hollow generic-building centres.
- [x] Run corrected authored wall/roof probes on exact source `fb4bc6dad612f759987db5163d0d7b9e1b664405`; run `33480730488` is green but built-player authoritative storage reads `material=0` at every Moordell/Rossdam/Fairy/Orc shell/roof probe while editor storage still proves all 16 buildings.
- [x] Inspect full-resolution Fairy/Orc captures from `33480730488`; terrain/corridor content is visible but the required four-building settlements are absent, so camera/framing is not the current owner.
- [x] Inspect the one-shot selection contract and add the shared-selector regression `PlayableKentridgeCatalogueRequiresExplicitOneShotMacroSelection`.
- [x] Falsify missing playable selection before any production fix: the exact failing source's internal `Game.Kentridge.PlayableSlice.KentridgeDefinition.Build` already calls `TopDownWorldLayoutSelection.Select`; Hightown authoring and campaign planning do not intentionally consume it.
- [x] Run shared selector discriminator `33485694443` green; it proves the seed catalogue requires/consumes the explicit one-shot handoff but remains supporting evidence rather than playable caller proof.
- [x] Add real-caller discriminator `PlayableCompatibilityAuthoringLeavesMacroSelectionForCatalogueBuild`, invoking the internal playable Kentridge/Hightown adapters by reflection without widening production APIs.
- [x] Classify run `33486393258` as test-harness compile red only: nonexistent `KentridgeHiddenSpaceGeometry` made the fixture invalid, so it provides no product signal.
- [x] Correct the discriminator at source `930b6bfe28095fb4939dd26a1de12b2786de9a87` to mirror shipped semantics: playable authoring adapters run for side effects, then the valid production `Build(seed, settings, allocator)` overload consumes the handoff; no production changes or test-only geometry.
- [x] Validate the corrected real-caller discriminator on the same sole CI transport: run `33495686226` passed the requested test and repository-derived module validation on exact source `1e7bb5f8b704023b661132facd46f9c1c0835476`. The workflow red is CI-request configuration only: the bare `scene_issue` id prevented standalone replay/artifact collection.
- [x] Run the existing runtime generation trace with corrected SceneIssue path. Run `33500236600` is fully green on exact source `91997c77e7b4218bc2c2c2877526211e107f80b0`; focused storage/module/player all pass, but shipped player logs `runtime-catalogue definitions=434` and no Fairy/Orc/Moordell/Rossdam/road/ridge/water macro definitions before remote generation. This isolates catalogue composition/handoff before evaluator/rasterizer/publication.
- [x] Confirm the green Kentridge->Hightown authoring discriminator already falsifies Hightown as the handoff consumer; campaign planning contains no direct `TopDownWorldLayoutSelection` access.
- [x] Add `PlayableProductionPlanningLeavesMacroSelectionForGeometryCatalogueBuild` at feature source `48614eb3fba318e05d734cb9a698053d0ba41d57`, mirroring real opening campaign planning and the shipped settlement/hidden-space catalogue overload; no production change.
- [x] Validate `PlayableProductionPlanningLeavesMacroSelectionForGeometryCatalogueBuild`: run `33503231955` is green on exact source `e43c40579563cc72ded9027aadd1c485d5526475`, including repository-derived module validation. Synchronous production authoring/planning plus the exact settlement/hidden-space catalogue overload retains Fairy/Orc definitions.
- [x] Retain prior downstream proof: `PlayableCatalogueRetainsFairySettlementAfterHightownAndCorridorCombine` (`33476499718`) already validates final combine, `ShowcaseWorld.ConfigureGeneratedContentForGameplay`, negative-Z streaming, and storage when macro input is present; do not duplicate this path without new evidence.
- [ ] Isolate player-only lifecycle/ownership before the runtime catalogue diagnostic: inspect presentation lifecycle, every macro handoff consumer, diagnostic ownership, and duplicate `KentridgePlayableSlice` enable paths; add only the minimal discriminator selected by source evidence.
- [ ] Re-run built-player authored shell/roof probes and require Fairy/Orc storage plus readable captures after a demonstrated product fix.

## Remaining visual acceptance
- [ ] Full-resolution Moordell/Rossdam/Fairy/Orc surveys visibly show readable grounded authored blockouts, internal street/open space, and road arrival/exit.
- [ ] Rossdam lake frame visibly shows substantial authored water plus the constrained route, not a thin distant strip.
- [ ] Southern Ridge/pass frame visibly establishes the barrier/pass relationship.
- [ ] Final `macro-network-overview` remains closure-quality on the final exact SHA.
- [ ] Representative built-player CharacterMotor road traversal remains visible and process-clean on the final exact SHA.

## Runtime / cost
- [x] Terrain-relief sampling remains bounded to 25 x 16 = 400 deterministic catalogue queries; shared feature scheduler unchanged.
- [x] Existing exact evidence: 20 hard routes, 824 route tiles, 5 constrained routes, 16 generic buildings; captured steady-state FPS >200 on later replay windows.
- [x] No unrelated SceneIssue implementation, feature-branch `.github/test-request.json`, custom CI transport/workflow, CharacterMotor/load-radius/device-budget change.
- [ ] Quantify actual additional vertical resident/generated region count against baseline; numerically prove no horizontal interest-radius/device-budget change.
- [ ] Measure final lake dimensions/depth/cells, route tile/solve/constrained counts, feature work/time, CPU/FPS, memory, streaming convergence, render/far-field telemetry against budgets.
- [ ] Quantify final fixed-replay target timing.
- [ ] Fetch current master, merge it before final validation, and re-check exact feature diff.
- [ ] Re-run final exact-SHA targeted CI after the master merge; focused regression + repository-derived module validation + supported real-player smoke must all be green for the same final feature SHA.

## Acceptance / closure
- [ ] (1) Source-backed macro graph remains authoritative through shared WorldBuilder APIs.
- [ ] (2) Every settlement has readable physical presence, including >=4 grounded generic blockouts where no richer generator owns it.
- [ ] (3) Every settlement is physically reachable from Kentridge over contiguous generated hard-route surfaces.
- [ ] (4) Roads are terrain-aware; blocked geography requires explicit semantic solutions and no silent lake/cliff/building crossing.
- [ ] (5) Reusable geographic authoring/query covers required region kinds, extents/elevation, relationships, deterministic variation, terrain output, and route/placement constraints.
- [ ] (6) Built world visibly contains a substantial lake + ridge and at least one geography-altered hard route.
- [ ] (7) Regional terrain visibly reads as differentiated countryside rather than a flat debug plane.
- [ ] (8) No second scene-local graph/direct voxel-writing/static destination hierarchy.
- [ ] (9) Focused behavioral regressions cover determinism, reachability, roads, settlements, constraints, blocked-route failure, spatial reservations, bounded water cost, evidence sequencing, vertical residency/readiness, settlement framing, hollow-shell cost, and runtime publication/storage discriminators.
- [ ] (10) Exact built-player evidence covers settlements, roads entering/leaving settlements, network survey, geography, constrained route, and CharacterMotor traversal without runtime exceptions.
- [ ] (11) Blast radius and world-build/route/CPU/GPU/memory/streaming cost are measured against budgets.
- [ ] Complete `resolutionSummary`, `regressionTest`, and `fixCommit` from the verified final result.
- [ ] Every checkbox above is complete before closure.
- [ ] After green exact gates, move this assignment directly `open -> closed`, set `status=fixed` and `resolvedUtc`, merge current master, and non-force push that exact feature head to master; retry if master advances.
