# Experiment 046 — sustain unrelated-change discriminator

## Question
Did exact run `33912155787` fail because footprint-local mirror invalidation still starves relocated GPU work, or because the corrected test ended before its own unrelated-change workload was sustained?

## Exact result
- Feature source: `b45f6e36738c051250747253df9d75f6ad40c1fb`
- CI transport: `f235a97fb05dbfceeae248b48fe411910167dd01`
- Run: `33912155787`
- Persistent repository-derived EditMode/PlayMode module phases: passed.
- Requested test: `GpuSurfaceMirrorRelocationRequestedValidationTests.DistantUnrelatedChangeChurnExecutesProductionGpuLivenessRegression` failed after 39.1s only because `injectedDistantChanges=3`, below the test's required minimum of 8.

The harness exits the observation loop once mirror recovery has produced four post-relocation GPU completions and visible geometry. On this source that success condition was reached after only three control-block re-publication cycles, so the loop exited and the following `>=8` discriminator precondition necessarily failed. This is not evidence of renderer starvation; it is contradictory test orchestration.

The same run's standalone 180s Kentridge replay had zero harness assertion failures but remained closure-red: at the end `coverage=False`, `missingVisible=252`, `demand=8`, `flight=8`, and the mirror recovery backlog was still draining. Treat that as an independent integration/convergence boundary, not as the focused discriminator result.

## Selected correction
Test-only commit `77d5314a39857b55e87ddb299807b5e323af5e28` requires the full eight unrelated changes before the observation loop may take its successful early exit. It leaves the 20s no-progress failure threshold, four-useful-completions requirement, production renderer, budgets, strict coverage, and concurrency unchanged.

## Next proof
Exact-SHA rerun the same requested regression with repository-derived module validation and the required 180-second SceneIssue replay. If the focused test passes while Kentridge still ends with strict coverage incomplete, isolate the integration convergence cost separately before any further production change.
