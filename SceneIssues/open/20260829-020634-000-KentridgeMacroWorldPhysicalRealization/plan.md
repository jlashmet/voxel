# Plan

## Observed behavior / acceptance
`captures` is empty, so `issue.json` is the acceptance contract. The source-backed Kentridge macro graph must physically realize every settlement, continuous terrain-aware hard routes, reusable regional geography, a substantial lake/ridge with semantic route response, real CharacterMotor traversal, and world-scale streaming cost within existing budgets. Closure also requires exact built-player visual evidence. `SceneIssues/feature-readme.md` is absent; `SceneIssues/README.md` is the workflow authority.

## Material results / hypotheses
1. Macro generation is missing — rejected. Production acceptance repeatedly yields 20 hard routes, 16 generic buildings, 5 constrained routes, and max road rise 2 voxels.
2. Persisted settlement payload never reaches rendering — rejected. Two-stage terrain/feature publication is real and the scoped readiness regression proves settlement shell publication.
3. Broad residency readiness is acceptable — rejected. It delayed opening ~40 s and was too expensive.
4. Serially pinning each building centre is efficient — rejected by run `33290154012`; only Moordell column 0 completed in 60 s.
5. `Time.timeScale=12` shortens opening dialogue — rejected because dialogue uses realtime.
6. Validation-only dialogue dismissal + one stable survey demand fixes final evidence — partly confirmed by run `33292088730` / artifact `9726298626` (source `652f531c...`). Opening reaches gameplay around t=15, real CharacterMotor traversal succeeds, all four Moordell content columns settle, and `macro-moordell.png` is captured. Rossdam then stalls for ~35 s while renderer work itself drains.
7. The Rossdam stall is evidence-only — rejected. Rossdam shares residency with the carved `1040 x 540 x 47`-voxel lake; carve + fill bounds were roughly 42M cells. This is a real scale cost problem, not a reason to weaken readiness.
8. Run `33292088730` focused failure is a product assertion — rejected. It dies in native Mono/Burst `CacheBuilder.ILHasher` SIGSEGV with no NUnit assertion/XML; the later built-player step succeeds.
9. An elevated survey must move CharacterMotor — rejected. `KentridgePlayableSlice` streams from `_motor.EyePosition`; camera-only elevation preserves production streaming demand without adding a vertical residency layer.

## Selected fix
- Keep generation budgets, streaming radius, CharacterMotor, macro topology, replay duration, and route semantics unchanged.
- Bound Rossdam Lake to 90 m x 45 m x 2.4 m while retaining real basin carving, water fill, shoreline routing, and `GoAround` constraints. Aggregate water primitive bounds fall to ~16.6M cells (~39% of prior work), with a 17M-cell regression cap.
- Keep motor/streaming demand grounded at semantic focus while required content settles; independently frame elevated evidence camera in `LateUpdate`.
- Capture player-height Moordell road arrival after settlement publication.

## Blast radius / cost
`fixes/agent-6` now includes current master `61d03336390ed9079498b183217cbf0ecf0abcd2` via clean merge `6a49e63cc46fd651bbb815ac98f824a087a09c33`; the 17 intervening master commits were disjoint from this assignment. Product change is limited to shared macro authoring plus the high-level Rossdam intent; evidence code is dormant outside `validationProfile=kentridge-macro-world`. No device budget, load radius, renderer, CharacterMotor, ecology, unrelated SceneIssue, or feature-branch `.github/test-request.json` change.

## Remaining gate
The bounded-lake cost regression, grounded-demand evidence, master refresh, and exact diff review are complete. Issue one final exact-SHA targeted request from the resulting documentation head on the existing `ci-test/fixes/agent-6` transport, inspect the focused result and every full-resolution built-player artifact, record cost telemetry, then promote only if all four generic settlements, settlement-road view, network, lake, ridge/pass, constrained route, CharacterMotor traversal, and runtime cleanliness are proven.