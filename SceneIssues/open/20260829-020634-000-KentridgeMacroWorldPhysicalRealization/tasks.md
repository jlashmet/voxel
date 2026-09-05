# Tasks — Kentridge macro-world physical realization

## Implemented foundation
- [x] Preserve source-backed `TopDownWorldLayout` / `MountingForceTopDownWorldDefinition` as topology authority through shared WorldBuilder APIs.
- [x] Add reusable deterministic regional geography, terrain-aware hard-route solving, generic settlement blockouts/streets, continuous hard-route surfaces, Rossdam basin, Southern Ridge/pass, and CharacterMotor traversal.
- [x] Prove reuse with independent non-Kentridge blocked-water/spatial-reservation fixtures; keep Kentridge placement policy out of shared planner/catalogue APIs.
- [x] Preserve existing streaming/device/scheduler budgets and radius-3 horizontal residency (29 X/Z columns).
- [x] Replace demonstrated-cost solid generic blockouts with grounded hollow shells/perimeter plinths; combined foundation+timber synthetic work reduced 79.7%.
- [x] Add focused behavioral regressions for deterministic macro realization, reachability, route continuity/constraints, settlement placement, spatial reservations, bounded water cost, evidence sequencing, vertical residency/readiness, settlement framing, shell cost, GPU count-batch fairness, and distant mirror relocation/churn.
- [x] Add required module-local production validation scenes for WorldBuilder, Showcase residency, Kentridge playable macro world, and GPU mirror relocation.

## Demonstrated corrections from exact run 33930622766
- [x] Preserve request `6d076011180388c8346b5338c0efa6946676c91b` until completion; do not replace it while queued/running.
- [x] Classify exact source `3c839facd018eb8118811db1a7b81644375c419f`: 180-second standalone process completed, but closure evidence was red because the runtime catalogue had 434 definitions with no macro town/road/lake/ridge entries, evidence never advanced beyond Moordell, and ~35,981 legacy-input exceptions were logged.
- [x] Classify repository-derived module validation red in `VoxelEngine.Structures` as a test-harness/render-frame boundary rather than a Structures production failure.
- [x] Isolate macro catalogue ownership root cause: the temporary generic `ShowcaseWorld` catalogue consumed the scene-selected one-shot macro layout before the concrete Kentridge playable catalogue.
- [x] Correct ownership at the reusable boundary: generic `WorldBuilderVoxelCatalogue` uses `KentridgeCombinedVoxelCatalogue.BuildLocalOnly`, while the concrete playable catalogue remains the macro-selection owner.
- [x] Strengthen `KentridgeMacroWorldBootstrapOwnershipTests` to reproduce production Showcase-first startup order.
- [x] Migrate the Kentridge scene compatibility input bridge to Input System device reads and route HUD pressed/held state through that bridge; leave global input settings unchanged.
- [x] Keep the Structures composition regression deterministic and module-scoped by invoking its public production validation synchronously instead of yielding an unrelated URP frame.

## Exact-SHA validation
- [ ] Run `GpuSurfaceMirrorRelocationRequestedValidationTests.DistantUnrelatedChangeChurnExecutesProductionGpuLivenessRegression` on the current exact feature source through `ci-test/fixes/agent-6`.
- [ ] Require repository-derived module validation to pass every affected owned test assembly and all four required module-local player validations.
- [ ] Require the 180-second SceneIssue replay to reach strict built-player coverage beyond Moordell and emit settlement/geography/network evidence without runtime exceptions.
- [ ] If the same acceptance symptom remains after two materially different fixes, isolate the new minimal repro/root cause before another production change.

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
