# Plan

## Acceptance and ownership
`issue.json` is the contract (`captures: []`). Preserve the source-backed Mounting Force macro graph while delivering physical settlements, contiguous terrain-aware hard routes, reusable regional geography, Rossdam Lake, Southern Ridge/pass, representative CharacterMotor traversal, durable built-player evidence, and bounded CPU/GPU/memory/streaming cost.

Owned validation surfaces remain:
- WorldBuilder Generation: `Validation/MacroPhysicalWorld` through the real production catalogue/rendering path.
- Showcase composition: `Validation/FeatureResidency` through production residency/readiness.
- Kentridge Playable composition: `Validation/KentridgeMacroWorld` through the real slice and macro evidence driver.
- VoxelEngine Rendering: `Validation/GpuSurfaceMirrorRelocation` through the production GPU mirror/extraction/publication path.
Semantic/config adapters without independent scene lifecycle retain module-local behavioral coverage.

## Proven implementation
The shared physical planner/catalogues already cover 20 hard routes, 6 settlements, 16 generic buildings, deterministic geography, Rossdam Lake, Southern Ridge/pass, bounded slopes, storage, spatial reservation integration, and feature-aware residency. Independent non-Kentridge fixtures prove blocked-water routing and generic reuse. Generic fallback buildings use hollow shells/perimeter plinths, reducing combined foundation+timber synthetic work 79.7% without changing footprint/support semantics. Horizontal residency remains radius 3 / 29 XZ columns and no device or renderer concurrency budget has been increased.

## Current root cause and selected fix
Exact run `33899824434` on feature source `23a00f432cb97338dc7887eb852c5dd39fbd430a` failed the minimal unrelated-change discriminator. After a 384 m relocation, 2,943 changes to one distant held control block kept all relocated workers mirror-admission pending for 20.0 s with `active=0`, only `pending=1`, and `mixedResident=1059/93312`. This confirms global `CoverageEpoch` churn—not mirror capacity or in-footprint recovery—is sufficient to starve bounded 18^3 coverage scans.

Selected production correction `7641f8d2c0b4088ed23f7ad29161965a6005a606` keeps the global epoch only for world/history replacement. Queued recovery is counted per registered demand footprint; `TryBeginExtraction` blocks only when its own footprint still has changed blocks awaiting recovery. Relevant edits remain stale-safe even after the bounded scan cursor has passed them, while unrelated edits cannot reset every worker. No Kentridge-specific renderer policy, radius, coverage threshold, upload/mirror budget, or extraction concurrency changed. Experiment 044 records the discriminator and correction.

Rejected alternatives remain: widening Kentridge load/coverage radius, weakening strict publication acceptance, increasing renderer/device budgets or concurrency, hardcoding settlement-specific rendering, or substituting fake validation visuals.

## Remaining gates
1. Exact-SHA rerun `DistantUnrelatedChangeChurnExecutesProductionGpuLivenessRegression` on the final bookkeeping source through `ci-test/fixes/agent-6`; do not replace queued/running CI.
2. Inspect repository-derived validation for all four owned player scenarios and the 180-second Kentridge replay. Strict macro evidence must progress through Moordell, Rossdam/lake, Fairy, Orc, Southern Ridge/pass, network overview, and real CharacterMotor traversal.
3. Inspect full-resolution visual evidence for grounded settlements, road arrivals/exits, substantial lake, barrier/pass relationship, constrained route, differentiated countryside, and process cleanliness. Automation alone is not visual acceptance.
4. Quantify final multi-target convergence, vertical resident/generated deltas, feature work, FPS/CPU/render/far-field telemetry, and process/managed/native/GPU memory against repository budgets.
5. Fetch current `origin/master`, merge it into `fixes/agent-6` before final promotion, revalidate affected exact-SHA work, complete every `tasks.md` item, move only this issue `open -> closed`, populate resolution metadata, then promote via feature PR + auto-merge and monitor the required `affected` gate until merged.
