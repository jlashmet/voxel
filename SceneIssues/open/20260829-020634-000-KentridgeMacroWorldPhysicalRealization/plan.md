# Plan

## Acceptance authority
`issue.json` is the contract (`captures: []`). Preserve the source-backed Mounting Force graph while delivering physical settlements, terrain-aware hard routes, reusable geography, Rossdam Lake, Southern Ridge/pass, CharacterMotor traversal, durable built-player evidence, and bounded cost. Follow `AGENTS.md`, `SceneIssues/README.md`, and `feature-readme.md`.

## Proven results
- Production physical planning covers 20 hard routes, 6 settlements, 16 generic buildings, deterministic geography, constrained-route solutions, lake/ridge realization, bounded slopes, storage, and feature-aware vertical residency/readiness.
- Reuse is proven by independent non-Kentridge blocked-water, spatial-reservation, and synthetic generic-blockout fixtures.
- Exact ownership source `d36d96eefe159b4b62bb8e9a275bbdc69744e84b` passed focused ownership, repository-derived module validation, and standalone player in `33562388717`. The shipped player catalogue now contains 480 definitions with all macro towns/routes/geography instead of the broken local-only 434 definitions.
- Exact source `d36d96...` also passed focused production storage plus a 180-second standalone replay in `33563288872`. Editor storage proves all 16 generic buildings; real-player Moordell shell/roof probes become non-air, but Moordell content does not settle until ~164 s and only `macro-moordell.png` is emitted before the replay ends.
- The 180-second trace falsifies missing catalogue, evaluator, storage, and renderer-readiness as the remaining cause. Per-building authoritative feature work is dominated by the generic fallback foundation: the prior hollow-wall optimization retained a solid full-footprint foundation slab, with individual Moordell structures writing roughly 460k voxels.

## Selected fix under validation
The ownership bug remains fixed scene-locally: `KentridgePlayableSlice` builds/combines the intended playable catalogue before constructing `ShowcaseWorld`, so the intended catalogue consumes the one-shot `TopDownWorldLayoutSelection` first. Shared WorldBuilder/macro semantics are unchanged.

The demonstrated throughput defect is corrected in the reusable generic blockout program: the foundation is now four bounded perimeter-plinth boxes rather than one solid slab. The plinth still spans sampled terrain relief, preserves the authored outer footprint/material/vertical envelope, and supports all four exterior walls. The independent synthetic regression requires four foundation boxes, a hollow centre, and <50% of the former solid-foundation voxel volume, while retaining the existing hollow-wall cost proof. Kentridge storage acceptance still checks grounded foundation semantics plus timber/roof storage.

Current merged feature head before documentation updates: `a8bdad310f37715f560aadba6cce56d302a9867d`, which includes master `b1b69290a59278b0e7caba798641c76a9866aa5c`.

## Next gates
1. Exact-SHA CI for the merged perimeter-foundation source: smallest blockout regression plus repository-derived module validation and canonical built-player integration.
2. Require the built player to reach Moordell/Rossdam/Fairy/Orc authored shell/roof probes and produce readable settlement captures; quantify target timing improvement.
3. Inspect full-resolution settlement, lake/constrained-route, ridge/pass, network, and CharacterMotor evidence and reject anything below the issue’s intended blockout acceptance quality.
4. Measure vertical residency blast radius, route/water/feature work, streaming convergence, FPS/CPU/memory, and render/far-field telemetry against budgets.
5. After every acceptance item is green, re-fetch master, merge if advanced, run final exact-SHA gates, close the SceneIssue, and non-force promote the exact feature head.