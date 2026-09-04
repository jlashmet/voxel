# Plan

## Acceptance and ownership
`issue.json` is the contract (`captures: []`). Preserve the source-backed Mounting Force macro graph while delivering physical settlements, contiguous terrain-aware hard routes, reusable regional geography, Rossdam Lake, Southern Ridge/pass, CharacterMotor traversal, durable built-player evidence, and bounded CPU/GPU/memory/streaming cost.

Owned validation surfaces:
- WorldBuilder: `Validation/MacroPhysicalWorld` through production catalogue/rendering.
- Showcase: `Validation/FeatureResidency` through production residency/readiness.
- Kentridge Playable: `Validation/KentridgeMacroWorld` through the real slice/evidence driver.
- Rendering: `Validation/GpuSurfaceMirrorRelocation` through production GPU mirror/extraction/publication.

## Proven implementation
The shared planner/catalogues cover 20 hard routes, 6 settlements, 16 generic buildings, deterministic geography, Rossdam Lake, Southern Ridge/pass, bounded slopes, storage, reservations, and feature-aware residency. Independent non-Kentridge fixtures prove reuse. Hollow fallback shells/perimeter plinths reduce combined foundation+timber synthetic work 79.7%. Horizontal residency remains radius 3 / 29 XZ columns; renderer/device budgets were not increased.

## Current root causes and selected corrections
Run `33899824434` proved unrelated ready-block edits could globally restart all bounded mirror coverage scans. Production commit `7641f8d2c0b4088ed23f7ad29161965a6005a606` keeps world/history invalidation global but gates changed-block recovery per demand footprint.

Run `33905495634` then reported 461 GPU completions during the alleged 20s all-worker stall, falsifying the old `active==0` classifier. Experiment 045 / test commit `2fb10483fe3584dc73a2326a9cd806a7589d2ff0` requires both zero active extraction and zero GPU-completion progress.

Run `33912155787` exposed a test-only contradiction: success exited after four useful GPU completions before the discriminator had sustained its own eight-change workload. Experiment 046 / commit `77d5314a39857b55e87ddb299807b5e323af5e28` requires the full workload before successful exit.

Run `33915899972` on exact source `36c40e394a083892a0de84f02e62bb7c9e036b92` then passed that requested production GPU regression in 39.34s, validating the footprint-local mirror correction. Automatic module validation failed later because the Kentridge macro module's 50s player window ended with only three visible chunks missing, immediately before strict opening coverage could release gameplay. The standalone 180s replay reached real CharacterMotor traversal and Moordell content-ready, but its 70m / 90-degree near-nadir settlement framing remained both visually unreadable and expensive: strict coverage still had ~233 visible chunks missing at 180s and no settlement capture occurred.

Experiment 047 selects an acceptance-composition correction instead of another renderer budget change. Commit `8425e7a8f96656d1d3ec95456d03ff3449ac2bcd` makes only validation settlement evidence close and oblique (31m height, 260dm X/Z offset, shipped 58-degree FOV preserved) while keeping CharacterMotor streaming at the same camera point. Commit `c93cd9ca06f30dad3c5267a7cfe888321aadc1bd` extends the module proof window to 120s. Strict renderer coverage, LOD thresholds, residency radius, generation budgets, and normal gameplay composition remain unchanged.

## Remaining gates
1. Exact-SHA validate the Experiment 047 source: keep the requested GPU discriminator green and let automatic module validation complete all mandatory module-local players.
2. Inspect full-resolution module and 180s SceneIssue evidence; require readable Moordell/Rossdam/Fairy/Orc settlement views, Rossdam lake/constrained route, Southern Ridge/pass, macro-network overview, and representative CharacterMotor traversal under strict coverage.
3. Record final convergence, resident-region, FPS/CPU/GPU/streaming and memory costs against existing budgets.
4. Merge the then-current `origin/master` before final promotion, revalidate the merged exact SHA, complete every task/acceptance item, close only this SceneIssue, then PR + auto-merge and monitor `affected` until merged.
