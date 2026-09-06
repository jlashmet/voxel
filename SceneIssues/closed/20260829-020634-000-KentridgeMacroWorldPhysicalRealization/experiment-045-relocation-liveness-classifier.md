# Experiment 045 — relocation liveness classifier after footprint-local invalidation

## Question
Did the footprint-local mirror correction still leave all relocated GPU workers admission-starved under unrelated change churn, or did the focused regression misclassify healthy same-frame progress?

## Exact evidence
Targeted run `33905495634` executed request `eabedcd20e7a5b84ea25026c705f4d58b03cea4c` against feature source `cafd0f934a3bf376dc10cf33196d90a821b40862`. Persistent repository-derived tests passed; the automatically requested PlayMode test `GpuSurfaceMirrorRelocationRequestedValidationTests.DistantUnrelatedChangeChurnExecutesProductionGpuLivenessRegression` failed after its 20-second saturated-admission timer.

The failure payload itself reports `ready=65535`, `pending=1`, `demand=9`, `active=0`, `mixedResident=6061/93312`, **and `gpuCompleted=461`** after the 384m relocation with 5,146 unrelated changes. Four useful completions are the regression's throughput acceptance threshold, so 461 completions directly falsify the assertion that no relocated request crossed admission.

## Root cause
The test sampled `GpuSurfaceMirrorCoordinator.ActiveExtractions` only after `yield` + `camera.Render()`. A request can admit, dispatch, complete, and decrement the active counter inside that rendered frame, leaving `active=0` at the sampling point despite real progress. The held one-block control demand deliberately keeps `pending=1` and `demand>=9`, so the old classifier could accumulate 20 seconds of false "saturated admission" after hundreds of successful GPU completions. Its break condition also incorrectly required demand to fall below the threshold even though the control demand is intentionally held for the discriminator.

## Selected correction
Commit `2fb10483fe3584dc73a2326a9cd806a7589d2ff0` changes only the PlayMode regression. A saturated no-progress interval now requires: recovery backlog, saturated demand, `active==0`, **and no increase in `GpuCompletedSolidBuilds` since the prior observation**. Any active extraction or completion progress resets the stall timer. The test exits once recovery backlog has been observed and at least four post-relocation GPU builds have completed with visible geometry, which is the behavior it is intended to prove.

No production renderer, Kentridge policy, radius, budget, coverage threshold, or concurrency changed in this experiment.

## Next gate
Run the same requested unrelated-change regression on the exact new feature SHA through `ci-test/fixes/agent-6`, with repository-derived module validation and the required 180-second SceneIssue replay. If the corrected test still sees a true 20-second interval with neither active extraction nor completion progress, inspect that new telemetry before any further production change.
