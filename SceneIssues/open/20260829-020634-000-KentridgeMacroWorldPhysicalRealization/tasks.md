# Tasks — Kentridge macro-world physical realization

## Implemented foundation
- [x] Preserve source-backed `TopDownWorldLayout` / `MountingForceTopDownWorldDefinition` as topology authority through shared WorldBuilder APIs.
- [x] Add reusable deterministic regional geography, terrain-aware hard-route solving, generic settlement blockouts/streets, continuous hard-route surfaces, Rossdam basin, Southern Ridge/pass, and CharacterMotor traversal.
- [x] Prove reuse with independent non-Kentridge blocked-water/spatial-reservation fixtures; keep Kentridge placement policy out of shared planner/catalogue APIs.
- [x] Preserve existing streaming/device/scheduler budgets and radius-3 horizontal residency (29 X/Z columns).
- [x] Replace demonstrated-cost solid generic blockouts with grounded hollow shells/perimeter plinths; combined foundation+timber synthetic work reduced 79.7%.
- [x] Add focused behavioral regressions for deterministic macro realization, reachability, route continuity/constraints, settlement placement, spatial reservations, bounded water cost, evidence sequencing, vertical residency/readiness, settlement framing, shell cost, GPU count-batch fairness, and distant mirror relocation/churn.
- [x] Add required module-local production validation scenes for WorldBuilder, Showcase residency, Kentridge playable macro world, and GPU mirror relocation.

## Demonstrated corrections
- [x] Preserve request `6d076011180388c8346b5338c0efa6946676c91b` until completion and classify run `33930622766` from complete evidence.
- [x] Isolate macro catalogue ownership root cause: the temporary generic `ShowcaseWorld` catalogue consumed the scene-selected one-shot macro layout before the concrete Kentridge playable catalogue.
- [x] Correct ownership at the reusable boundary: generic `WorldBuilderVoxelCatalogue` uses `KentridgeCombinedVoxelCatalogue.BuildLocalOnly`, while the concrete playable catalogue remains the macro-selection owner.
- [x] Strengthen `KentridgeMacroWorldBootstrapOwnershipTests` to reproduce production Showcase-first startup order.
- [x] Migrate the Kentridge scene compatibility input bridge to Input System device reads and route HUD pressed/held state through that bridge; leave global input settings unchanged.
- [x] Keep the Structures composition regression deterministic and module-scoped by invoking its public production validation synchronously instead of yielding an unrelated URP frame.
- [x] Preserve exact request `3ebc24eb4bad34297c494592e3ca9b4a0dc2a7f6` through completed run `33932977686` and classify its full artifact before another request.
- [x] Confirm run `33932977686` restored the macro catalogue: 480 definitions including Moordell/Rossdam/Fairy/Orc, 20 roads/824 route tiles, ridge and water; prior Kentridge legacy-input storm is absent.
- [x] Isolate requested GPU-regression blocker to production `VoxelShowcase.HandleKeys()` legacy Input use before any intended GPU-liveness assertion.
- [x] Add a scene-local Input-System compatibility boundary to `VoxelEngine.Showcase` without changing global input settings or suppressing runtime errors.
- [x] Falsify stale FIFO as the Moordell convergence cause; production terrain and feature work are already nearest-first.
- [x] Keep validation demand on the real streaming path: while an elevated settlement survey waits, move the real CharacterMotor demand to the first unsettled authored building centre without force-generation, radius widening, or extra streaming budget.
- [x] Preserve exact requests `bd10ce4f594f1619e8bf48a866459c6ac0aa174f` and `59ed8938f6ace32c89817f0c417f45fddfabb26e` through runs `33937792014` and `33938299650`; both stop before runtime on the same sole `CS0246` in the new content-demand helper.
- [x] After the repeated compiler symptom, isolate the root cause before another production change: `TopDownWorldPhysicalPlan` is declared in `MountingForce.WorldGen.Voxel`, and `Game.Kentridge.PlayableSlice` already references `MountingForce.WorldGen.Voxel`; the helper was missing that namespace import.
- [x] Classify run `33938819767`: persistent tests and the requested GPU-liveness regression pass; the Kentridge macro player reaches startup/local CharacterMotor traversal with no forbidden exceptions but never emits Moordell content/capture readiness because its module-local bootstrap attached the evidence driver without the companion profile-installed content-demand driver.
- [x] Correct the module-local validation composition by attaching both macro evidence helpers; keep the SceneIssue profile self-install path unchanged and do not weaken streaming/readiness assertions or budgets.
- [x] Classify exact run `33943012273`: requested count-batch fairness and persistent tests pass; both Kentridge players exercise the production macro path, but strict visible coverage remains incomplete and durable evidence shows real checkerboard terrain holes.
- [x] Falsify frame-clock mismatch and sustained fence-block theories for the remaining stall: production scheduler/sealer share `Time.frameCount`, and `batchArenaWait=0` while phase-2 owners age for seconds.
- [x] Isolate the stronger lifecycle root cause before another renderer edit: paged completion is signaled at GPU submission, but the extraction slot/mirror-reader footprint is released later by CPU worker polling, so ownership can end either before or long after the graphics fence.
- [x] Correct GPU extraction ownership lifetime so the active-count and mirror-reader protection transfer to the in-flight graphics fence and retire exactly once at fence completion, independent of worker polling; add a behavioral regression against that production lifetime policy.
- [x] Classify exact run `33949455376`: fence-owned retirement is effective (`activeExtractions=0` while worker contexts can still report phase 2), but required Kentridge module validation remains product-red because completed paged results are not consumed promptly enough for strict visible coverage to converge.
- [x] Prioritize workers holding phase-2 paged GPU results ahead of ordinary worker polling under the existing solid deadline, preserving cursor fairness, build ceilings, streaming radius, and all acceptance budgets; add a focused behavioral ordering regression.
- [x] Classify exact run `33955763704`: repository-derived persistent validation and the requested production GPU relocation/liveness regression pass, but the 180-second feature replay remains product-red for strict multi-settlement/geography/network convergence; do not treat workflow/test green as feature acceptance.
- [x] Isolate the follow-on scheduler boundary before another budget change: an already-expired shared solid deadline breaks before the first priority worker, so a phase-9 completion owner can still be denied its bounded retirement poll despite correct ordering.
- [x] Keep phase-9 paged GPU completion owners pollable after ordinary admission budget expires while ordinary workers still stop at the unchanged deadline; add focused regressions for both exhausted-budget completion and ordinary-worker stopping behavior.
- [x] Preserve exact request `51dd1ded1e1c3778fc8cfcec178b28c7c04dee8e` through completed run `33962213806` and classify artifact `9968455702` rather than retrying the unchanged source.
- [x] Classify run `33962213806`: persistent phases pass and the requested production GPU relocation/liveness test passes `1/1`; the 120-second Kentridge module and 180-second SceneIssue replay remain product/acceptance-red because strict near-surface publication does not converge in time.
- [x] Isolate the stronger current timing boundary: strict opening publication consumes roughly the first high-90 seconds before macro evidence can begin; in the 180-second replay Moordell demand starts only after roughly 108 seconds, content becomes ready around 161 seconds, and capture remains blocked with `missing=89` at t=180.
- [x] Preserve production acceptance semantics while classifying that boundary: do not weaken `HasCompletePublishedNearSurfaceCoverage`, force-generate macro acceptance regions, widen residency, raise streaming/device budgets, or substitute storage-only evidence.
- [x] Verify renderer ownership before another production change: `fixes/agent-6` already contains published restoration `f5593cc1236ba3963fc5713a11df35292628e97d`; current master-only commits are unrelated, while active `fixes/agent-1` work is changing the same GPU mirror/page-arena/publication lifecycle and remains visual-acceptance red.

## Exact-SHA validation
- [x] Run the fence-owned extraction-lifetime regression plus `GpuSurfaceMirrorRelocationRequestedValidationTests.DistantUnrelatedChangeChurnExecutesProductionGpuLivenessRegression` on exact source `75da81caa3be77b45ecad5adb9050391d5b866a0` through `ci-test/fixes/agent-6`; classify completed product-red run `33949455376` from its durable artifact rather than retrying unchanged.
- [x] Run the phase-9 exhausted-budget completion-consumption regression plus the production GPU relocation/liveness regression on exact source `870c6bd0b9fed9005586945a328a9e5a8ed2f1dd` through `ci-test/fixes/agent-6`; focused/persistent tests are green in run `33962213806`, while built-player acceptance remains red.
- [x] Re-fetch publication refs on 2026-09-05: `origin/master=939e9a6f744313d93992b0479d5f6140d774ef42`, renderer owner `fixes/agent-1=8ae5d743dc9ecef8f0634f2144a877b0efef010f`; the required correction is not on `master`.
- [x] Recheck renderer-owner targeted workflow `33978398855` on transport head `8505b63c4a1a199ff8ed416151f98801956dd5c0`; it is completed with conclusion `failure`, so retain the external prerequisite blocker and do not replace it with overlapping agent-6 renderer work.
- [ ] After Agent 1's current GPU renderer correction is validated and merged to `master`, merge then-current `origin/master` into `fixes/agent-6` per `master-sync-required.md` and re-evaluate the strict opening/publication blocker before any further agent-6 renderer change.
- [ ] Require repository-derived module validation to pass every affected owned test assembly and all four required module-local player validations.
- [ ] Require the 180-second SceneIssue replay to reach strict built-player coverage beyond Moordell and emit settlement/geography/network evidence without runtime exceptions.
- [x] If the same acceptance symptom remains after two materially different fixes, isolate the new minimal repro/root cause before another production change.

## Module-local visual/runtime acceptance
- [ ] `MacroPhysicalWorldValidation`: production voxel output visibly proves generic settlements, hard routes, substantial water, ridge/pass, and blocked-route semantics.
- [ ] `FeatureResidencyValidation`: production `ShowcaseWorld` proves authored upper-layer residency/readiness without horizontal-radius widening.
- [ ] `KentridgeMacroWorldValidation`: real `KentridgePlayableSlice` proves source-backed settlement/road/geography relationships and strict streaming/readiness.
- [ ] `GpuSurfaceMirrorRelocationValidation`: real GPU mirror/extraction/publication path demonstrates useful recovery after distant relocation.
- [ ] Inspect full-resolution durable built-player evidence directly; classify all required views `production-quality` under repo visual rules.

## SceneIssue visual acceptance
- [ ] Moordell, Rossdam, Fairy Village, and Orc Village each visibly show readable grounded settlement presence, internal street/open space, and road arrival/exit.
- [ ] Rossdam evidence visibly shows substantial authored lake water plus the constrained route relationship.
- [ ] Southern Ridge/pass evidence visibly establishes the barrier/pass relationship.
- [ ] `macro-network-overview` visibly demonstrates the physical road network corresponding to the source-backed macro graph.
- [ ] Representative real CharacterMotor road traversal is visible, collision/streaming-active, and process-clean.
- [ ] Regional terrain between towns reads as differentiated countryside rather than a flat debug plane.

## Runtime / cost
- [x] Physical baseline recorded: 20 hard routes, 824 route tiles, 5 constrained routes, 1,090 solve steps, 16 generic buildings, max road rise 2 voxels, road step 30 dm, Rossdam water depth 24 voxels.
- [x] Partial runtime baseline recorded: median FPS 103.9, median mean-frame 9.61 ms, worker-p95 median 3.018 ms, admission-total median 1.1605 ms, ~26.99 MiB cumulative render-arena uploads over 114 calls.
- [ ] Quantify final per-target convergence timing and additional vertical resident/generated region count across the completed multi-target replay.
- [ ] Record final FPS/CPU/GPU/streaming plus process/managed/native/GPU memory against existing budgets; do not weaken budgets to pass.

## Acceptance / closure
- [x] (1) Source-backed macro graph remains authoritative through reusable WorldBuilder/shared APIs.
- [ ] (2) Every settlement has physical generated presence; unrealized destinations have >=4 grounded reusable blockouts plus circulation and road arrival/exit.
- [x] (3) Every settlement is physically reachable from Kentridge over contiguous generated hard-route surfaces.
- [x] (4) Roads are terrain-aware and require explicit semantic solutions for blocked geography.
- [x] (5) Reusable geographic-region authoring/query covers required kinds, extents/elevation, deterministic variation, relationships, terrain output, and route/placement constraints.
- [ ] (6) Exact built world visibly contains substantial lake + ridge and at least one geography-altered hard route.
- [ ] (7) Regional terrain visibly reads as intentionally differentiated world space.
- [x] (8) No second scene-local graph, direct voxel-writing authority, or giant hand-authored destination hierarchy was introduced.
- [ ] (9) Focused behavioral regressions and required module-local players pass on the final exact SHA.
- [ ] (10) Exact built application reaches usable rendered macro-world evidence with no startup/runtime exceptions and demonstrates settlements, roads, geography, constrained route, and CharacterMotor traversal.
- [ ] (11) Blast radius and world-build/route/CPU/GPU/memory/streaming cost are measured against existing budgets.
- [ ] Merge current `origin/master` before final promotion and revalidate the exact merged feature SHA if required by the repo workflow.
- [ ] Complete `resolutionSummary`, `regressionTest`, `fixCommit`, set `status: fixed`/`resolvedUtc`, and move only this assignment `open -> closed` after all gates pass.
- [ ] Open/update PR `fixes/agent-6 -> master`, enable auto-merge, monitor required PR checks until merged, and verify the closed SceneIssue is visible on `origin/master`.
- [ ] Every checkbox above is complete before closure.
