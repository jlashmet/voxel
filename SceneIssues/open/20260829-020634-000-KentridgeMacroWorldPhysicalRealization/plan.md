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

## Selected fix and evidence under validation
The ownership bug remains fixed scene-locally: `KentridgePlayableSlice` builds/combines the intended playable catalogue before constructing `ShowcaseWorld`, so the intended catalogue consumes the one-shot `TopDownWorldLayoutSelection` first. Shared WorldBuilder/macro semantics are unchanged.

The demonstrated throughput defect is corrected in the reusable generic blockout program: the foundation is four bounded perimeter-plinth boxes rather than one solid slab. The plinth preserves sampled-relief grounding, outer footprint/material/vertical envelope, wall support, collision silhouette, and definition bounds. The independent synthetic regression requires four foundation boxes, a hollow centre, and <50% of the former solid-foundation volume while retaining the hollow-wall cost proof.

Vertical-residency blast-radius evidence now uses a validation-only `KentridgeMacroWorldResidencyCostDiagnostic`. It reads actual resident coordinates through `IRegionReadSource`, applies the same terrain/camera baseline used by `ShowcaseWorld`, and reports feature-only extra Y regions per generic settlement. No production residency API or policy is changed.

## Next gates
1. When the sole runner is not occupied and memory is healthy, run exact-SHA CI for the current feature head: smallest blockout regression plus repository-derived module validation and canonical built-player integration. Never replace queued/running work.
2. Require the built player to reach Moordell/Rossdam/Fairy/Orc shell/roof probes, emit readable settlement captures, and report numeric feature-only vertical residency deltas; quantify target timing improvement.
3. Inspect full-resolution settlement, lake/constrained-route, ridge/pass, network, and CharacterMotor evidence and reject anything below the issue's explicit blockout acceptance bar.
4. Measure final feature work/time, FPS/CPU/memory, streaming convergence, render/far-field telemetry, and resident-region blast radius against budgets.
5. After every acceptance item is green, re-fetch master, merge if advanced, run final exact-SHA gates, close the SceneIssue, and non-force promote the exact feature head.
