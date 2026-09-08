# Tasks — Kentridge macro-world physical realization

## Implemented foundation
- [x] Preserve source-backed `TopDownWorldLayout` / `MountingForceTopDownWorldDefinition` as topology authority through shared WorldBuilder APIs.
- [x] Add reusable deterministic regional geography, terrain-aware hard-route solving, generic settlement blockouts/streets, continuous hard-route surfaces, Rossdam basin, Southern Ridge/pass, and CharacterMotor traversal.
- [x] Prove reuse with independent non-Kentridge blocked-water/spatial-reservation fixtures; keep Kentridge placement policy out of shared planner/catalogue APIs.
- [x] Preserve existing streaming/device/scheduler budgets and radius-3 horizontal residency (29 X/Z columns).
- [x] Replace demonstrated-cost solid generic blockouts with grounded hollow shells/perimeter plinths; combined foundation+timber synthetic work reduced 79.7%.
- [x] Add focused behavioral regressions for deterministic macro realization, reachability, route continuity/constraints, settlement placement, spatial reservations, bounded water cost, evidence sequencing, vertical residency/readiness, settlement framing, shell cost, GPU count-batch fairness, and distant mirror relocation/churn.
- [x] Create module-local scene/scenario pairs for WorldBuilder `MacroPhysicalWorldValidation`, Showcase `FeatureResidencyValidation`, Kentridge `KentridgeMacroWorldValidation`, and Rendering `GpuSurfaceMirrorRelocationValidation`.

## Demonstrated corrections retained
- [x] Correct macro catalogue ownership so generic `ShowcaseWorld` uses local-only definitions while the concrete Kentridge playable catalogue remains macro-selection owner; cover production Showcase-first startup order.
- [x] Migrate Kentridge/Showcase compatibility input reads to the Input System boundary without changing global input settings.
- [x] Keep validation demand on the real streaming path; do not force-generate, widen residency, or add streaming budget.
- [x] Correct Kentridge module validation composition so both macro evidence helpers are attached.
- [x] Correct GPU extraction ownership lifetime so active-count/mirror-reader protection transfers to the graphics fence and retires exactly once.
- [x] Prioritize phase-2 paged results while preserving scheduler fairness and existing budgets.
- [x] Keep phase-9 paged completion owners pollable after ordinary admission budget expiry while ordinary workers still obey the unchanged deadline.
- [x] Add a validation-only Application start companion that is inert without `KentridgeMacroWorldEvidenceDriver`, waits for the production composition root, and invokes public `RequestNewGame()` exactly once from `FrontEnd/MainMenu`; cover the state decision with EditMode regression.
- [x] Preserve strict acceptance semantics: do not weaken `HasCompletePublishedNearSurfaceCoverage`, force-generate acceptance content, widen residency, raise budgets, or substitute storage-only evidence.
- [x] Reconcile current `origin/master=5c9a715a42735a0b573f436af1e3700dff2f591c`; branch is 0 behind current master.

## GPU publication liveness root cause
- [x] Classify exact source `abff52bba0c04c3940d98d6c24ebab2b081258ec`, transport `522fe183329e6ad645664e4bf1f88515b51ae5dc`, run `34213075572` as product/acceptance red: tests and requested macro regression complete, real Kentridge player advances through New Game / CharacterMotor / Moordell / Rossdam / lake-detour, then misses required `macro-network-overview` at 180 seconds.
- [x] Preserve synchronized discriminator evidence from run `34200648642`: lake content settles while strict GPU publication remains nearly flat under sustained retryable `NoSlot` exhaustion; not a streaming-authority mismatch.
- [x] After multiple materially different fixes, isolate the minimal production cause before another change: replacement generation advances Desired; an exhausted replacement leaves old live pages allocated; pressure previously rejected those pages because `live.generation != desired`, so obsolete pages could permanently pin their replacement capacity.
- [x] Verify host acknowledgement already keys eviction to `Entry.PublishedGpuGeneration`, so retiring the stale live identity is compatible with existing ownership semantics.
- [x] Correct only allocation pressure to reclaim stale live generations, including visible stale live geometry; keep filtered capacity pressure current-generation/off-screen-only.
- [x] Add real compute regression `GpuAllocationPressureStaleLiveTests` proving ordinary capacity pressure preserves visible current/stale live records while allocation pressure retires only the stale published generation and reports its published identity.
- [ ] Exact-SHA validation proves the new pressure regression passes and distant Kentridge relocation makes useful publication progress without the prior sustained stale-page `NoSlot` liveness stall.

## Exact-SHA validation
- [ ] Run targeted CI from the final synchronized `fixes/agent-6` SHA using only `ci-test/fixes/agent-6`; do not replace a queued/running request.
- [ ] Require every repository-derived affected EditMode/unit assembly to pass.
- [ ] Require every discovered affected module-local player to execute successfully, including all four macro-specific validation pairs.
- [ ] Require the 180-second SceneIssue replay to transition through the real Application lifecycle, reach strict built-player coverage beyond Moordell, and emit settlement/geography/network/traversal evidence without runtime exceptions.

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
- [x] Partial runtime baseline recorded: median FPS 103.9, mean-frame 9.61 ms, worker-p95 3.018 ms, admission-total 1.1605 ms, ~26.99 MiB cumulative render-arena uploads over 114 calls.
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
- [ ] Fetch/merge current `origin/master` again before final promotion if it advances; revalidate exact merged feature SHA when required.
- [ ] Complete `resolutionSummary`, `regressionTest`, `fixCommit`, set `status: fixed`/`resolvedUtc`, and move only this assignment `open -> closed` after all gates pass.
- [ ] Open/update PR `fixes/agent-6 -> master`, enable auto-merge, monitor required `affected` checks until merged, and verify the closed SceneIssue is visible on `origin/master`.
- [ ] Every checkbox above is complete before closure.
