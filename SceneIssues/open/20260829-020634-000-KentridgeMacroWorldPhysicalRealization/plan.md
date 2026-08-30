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
10. Final exact run `33292881845` / artifact `9726538126`, source `86117754d2d4d8bc23a520075ce96b8adea5aa79`, is product-red. NUnit reaches the requested test and fails because deterministic region variation resolves Rossdam Lake to only `888 dm` width versus the >=900 dm macro-landmark floor. The matching Z extent is likewise reduced by deterministic variation and must be guarded, not papered over by weakening the assertion.
11. That same built-player artifact rejects the current settlement survey framing. Moordell road-arrival visibly proves generated blockout buildings exist, but diagonal 36 m surveys are terrain-occluded; Fairy Village and Orc Village captures show roads/countryside without four readable structures. Readiness logs are therefore not closure evidence by themselves.
12. The 60 s validation capture window is also too short for the complete evidence sequence: Orc captures around t57, Southern Ridge becomes content-ready around t59.4, and ridge/network captures do not occur. Extending the validation replay window changes no runtime device/streaming budget and is preferable to deleting required evidence targets.

## Selected remediation
- Keep generation budgets, streaming radius, CharacterMotor, macro topology, hard-route semantics, and runtime rendering unchanged.
- Author deterministic guard margin for Rossdam Lake so this seed resolves to at least 90 m x 45 m while preserving variation. With current stable hash deltas (`extentX=-6 dm`, `extentZ=-3 dm`, `elevation=-1 dm`), use nominal half extents/depth that resolve to 450/225/24 dm. Keep the 17M aggregate water primitive scan regression; do not grow the physical lake past that cost ceiling.
- Replace diagonal settlement survey framing with a near-overhead camera above the settlement focus. Streaming demand remains grounded at semantic focus; only the validation camera moves, so terrain cannot hide the four blockout plots.
- Keep the player-height Moordell road-arrival capture as separate proof of a generated road entering a settlement.
- Extend only the targeted validation replay request to 75 seconds so Southern Ridge/pass and macro-network evidence can be captured after all settlement/geography gates. Production simulation/streaming budgets remain unchanged.

## Blast radius / cost
`fixes/agent-6` includes master `61d03336390ed9079498b183217cbf0ecf0abcd2` via clean merge `6a49e63cc46fd651bbb815ac98f824a087a09c33`; those master commits were disjoint from this assignment. Product change remains limited to high-level Kentridge macro intent; evidence changes remain dormant outside `validationProfile=kentridge-macro-world`. No device budget, load radius, renderer, CharacterMotor, ecology, unrelated SceneIssue, or feature-branch `.github/test-request.json` change. Run `33292881845` player process reports `elapsed=70s peak=5233MB systemFree=28711MB swapGrowth=0MB`; after startup its one-second FPS windows are generally >60 and often >100 while the evidence sequence advances.

## Remaining gate
Implement deterministic resolved-lake guard margin and non-occluded settlement framing, then refresh `tasks.md`. Re-check current master/diff and advance the existing `ci-test/fixes/agent-6` transport to one corrected exact source (the prior exact request was product-red, so it cannot be rerun as infrastructure). Inspect focused XML, full-resolution Moordell/Rossdam/Fairy/Orc evidence, lake detour, ridge/pass, network overview, real CharacterMotor traversal, exceptions, and final cost telemetry. Promote only when every task/acceptance checkbox is actually proven.