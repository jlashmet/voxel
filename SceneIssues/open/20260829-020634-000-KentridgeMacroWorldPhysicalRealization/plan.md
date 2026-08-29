# Plan

## Observed gap / acceptance
`captures` is empty, so the feature note is the repro contract. Closure requires the authoritative Kentridge macro graph to drive deterministic physical settlements, continuous terrain-aware hard routes, reusable geographic constraints, substantial lake/ridge geography, real CharacterMotor traversal, closure-quality built-player evidence, and measured cost. `SceneIssues/feature-readme.md` is absent; canonical `SceneIssues/README.md` governs.

## Material results / hypotheses
1. **Scale up legacy markers/roads.** Rejected; it cannot satisfy reusable geography, settlement realization, or blocked-route semantics. The reusable physical-plan layer is implemented.
2. **Weaken geography to clear blocked routes.** Rejected. Rossdam/Bandit and Southern Ridge/Orc conflicts use explicit semantic route solutions with focused regressions.
3. **A green workflow/file count proves visual acceptance.** Rejected repeatedly. Runs `33259572439` through `33263409994` exposed incomplete evidence, harmful prewarm, occlusion, missing scheduling, and false-positive near-surface coverage.
4. **Remote blocking prewarm fixes evidence.** Rejected by run `33260866388`; same-camera prewarm harmed presentation and was removed.
5. **The planner failed to create generic settlements.** Rejected at plan level by the focused production regression, which creates four deterministic blockout plots per generic settlement.
6. **Four stable `HasCompletePublishedNearSurfaceCoverage()` frames plus closer cameras are sufficient.** Rejected by final run `33265086481` / artifact `9718439671`: focused PlayMode and built-player harness are green, all eight captures emit with `stableFrames=4`, but full-resolution images still do not prove the physical feature. Moordell shows only one recess, Rossdam is dominated by a flat blue/cut surface with no readable town, Fairy/Orc show roads but no four-building blockouts, and lake/ridge views do not clearly prove the authored basin/barrier. See experiment 010.
7. **The production catalogue only contains settlement metadata/foundations.** Rejected by source trace. `TopDownWorldPhysicalVoxelCatalogue` emits grounded foundations, filled timber wall volumes and roof/gable primitives for each authored generic building, and `KentridgeCombinedVoxelCatalogue` includes that physical catalogue. The structure pass is composed after macro roads. A final `VoxelData` occupancy assertion is still required as a behavioral guard rather than relying on this source trace alone.
8. **The evidence readiness gate proves the photographed target is resident.** Rejected. `KentridgePlayableSlice.UpdateDynamicResidency` follows the `CharacterMotor` eye position. `KentridgeMacroWorldEvidenceDriver.PinToTarget` currently puts that motor at the *survey camera* position, and `IsTargetReady` probes storage around that same camera position. Thus a remote frame can become "ready" while the actual settlement/lake/ridge focus lies outside the residency/readiness center. This matches the repeated road-visible / feature-unreadable screenshots without requiring another production geometry rewrite.

Resumed 2026-08-29 with `fixes/agent-6` at `c8c3dabc35aced83df7112971572ee4d75207406` and `origin/master` at `bb041b56095db10a9580f059f44f482d9eb35162`. Production world semantics remain frozen while the validation-path defect is repaired.

## Next implementation discriminator
1. Add a focused production-path regression that selects the real macro layout, builds `KentridgeCombinedVoxelCatalogue`, resolves one real generic settlement building, executes the catalogue at sampled building coordinates, and proves above-ground shell/roof occupancy in final `VoxelData` rather than only definition presence.
2. Decouple evidence residency from survey-camera transform: keep the motor/streaming center at the target feature, while positioning/aiming the scene camera independently at the existing survey offset.
3. Make target readiness probe storage around the actual target feature. Retain current rendering-completeness diagnostics so a capture still cannot proceed on fallback/incomplete published surface data.
4. Re-run the existing evidence targets unchanged first. Only adjust framing if production occupancy is proven, target-centered residency is active, and full-resolution evidence is still ambiguous.

## Blast radius / cost for the minimal fix
- Evidence-driver changes execute only in the validation/evidence path; production world generation, semantic routes, catalogue precedence, streaming radius, gameplay motor semantics, and renderer budgets remain unchanged.
- Target-centered residency uses the existing `ResidencyManager` radius and normal `KentridgePlayableSlice` streaming path; it does not eagerly build remote worlds or increase resident radius.
- The new regression adds bounded catalogue sampling for a small number of coordinates in one authored building and reuses the existing final targeted PlayMode test; no additional CI transport/test workflow is introduced.
- Re-check branch diff and built-player telemetry after the fix before closure.

## Remaining gate
After the proven minimal fix, refresh master, self-review blast radius/cost, and run exact-SHA focused PlayMode plus the built `KentridgePlayableSlice`. Full-resolution evidence must visibly show four readable blockouts in each generic settlement, continuous road/motor traversal, substantial clean Rossdam basin/shoreline with constrained route, readable ridge/pass response, and a connected route view without large holes. Only then complete pending metadata, move this assignment `open -> pending -> closed`, set `fixed`/`resolvedUtc`, refresh master again, and non-force promote the exact feature head.
