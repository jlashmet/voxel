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

Current tested feature source is `e8b0b5351e8ac8a9ce019caa1a3ddbe82457da3f`, with current master `ff781ed26b1d9182fa8cd76e2d2da08abfa3765c` already an ancestor. Production semantics remain frozen until source/runtime evidence proves the visual defect belongs to generation rather than evidence framing/streaming.

## Next discriminator
Trace generic settlement voxel realization end-to-end: prove whether every planned building emits visible above-ground shell/roof voxels at the sampled terrain height, whether later terrain/road composition overwrites them, and whether those features are in the resident catalogue used by the real player. Compare that with the exact camera/focus coordinates from run `33265086481`. If generation is correct, fix only evidence targeting/readiness using production geometry. If buildings are foundation-only, terrain-swallowed, or overwritten, add a production-path regression for visible shell voxels and fix the reusable catalogue rather than hiding the defect with camera changes.

## Remaining gate
After the proven minimal fix, refresh master, self-review blast radius/cost, and run exact-SHA focused PlayMode plus the built `KentridgePlayableSlice`. Full-resolution evidence must visibly show four readable blockouts in each generic settlement, continuous road/motor traversal, substantial clean Rossdam basin/shoreline with constrained route, readable ridge/pass response, and a connected route view without large holes. Only then complete pending metadata, move this assignment `open -> pending -> closed`, set `fixed`/`resolvedUtc`, refresh master again, and non-force promote the exact feature head.
