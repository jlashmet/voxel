# Plan

## Acceptance authority
`issue.json` is the contract (`captures: []`). Preserve the source-backed Mounting Force graph while delivering physical settlements, terrain-aware hard routes, reusable geography, Rossdam Lake, Southern Ridge/pass, CharacterMotor traversal, durable built-player evidence, and bounded cost. Follow `AGENTS.md`, `SceneIssues/README.md`, and `feature-readme.md`.

## Proven results
- Production physical planning covers 20 hard routes, 6 settlements, 16 generic buildings, deterministic geography, constrained-route solutions, lake/ridge realization, bounded slopes, storage, and feature-aware vertical residency/readiness. Reuse is proven by independent non-Kentridge blocked-water, spatial-reservation, and synthetic generic-blockout fixtures.
- Exact ownership source `d36d96eefe159b4b62bb8e9a275bbdc69744e84b` passed focused ownership, repository-derived module validation, and standalone player in `33562388717`; shipped runtime catalogue contains 480 macro-aware definitions. Pre-plinth replay `33563288872` proved all 16 editor storage shells and real-player Moordell shell/roof storage, but Moordell content-ready required ~175 s.
- The demonstrated throughput cause was the generic fallback building's retained solid full-footprint foundation slab. The selected reusable fix is four bounded perimeter plinth boxes preserving sampled-relief grounding, footprint/material/envelope, support, collision silhouette, and bounds; the independent synthetic regression requires a hollow centre and <50% of the former solid-foundation volume.
- Requests `33635511188` and `33635715141` were infrastructure-only runner-memory failures. Retry 5 `33641059051`, exact source `7e6d30858677f2504763e891289293c9507cfd9f`, cleared admission but repository-derived validation failed first in 15 unrelated renderer/GPU EditMode tests, so the requested process-isolated Kentridge plinth regression never executed.
- Retry 5's supported 180-second standalone replay nevertheless gives partial product evidence: 480 macro definitions; Moordell content-ready ~85 s (~90 s / 51% faster than pre-plinth); load radius 3 with 29 horizontal columns, 31 total resident snapshot, 29 in-radius and zero feature-only vertical extras at Moordell readiness. Across 143 one-second samples after t>=30 s, median FPS is 103.9 and median mean-frame time 9.61 ms, but worst sampled frame is 1172.43 ms. Renderer publication coverage remains false through the replay, with visible unpublished surface holes, so strict named captures correctly do not advance.
- Current master `b18d470f66221c7cb6091249f4683c2d994bffec` now contains the GPU renderer production-restoration merge. The prerequisite has landed, but the coordinator's explicit assignment order requires green exact-SHA gates before merging current master. Do not cherry-pick/copy renderer work or weaken coverage from agent-6.

## Selected fix and evidence under validation
The ownership fix remains scene-local: `KentridgePlayableSlice` builds/combines the intended playable catalogue before constructing `ShowcaseWorld`, so the intended catalogue consumes the one-shot `TopDownWorldLayoutSelection` first. Shared WorldBuilder/macro semantics are unchanged.

The perimeter-plinth throughput fix remains the only selected production change for the current convergence defect. `KentridgeMacroWorldResidencyCostDiagnostic` is validation-only and measures actual resident coordinates through `IRegionReadSource`; it changes no production residency policy.

`experiment-030-throughput-retry5-baseline-renderer-gate.md` records the CI failure classification. `experiment-031-retry5-cost-and-master-renderer-prerequisite.md` records the exact-source cost measurements and the now-landed-but-order-blocked renderer prerequisite.

## Next gates
1. Pre-merge exact-SHA Kentridge validation is blocked: the supported workflow runs stale pre-merge renderer modules before the requested isolated PlayMode regression, while the coordinator order forbids integrating current master until green exact-SHA gates. Record this rather than broadening scope or issuing an identical retry.
2. Retain Retry 5's provisional throughput/residency/FPS evidence, but require actual Fairy/Orc shell/roof storage and readable built-player captures before checking those tasks.
3. Inspect settlement, lake/constrained-route, ridge/pass, network, and CharacterMotor evidence only when strict published coverage succeeds; reject anything below the issue's explicit blockout acceptance bar.
4. Final cost proof still needs multi-target convergence plus CPU/memory/render/far-field telemetry against budgets; current measurements are partial because the evidence sequence stalls at publication coverage.
5. Do not close while required gates are blocked. Once the coordinator-prescribed green gates exist, perform the requested close bookkeeping, merge current master, revalidate affected work as required by the repository workflow, and non-force promote the exact final head.
