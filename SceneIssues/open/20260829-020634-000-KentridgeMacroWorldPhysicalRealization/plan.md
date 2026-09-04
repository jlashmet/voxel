# Plan

## Acceptance and ownership
`issue.json` is the contract (`captures: []`). Preserve the source-backed Mounting Force macro graph while delivering physical settlements, contiguous terrain-aware hard routes, reusable regional geography, Rossdam Lake, Southern Ridge/pass, real CharacterMotor traversal, durable built-player evidence, and bounded CPU/GPU/memory/streaming cost.

Owned validation surfaces:
- WorldBuilder: `Validation/MacroPhysicalWorld` through the production catalogue/rendering path.
- Showcase composition: `Validation/FeatureResidency` through production residency/readiness.
- Kentridge Playable: `Validation/KentridgeMacroWorld` through the real slice/evidence driver.
- VoxelEngine Rendering: `Validation/GpuSurfaceMirrorRelocation` through production GPU mirror/extraction/publication.
Semantic/config adapters without independent scene lifecycle retain module-local behavioral coverage.

## Proven implementation
The shared planner/catalogues cover 20 hard routes, 6 settlements, 16 generic buildings, deterministic geography, Rossdam Lake, Southern Ridge/pass, bounded slopes, storage, spatial reservations, and feature-aware residency. Independent non-Kentridge fixtures prove blocked-water routing/reuse. Generic fallback buildings use hollow shells/perimeter plinths, reducing combined foundation+timber synthetic work 79.7%. Horizontal residency remains radius 3 / 29 XZ columns; no device or renderer concurrency budget was increased.

## Current root cause and selected correction
Run `33899824434` proved unrelated ready-block changes could globally restart every bounded mirror coverage scan. Production commit `7641f8d2c0b4088ed23f7ad29161965a6005a606` keeps the global epoch only for world/history replacement and gates changed-block recovery per registered demand footprint.

Exact validation run `33905495634` on source `cafd0f934a3bf376dc10cf33196d90a821b40862` then failed the focused unrelated-change PlayMode assertion, but its own payload reported `gpuCompleted=461` during the alleged 20-second all-worker stall. That falsifies continued admission starvation: `active` is sampled only after each rendered frame and can be zero after same-frame admission/completion, while the intentionally held control demand keeps `pending=1` and `demand>=9`.

Experiment 045 records this discriminator. Test-only commit `2fb10483fe3584dc73a2326a9cd806a7589d2ff0` now defines a true saturated interval as backlog + saturated demand + `active==0` + no new GPU completion, and exits after four useful post-relocation completions. Current feature head is `ac5b4e387e3be0f75a8f2df41af82ebebe388b00`. No production behavior changed after `7641f8d2`.

## Remaining gates
1. Exact-SHA rerun the corrected unrelated-change regression through `ci-test/fixes/agent-6`; preserve queued/running requests.
2. Inspect repository-derived validation for all four owned player scenarios and the 180-second Kentridge replay. Macro evidence must progress through Moordell, Rossdam/lake, Fairy, Orc, ridge/pass, network overview, and CharacterMotor traversal.
3. Inspect full-resolution built-player evidence for production-quality settlement readability, road arrival/exit, substantial lake, ridge/pass, constrained route, differentiated countryside, and process cleanliness.
4. Quantify multi-target convergence, vertical residency deltas, feature work, FPS/CPU/render/far-field telemetry, and process/managed/native/GPU memory against budgets.
5. Merge current `origin/master` before final promotion, revalidate affected exact-SHA work, complete every `tasks.md` item, close only this issue, populate resolution metadata, then PR + auto-merge and monitor the required `affected` gate until merged.
