# Plan

## Observed gap / acceptance
`captures` is empty, so the feature note is the repro contract. Closure requires the authoritative Kentridge macro graph to drive deterministic physical settlements, continuous terrain-aware hard routes, reusable geographic constraints, substantial lake/ridge geography, real CharacterMotor traversal, closure-quality built-player evidence, and measured cost. `SceneIssues/feature-readme.md` is absent; canonical `SceneIssues/README.md` governs.

## Material results / hypotheses
1. **Scale up legacy markers/roads.** Rejected; it cannot satisfy reusable geography, settlement realization, or blocked-route semantics. The reusable physical-plan layer is implemented.
2. **Weaken geography to clear blocked routes.** Rejected. Rossdam/Bandit and Southern Ridge/Orc conflicts use explicit semantic route solutions with focused regressions.
3. **A green workflow/file count proves visual acceptance.** Rejected repeatedly. Runs `33259572439` through `33263409994` exposed incomplete evidence, harmful prewarm, occlusion, missing scheduling, and false-positive near-surface coverage.
4. **Remote blocking prewarm fixes evidence.** Rejected by run `33260866388`; same-camera prewarm harmed presentation and was removed.
5. **The planner failed to create generic settlements.** Rejected at plan level: each generic settlement receives deterministic >=4-building blockouts.
6. **Four stable `HasCompletePublishedNearSurfaceCoverage()` frames plus closer cameras are sufficient.** Rejected by run `33265086481` / artifact `9718439671`: focused PlayMode and built-player harness are green and all eight captures emit, but full-resolution images still do not prove four readable generic buildings or the intended lake/ridge geography.
7. **The production catalogue only contains settlement metadata/foundations.** Rejected by source trace. `TopDownWorldPhysicalVoxelCatalogue` emits grounded foundations, filled timber volumes and gable roof primitives for each generic building, and `KentridgeCombinedVoxelCatalogue` contains that catalogue.
8. **Camera-centered evidence streaming leaves the photographed focus nonresident.** Rejected after inspecting current production settings. `KentridgePlayableSlice` uses a radius of three 51.2 m regions (about 153.6 m), while every current camera-to-focus offset is under ~54 m. The target focus is therefore inside normal residency; no streaming-radius, motor, or residency change is justified.
9. **Renderer coverage proves the intended semantic target exists in final voxel storage.** Not proven. The current evidence gate checks only current published near-surface completeness. It does not discriminate a fully published road/terrain frame from a frame whose intended settlement/lake/ridge material is absent or unreadable. There is no clean authoritative world-read capability exposed by the playable-slice validation surface, so do not add reflection or broaden production APIs solely for evidence.

Resumed 2026-08-29 from `fixes/agent-6`; production world semantics and streaming settings remain frozen while the final-storage discriminator is added.

## Current implementation discriminator
1. Run the existing full macro acceptance from one final targeted test.
2. In that same target, select the real macro layout, build `KentridgeCombinedVoxelCatalogue`, resolve a real generic settlement building, rasterize the true combined catalogue into current block-granular `RegionTable`/`BrickPool` storage, and read deterministic final voxels from the filled timber volume and gable roof.
3. If final storage lacks either material, fix that production path before visual work. If storage proves both, keep the existing evidence cameras unchanged for the first rebuilt-player run and use the screenshots to discriminate rendering/presentation from generation.
4. Only adjust evidence framing/presentation after stored production geometry is proven and the unchanged rebuilt-player captures remain ambiguous.

## Blast radius / cost
- The added regression is test-only. It uses the production combined catalogue and current storage/rasterizer APIs, but does not alter world generation, catalogue precedence, renderer budgets, CharacterMotor, residency radius, or gameplay streaming.
- Sampling is bounded to one authored generic building and one or at most two 51.2 m regions. The temporary `BrickPool` is fixed at 8192 slots and disposed synchronously.
- The final targeted test invokes the existing full acceptance first, so determinism/routes/geography/water remain in the same exact-SHA gate; no extra CI transport is introduced.
- Re-check branch diff, exact test telemetry, built-player CPU/GPU/frame/memory/streaming telemetry, and screenshot evidence before closure.

## Remaining gate
Refresh master and self-review the branch, then make the one final exact-SHA targeted-CI request on `ci-test/fixes/agent-6` for the storage acceptance target plus the existing built `KentridgePlayableSlice` evidence harness. Full-resolution evidence must visibly show four readable blockouts in each generic settlement, continuous road/motor traversal, a substantial clean Rossdam basin/shoreline with constrained route, readable ridge/pass response, and a connected route view without large holes. Only after every task/acceptance checkbox is proven should metadata be completed, the assignment move `open -> pending -> closed`, `status=fixed`/`resolvedUtc` be set, current `origin/master` be merged, and the exact feature head be non-force promoted.
