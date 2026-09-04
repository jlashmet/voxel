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

## Current root cause and selected correction
Run `33899824434` proved unrelated ready-block edits could globally restart all bounded mirror coverage scans. Production commit `7641f8d2c0b4088ed23f7ad29161965a6005a606` keeps world/history invalidation global but gates changed-block recovery per demand footprint.

Run `33905495634` then reported 461 GPU completions during the alleged 20s all-worker stall, falsifying the old `active==0` classifier. Experiment 045 / test commit `2fb10483fe3584dc73a2326a9cd806a7589d2ff0` requires both zero active extraction and zero GPU-completion progress.

Run `33912155787` on exact source `b45f6e36738c051250747253df9d75f6ad40c1fb` passed all persistent repository-derived test phases. The requested discriminator failed only because its success exit occurred after four useful GPU completions but before its own `>=8` unrelated-change requirement (`injected=3`). Experiment 046 isolates this test contradiction. Test-only commit `77d5314a39857b55e87ddb299807b5e323af5e28` now requires the full eight-change workload before successful exit; production behavior is unchanged.

The same 180s Kentridge replay remained closure-red despite zero harness assertions: `coverage=False`, `missingVisible=252`, eight GPU requests in flight, recovery backlog still draining. Treat this as an independent integration/convergence boundary if the focused discriminator turns green.

## Remaining gates
1. Exact-SHA rerun the corrected unrelated-change regression with automatic module validation and the required 180s replay.
2. If focused GPU liveness is green, isolate Kentridge convergence without speculative renderer changes; prove all four module-local player scenes.
3. Obtain production-quality Moordell/Rossdam/Fairy/Orc/ridge/network and CharacterMotor evidence plus final cost/memory measurements.
4. Merge current `origin/master` (`0d70e2e7…`) before final promotion, revalidate the merged exact SHA, complete every task/acceptance item, close only this SceneIssue, then PR + auto-merge and monitor `affected` until merged.
