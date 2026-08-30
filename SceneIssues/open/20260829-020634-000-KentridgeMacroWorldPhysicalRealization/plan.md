# Plan

## Observed behavior / acceptance
`captures` is empty, so `issue.json` is the acceptance contract. The source-backed Kentridge macro graph must physically realize every settlement, continuous terrain-aware hard routes, reusable regional geography, a substantial lake/ridge with semantic route response, real CharacterMotor traversal, and world-scale streaming cost within existing budgets. Closure also requires exact built-player visual evidence. `SceneIssues/feature-readme.md` is absent; `SceneIssues/README.md` is the workflow authority.

## Material results / hypotheses
1. Macro generation is missing — rejected. Production acceptance repeatedly yields 20 hard routes, 16 generic buildings, 5 constrained routes, and max road rise 2 voxels.
2. Persisted settlement payload never reaches rendering — rejected. Two-stage terrain/feature publication is real and the scoped readiness regression proves settlement shell publication.
3. Broad residency readiness is acceptable — rejected. It delayed opening ~40 s and was too expensive.
4. Serially pinning each building centre is efficient — rejected by run `33290154012`; only Moordell column 0 completed in 60 s.
5. `Time.timeScale=12` shortens opening dialogue — rejected because dialogue uses realtime.
6. Validation-only dialogue dismissal + one stable survey demand fixes final evidence — partly confirmed by run `33292088730` / artifact `9726298626`. Opening reaches gameplay around t=15, real CharacterMotor traversal succeeds, all four Moordell content columns settle, and a Moordell capture is produced; Rossdam then exposes real feature-streaming cost.
7. The Rossdam stall is evidence-only — rejected. The prior carved lake occupied roughly 42M primitive bounding cells. Bounding the first pass materially improves replay progress without changing production budgets.
8. Run `33292088730` focused failure is a product assertion — rejected. It dies in native Mono/Burst `CacheBuilder.ILHasher` SIGSEGV with no NUnit assertion/XML; the later built-player step succeeds.
9. An elevated survey must move CharacterMotor — rejected. `KentridgePlayableSlice` streams from `_motor.EyePosition`; camera-only elevation preserves production streaming demand without adding a vertical residency layer.
10. Exact run `33292881845` / artifact `9726538126`, source `86117754d2d4d8bc23a520075ce96b8adea5aa79`, is product-red. NUnit reaches the requested test and fails because deterministic region variation resolves Rossdam Lake to only `888 dm` width versus the >=900 dm macro-landmark floor. The matching Z extent is likewise reduced by deterministic variation and must be guarded, not papered over by weakening the assertion.
11. That same built-player artifact rejects the diagonal settlement survey framing. Moordell road-arrival visibly proves generated blockout buildings exist, but Fairy Village and Orc Village surveys show roads/countryside without four readable structures. Readiness logs are not closure evidence by themselves.
12. A 75 s replay can solve the remaining schedule — rejected by CI admission run `33293402602`. The repository workflow requires `replay_seconds` to be an integer from 20 through 60, so no Unity test/player ran for that request. The complete required evidence must fit the supported 60 s window.
13. The 60 s trace is close: Orc captures near t57 and Southern Ridge becomes content-ready near t59.4. Reusing already-streamed geography rather than increasing budgets is the remaining discriminator. Rossdam settlement currently runs before the lake target, so the expensive lake publication is paid while waiting on Rossdam and then the validation demand moves back to the lake. Visiting the lake first should publish that shared geography once and let the subsequent Rossdam target concentrate on settlement content.

## Selected remediation
- Keep generation budgets, streaming radius, CharacterMotor, macro topology, hard-route semantics, runtime rendering, and the repository-supported 60 s replay cap unchanged.
- Preserve the deterministic Rossdam guard margin: nominal values resolve this fixed seed to exactly 90 m x 45 m x 2.4 m while the aggregate carved/fill primitive scan stays below the 17M-cell regression ceiling.
- Preserve the validation-only near-overhead generic-settlement camera correction. Streaming demand remains grounded at semantic focus; only the evidence camera moves, and the separate player-height Moordell road-arrival view remains.
- Reorder only the dormant validation target array from `Moordell -> Rossdam -> lake` to `Moordell -> lake -> Rossdam`, before the first target advances. This uses ordinary `ShowcaseWorld.StepStreaming` to pre-publish Rossdam lake content and avoids paying the same shared geography after changing demand. Do not skip or mark any evidence target complete synthetically.
- Keep Fairy, Orc, Southern Ridge/pass, and network targets intact; all still require real readiness + renderer coverage before capture.

## Blast radius / cost
`fixes/agent-6` includes master `61d03336390ed9079498b183217cbf0ecf0abcd2` via clean merge `6a49e63cc46fd651bbb815ac98f824a087a09c33`; those master commits were disjoint from this assignment. Product change remains limited to high-level Kentridge macro intent; evidence scheduling/camera changes remain dormant outside `validationProfile=kentridge-macro-world`. No device budget, load radius, renderer, CharacterMotor, ecology, unrelated SceneIssue, or feature-branch `.github/test-request.json` change. Run `33292881845` player process reports `elapsed=70s peak=5233MB systemFree=28711MB swapGrowth=0MB`; after startup its one-second FPS windows are generally >60 and often >100.

## Remaining gate
Record the invalid 75 s admission in tasks/CI history, implement validation-only Rossdam lake-before-settlement target ordering, re-check current master/diff, and advance the existing `ci-test/fixes/agent-6` transport with `replay_seconds=60`. Inspect focused XML, full-resolution Moordell/Rossdam/Fairy/Orc evidence, lake detour, ridge/pass, network overview, real CharacterMotor traversal, exceptions, and final cost telemetry. Promote only when every task/acceptance checkbox is actually proven.