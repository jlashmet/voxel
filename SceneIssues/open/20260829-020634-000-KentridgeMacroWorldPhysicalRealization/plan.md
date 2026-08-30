# Plan

## Observed behavior / acceptance
`captures` is empty, so `issue.json` is the acceptance contract. The source-backed Kentridge macro graph must physically realize every settlement, continuous terrain-aware hard routes, reusable regional geography, a substantial lake/ridge with semantic route response, real CharacterMotor traversal, and world-scale streaming cost within existing budgets. Closure also requires exact built-player visual evidence. `SceneIssues/feature-readme.md` is absent; `SceneIssues/README.md` is the workflow authority.

## Material results / hypotheses
1. Macro generation is missing — rejected. Production acceptance repeatedly yields 20 hard routes, 16 generic buildings, 5 constrained routes, and max road rise 2 voxels.
2. Persisted settlement payload never reaches rendering — rejected. Two-stage terrain/feature publication is real and the scoped readiness regression proves settlement shell publication.
3. Broad residency readiness is acceptable — rejected. It delayed opening ~40 s and was too expensive.
4. Serially pinning each building centre is efficient — rejected by run `33290154012`; only Moordell column 0 completed in 60 s.
5. `Time.timeScale=12` shortens opening dialogue — rejected because dialogue uses realtime.
6. Validation-only dialogue dismissal + one stable survey demand fixes final evidence — partly confirmed by run `33292088730` / artifact `9726298626` (source `652f531c...`). Opening reaches gameplay around t=15, real CharacterMotor traversal succeeds, all four Moordell content columns settle, and `macro-moordell.png` is captured. Rossdam then stalls for ~35 s and never reports all content columns settled even though renderer work repeatedly reaches `jobs=0`, `missingVisible=0`.
7. The Rossdam stall is evidence-only — rejected. The same demand visibly spends the replay progressively publishing lake/settlement content. Rossdam shares residency with the carved `1040 x 540 x 47`-voxel lake feature; its carve + water rounded-box bounds are roughly 42M voxel cells before clipping/rounding. This is a real streaming-cost problem against the assignment’s scale requirement, not a reason to weaken readiness.
8. The focused test failure in run `33292088730` is a managed product assertion — rejected. `single.log` has no NUnit XML/assertion result and dies in native Mono/Burst `Burst.Compiler.IL.Hashing.CacheBuilder.ILHasher` with SIGSEGV while the later built-player step succeeds.
9. An elevated survey must move the CharacterMotor — rejected. Production `KentridgePlayableSlice` streams from `_motor.EyePosition`; raising the motor 36 m can put it in a different vertical region layer and queue that extra layer across the residency disc. Evidence framing can instead move only the camera transform in `LateUpdate` while the motor remains on ground-level semantic focus.

## Selected remediation
- Keep normal generation budgets, streaming radius, CharacterMotor, macro topology, replay duration, and all hard-route semantics unchanged.
- Bound the first-pass Rossdam lake to the smallest still-substantial contract size: 90 m x 45 m and 2.4 m authored depth (the production acceptance floor), retaining real basin carving, non-solid water fill, shoreline routing, and semantic `GoAround` constraints. This reduces the two rounded-box bounding workloads from roughly 42M to roughly 16.6M voxel cells (~39%) without weakening engine budgets.
- For target evidence, keep the real motor/streaming demand grounded at the semantic focus while all required content columns settle. Independently frame the elevated survey camera in `LateUpdate`; this avoids serial demand churn and avoids an artificial camera-height residency layer.
- After Moordell is ready, capture a player-height road-arrival view from the already traversed production route aimed into the loaded settlement so acceptance includes a road visibly entering a settlement, not only an isolated road and elevated survey.
- Preserve exact physical/storage/readiness regressions and add/retain a bounded-lake cost assertion so the first-pass water feature cannot silently return to the ~42M-cell scan envelope.

## Blast radius / cost
Current `fixes/agent-6` includes master `0901be5a...` and only this Kentridge feature/tests/docs beyond it. Product change is limited to the high-level Rossdam region intent; evidence change remains dormant outside `validationProfile=kentridge-macro-world`. No device budget, load radius, terrain sampler, renderer, CharacterMotor, ecology, unrelated SceneIssue, or `.github/test-request.json` feature-branch change.

## Remaining gate
The bounded-lake and grounded-demand evidence changes are implemented and the failed run is recorded. Add the bounded-water cost regression, refresh from current master, advance the existing final CI transport to the new exact feature SHA (do not replace queued work), and inspect focused results plus every full-resolution built-player artifact. Close only when all four generic settlements, player-height settlement road evidence, road/network survey, lake, ridge/pass, constrained route, CharacterMotor traversal, clean runtime, and measured cost are closure-quality.