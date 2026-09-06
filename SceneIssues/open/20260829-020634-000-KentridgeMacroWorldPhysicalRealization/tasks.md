# Tasks — Kentridge macro-world physical realization

## Implemented foundation
- [x] Preserve source-backed `TopDownWorldLayout` / `MountingForceTopDownWorldDefinition` as topology authority through shared WorldBuilder APIs.
- [x] Add reusable deterministic regional geography, terrain-aware hard-route solving, generic settlement blockouts/streets, continuous hard-route surfaces, Rossdam basin, Southern Ridge/pass, and CharacterMotor traversal.
- [x] Prove reuse with independent non-Kentridge blocked-water/spatial-reservation fixtures; keep Kentridge placement policy out of shared planner/catalogue APIs.
- [x] Preserve existing streaming/device/scheduler budgets and radius-3 horizontal residency (29 X/Z columns).
- [x] Replace demonstrated-cost solid generic blockouts with grounded hollow shells/perimeter plinths; combined foundation+timber synthetic work reduced 79.7%.
- [x] Add focused behavioral regressions for deterministic macro realization, reachability, route continuity/constraints, settlement placement, spatial reservations, bounded water cost, evidence sequencing, vertical residency/readiness, settlement framing, shell cost, GPU count-batch fairness, and distant mirror relocation/churn.
- [x] Add required module-local production validation scenes for WorldBuilder, Showcase residency, Kentridge playable macro world, and GPU mirror relocation.

## Demonstrated corrections retained
- [x] Correct macro catalogue ownership so generic `ShowcaseWorld` uses local-only definitions while the concrete Kentridge playable catalogue remains macro-selection owner; cover production Showcase-first startup order.
- [x] Migrate Kentridge/Showcase compatibility input reads to the Input System boundary without changing global input settings.
- [x] Keep validation demand on the real streaming path; do not force-generate, widen residency, or add streaming budget.
- [x] Correct Kentridge module validation composition so both macro evidence helpers are attached.
- [x] Correct GPU extraction ownership lifetime so active-count/mirror-reader protection transfers to the graphics fence and retires exactly once.
- [x] Prioritize phase-2 paged results while preserving scheduler fairness and existing budgets.
- [x] Keep phase-9 paged completion owners pollable after ordinary admission budget expiry while ordinary workers still obey the unchanged deadline.
- [x] Preserve the stronger root-cause result from exact run `33962213806`: strict opening/publication convergence, not another phase-9 poll defect, blocks Kentridge evidence.
- [x] Preserve acceptance semantics: do not weaken `HasCompletePublishedNearSurfaceCoverage`, force-generate acceptance content, widen residency, raise budgets, or substitute storage-only evidence.
- [x] Verify overlapping renderer/page-arena/publication/shared-presentation ownership belongs to Agent 1; do not duplicate that work on agent-6.

## Exact-SHA validation / external prerequisite
- [x] Agent-6 exact source `870c6bd0b9fed9005586945a328a9e5a8ed2f1dd`, request `51dd1ded1e1c3778fc8cfcec178b28c7c04dee8e`, run `33962213806`: persistent tests and requested GPU relocation/liveness regression pass; 120-second Kentridge module and 180-second SceneIssue replay remain acceptance-red for strict publication convergence.
- [x] Physical timing boundary from `33962213806`: opening publication consumes roughly the first high-90 seconds; Moordell demand starts ~108s, content ready ~161s, capture still blocked with `missing=89` at 180s.
- [x] Renderer-owner exact run `33986630571` on source `7ceaa0120a4e30c260b1f383e7fc973c3c205309`: compile failure before tests/player because the Rendering EditMode test assembly lacked `VoxelEngine.Rendering.Api`; Agent 1 fixed the owning assembly reference in `b971001b642f7b763aa0d42e585f4784e80cea9e`.
- [x] Renderer-owner replacement exact run `33987770257` on source `2008e51fc070228757ec5c7aa33d69ba50c805ce`: compilation and the 45-second CPU-only VoxelShowcase standalone replay succeed with zero scenario assertion failures, but repository validation repeats three `ShowcaseInputSystemTests` failures. Full-resolution built-player review is also red: a huge featureless gray far-world slab persists through the final frame, so CPU visual acceptance is not `production-quality`.
- [x] `origin/master=ef475182b866eabfe8e1d1a39c82bf7810a03f49` contains the validated shared SmallVoxelShowcase Input System restoration from `3654c13f72ed157c53b340443a766795d772f596`; later history remains unrelated manager/admin work. PR #310 adds Astra built-player screenshot review tooling and does not provide the renderer correction required here.
- [x] Classify renderer-owner exact run `33991474823`: transport `3fc980e8757cff92e891a68b3f3235605eca3cc5`, source `1c2720f54268054d90ac50f1a15999126bcc3c35`, completed product failure. Repository validation repeats the three `ShowcaseInputSystemTests`; the standalone VoxelShowcase build also fails in ILLink because an NUnit composition regression leaked into the player dependency graph.
- [x] Re-fetch renderer correction state: `fixes/agent-1=fc767620a0fe5d0dfee204947d13e7eefaa2a3fa` is a clean continuation based on then-current master; it preserves only far-presentation CPU-diagnosis work/module-local regressions and adopts master-owned Showcase Input System/runtime validation.
- [x] Renderer-owner exact run `33996360570`: transport `8e6aac9fe8845a04a0bdfca2640fc11988e50506`, source `fc767620a0fe5d0dfee204947d13e7eefaa2a3fa`, completed workflow success. Derived module tests/players and the 45-second VoxelShowcase replay pass, but direct full-resolution inspection remains visual-red: the giant gray left slab and other far blockout masses persist; player diagnostics report `gpu[req=0 ... pub=0]`, so this is CPU-only defect evidence, not renderer acceptance.
- [x] Agent 1 isolated the next bounded discriminator: source `da3f5be338c57f5fe99ad4324405422e78c3918e` adds far-frustum geometry-loss regressions; candidate `a164456a9eac5091ec3e5d6c2e03a9de7b675199` preserves normalized frustum cap centres/radii for renderer tessellation. Latest Agent 1 branch `e4e2f9975dc2d3f3d437b5bfe3f853b6f2cf468b` explicitly keeps visual/GPU acceptance open.
- [x] Classify the far-frustum fail-before exactly: `ci-test/fixes/agent-1=6ddc72724c6653538be5c5a9818ebee059726264`, parent/source `da3f5be338c57f5fe99ad4324405422e78c3918e`, run `33999899224` completed failure while the 45-second standalone replay succeeded. Artifact `single-test-33999899224` shows exactly 8 non-quarantined failures, all parameterizations of requested `FarFeatureFrustumGeometryTests.FrustumSilhouetteMatchesCanonicalTaper`; every failure says the canonical taper was replaced by its bounding box (e.g. expected `11.5 +/- 1.25`, got `24.5`). This is valid fail-before evidence, not collateral module/assembly failure. No pass-after request is published yet.
- [x] If the same acceptance symptom remains after two materially different fixes, isolate the new minimal repro/root cause before another production change; do not speculate across the Agent 1 ownership boundary.
- [ ] After Agent 1 completes the pass-after discriminator, clears CPU visual acceptance, validates the renderer/GPU correction, and merges it to `master`, merge then-current `origin/master` into `fixes/agent-6` per `master-sync-required.md` and re-evaluate strict opening/publication convergence.
- [ ] Require repository-derived module validation to pass every affected owned test assembly and all four required agent-6 module-local player validations on the final synchronized exact SHA.
- [ ] Require the 180-second SceneIssue replay to reach strict built-player coverage beyond Moordell and emit settlement/geography/network evidence without runtime exceptions.

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
