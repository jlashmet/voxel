# Plan

## Acceptance authority
`issue.json` is the contract (`captures: []`). Preserve the source-backed Mounting Force graph while delivering physical settlements, terrain-aware hard routes, reusable geography, Rossdam Lake, Southern Ridge/pass, CharacterMotor traversal, durable built-player evidence, and bounded cost. Follow `AGENTS.md`, `SceneIssues/README.md`, and `feature-readme.md`.

## Proven results
- Production physical planning covers 20 hard routes, 6 settlements, 16 generic buildings, deterministic geography, constrained-route solutions, lake/ridge realization, bounded slopes, storage, and feature-aware vertical residency/readiness. Reuse is proven by independent non-Kentridge blocked-water, spatial-reservation, and synthetic generic-blockout fixtures.
- The playable ownership fix remains scene-local: the intended Kentridge catalogue consumes the one-shot macro selection before `ShowcaseWorld` constructs its temporary bootstrap catalogue. Exact source `d36d96e...` passed focused ownership/module/player validation; runtime catalogue contains 480 macro-aware definitions.
- The demonstrated generic-blockout throughput defect was a retained solid full-footprint foundation slab. Four bounded perimeter plinth boxes preserve grounding/footprint/material/support/collision/bounds. The independent 100x80x60 fixture reduces foundation box volume 82,432 -> 29,440 (64.3%), timber body 416,000 -> 71,552 (82.8%), and combined foundation+timber work 79.7%; roof work is unchanged.
- Master renderer restoration was explicitly synchronized into this branch at merge `5185b5acddd3d35ec29730ea73d30ae796a705bf` from master `f5593cc1236ba3963fc5713a11df35292628e97d`.
- Exact-SHA run `33800775569` on feature source `5185b5acddd3d35ec29730ea73d30ae796a705bf` is green: repository-derived module validation passed, the requested perimeter-foundation regression passed, and the standalone player process completed. This closes the previously blocked plinth behavioral gate and supports acceptance (9).
- That same player replay still did not advance strict visual evidence beyond Moordell. It proves `definitions=480`, Moordell content-ready around 85 s, and radius-3 residency with 29 in-radius columns and zero feature-only vertical extras, but elevated survey coverage remained false (`inner=153.6m`, `streamed=153.6m`, observed `residentGround=110.2m`).
- Because the same strict publication symptom survived two materially different executable states (pre-restoration Retry 5 and post-restoration `5185...`), experiment 037 isolates the root cause before another fix: the discrete horizontal admission rule `dx²+dz² <= R²` does not guarantee a continuous metric disk of `R * RegionMetres` around every within-cell demand point. The conservative guaranteed disk is `(R - 1) * RegionMetres`; for R=3 and 51.2m regions this is 102.4m.
- `KentridgeStreamingCoveragePolicyTests` independently enumerates excluded lattice cells and measures point-to-square distance instead of restating the production formula. The selected fix remains Kentridge-only composition: keep streaming radius 3, far-field streamed radius, generation/device budgets, and strict renderer coverage unchanged; narrow only the near-surface completeness ring to the radius residency can guarantee.
- Partial cost evidence from Retry 5 remains valid context: median FPS 103.9 after 30 s, median mean-frame 9.61 ms, worker-p95 median 3.018 ms, admission-total median 1.1605 ms, and ~26.99 MiB cumulative render-arena uploads over 114 calls. No process RSS/working-set, managed/native heap, or GPU-memory footprint was emitted, so acceptance (11) remains open.

## Selected fix under validation
`KentridgeStreamingCoveragePolicy.GuaranteedNearSurfaceRadiusMetres(loadRadiusRegions, RegionMetres)` defines the scene-composition contract. A Kentridge-only post-scene-load installer applies that configured radius after the playable slice establishes its normal renderer world. No shared renderer API/policy, streaming lattice, load radius, scheduler, generation budget, or device budget is changed.

Rejected alternatives are recorded in experiment 037: do not weaken `HasCompletePublishedNearSurfaceCoverage`, widen load radius, hardcode the observed 110.2m radius, modify shared renderer publication semantics, or issue an identical retry.

Experiments 030-036 retain prior CI/cost/equivalence/acceptance/blocker evidence. `experiment-037-streaming-coverage-radius-root-cause.md` is the current two-fix root-cause isolate and selected-fix rationale.

## Next gates
1. Run the current exact feature SHA through `ci-test/fixes/agent-6`, targeting the independent streaming-coverage radius regression with the required 180-second SceneIssue replay. Do not replace the run while queued/running.
2. Require repository-derived module validation plus strict built-player coverage progression. The replay must advance beyond Moordell and produce readable Moordell/Rossdam/Fairy/Orc, lake/constrained-route, ridge/pass, network, and CharacterMotor evidence before visual acceptance is checked.
3. Quantify final multi-target residency/convergence, CPU/render/far-field behavior, and process/managed/native/GPU memory footprint against budgets; prior measurements remain partial.
4. If the same coverage symptom survives this root-cause-backed fix, isolate the new failure boundary before another fix.
5. Do not close until every task/acceptance checkbox is validated. After green exact-SHA gates, move open -> closed, set `status=fixed` and `resolvedUtc`, merge then-current `origin/master`, revalidate affected work if required, and non-force push the exact final feature head to `origin/master`.
