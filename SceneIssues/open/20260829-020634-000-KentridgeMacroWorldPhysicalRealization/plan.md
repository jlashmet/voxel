# Plan

## Acceptance authority
`issue.json` is the contract (`captures: []`). Preserve the source-backed Mounting Force graph while delivering physical settlements, terrain-aware hard routes, reusable geography, Rossdam Lake, Southern Ridge/pass, CharacterMotor traversal, durable built-player evidence, and bounded cost. Follow `AGENTS.md`, `SceneIssues/README.md`, and `feature-readme.md`.

## Proven results
- Production physical planning covers 20 hard routes, 6 settlements, 16 generic buildings, deterministic geography, constrained-route solutions, lake/ridge realization, bounded slopes, storage, and feature-aware vertical residency/readiness.
- Reuse is proven by independent non-Kentridge blocked-water, spatial-reservation, and synthetic generic-blockout fixtures.
- Exact ownership source `d36d96eefe159b4b62bb8e9a275bbdc69744e84b` passed focused ownership, repository-derived module validation, and standalone player in `33562388717`. The shipped player catalogue contains 480 definitions with macro towns/routes/geography instead of the broken local-only 434 definitions.
- Exact source `d36d96...` passed focused production storage plus a 180-second standalone replay in `33563288872`. Editor storage proves all 16 generic buildings and real-player Moordell shell/roof probes become non-air, but Moordell content is not ready until about 175 s and only `macro-moordell.png` is emitted.
- The 180-second trace falsifies missing catalogue, evaluator, storage, and renderer-readiness as the remaining cause. Individual simple blockouts still wrote roughly 460k voxels because the hollow-wall optimization retained a solid full-footprint foundation slab.
- Master `b1b69290a59278b0e7caba798641c76a9866aa5c` is already merged. Post-merge requests `33635511188` and `33635715141` were infrastructure-only failures: `unity-run.sh` killed Unity at the enforced 8192 MB free-memory floor before any test or player product execution.
- Retry 5 run `33641059051` on source `7e6d30858677f2504763e891289293c9507cfd9f` cleared runner admission with 38,665 MB free. Repository-derived validation then failed first in 15 unrelated `VoxelEngine.Tests.EditMode` renderer/GPU tests, so the requested process-isolated Kentridge PlayMode regression never executed. Current master has advanced hundreds of commits and its renderer implementation/test contract has moved; this is baseline drift outside agent-6 scope, not evidence for another Kentridge fix.
- The same Retry 5 standalone player replay completed 180 s with zero harness assertion failures. Macro catalogue ownership remained correct at 480 definitions. Moordell content became ready at about 85 s instead of the pre-plinth ~175 s, and readiness telemetry reported radius 3 / 29 horizontal columns / 31 total resident snapshot / 29 in-radius / zero feature-only vertical extras. Renderer publication coverage nevertheless remained false through 180 s, so the strict capture gate correctly prevented claiming readable downstream Fairy/Orc evidence.

## Selected fix and evidence under validation
The ownership bug remains fixed scene-locally: `KentridgePlayableSlice` builds/combines the intended playable catalogue before constructing `ShowcaseWorld`, so the intended catalogue consumes the one-shot `TopDownWorldLayoutSelection` first. Shared WorldBuilder/macro semantics are unchanged.

The demonstrated throughput defect is corrected in the reusable generic blockout program: the foundation is four bounded perimeter-plinth boxes rather than one solid slab. The plinth preserves sampled-relief grounding, outer footprint/material/vertical envelope, wall support, collision silhouette, and definition bounds. The independent synthetic regression requires four foundation boxes, a hollow centre, and <50% of the former solid-foundation volume while retaining the hollow-wall cost proof.

Vertical-residency blast-radius evidence uses a validation-only `KentridgeMacroWorldResidencyCostDiagnostic`. It reads actual resident coordinates through `IRegionReadSource`, applies the same terrain/camera baseline used by `ShowcaseWorld`, and reports feature-only extra Y regions per generic settlement. No production residency API or policy is changed.

`experiment-030-throughput-retry5-baseline-renderer-gate.md` is the current handoff authority for the new CI result. Do not change renderer production/tests from this assignment and do not weaken `HasCompletePublishedNearSurfaceCoverage()` just to force screenshots.

## Next gates
1. Pre-merge exact-SHA Kentridge validation remains blocked because the supported single-test workflow always runs repository-derived persistent modules before the requested process-isolated `VoxelEngine.Tests.PlayMode` test, and the stale branch renderer baseline currently fails that prerequisite. Record rather than broaden scope.
2. Continue independent Kentridge evidence/cost work that does not require claiming the blocked test: retain the measured ~90 s Moordell convergence improvement and zero feature-only vertical residency delta, but require actual Fairy/Orc shell/roof storage and readable captures before checking those tasks.
3. Inspect full-resolution settlement, lake/constrained-route, ridge/pass, network, and CharacterMotor evidence only when the strict published-coverage gate succeeds; reject anything below the issue's explicit blockout acceptance bar.
4. Measure final feature work/time, FPS/CPU/memory, streaming convergence, render/far-field telemetry, and resident-region blast radius against budgets. Retry 5 provides partial runtime evidence (post-30 s interval FPS median ~103.9, burst stalls still present), not closure evidence.
5. Do not close while the pre-merge exact-SHA gate is blocked. Once required gates are genuinely green, follow the requested order: close state updates, merge current master, re-run final exact-SHA gates, and non-force promote the exact feature head.
