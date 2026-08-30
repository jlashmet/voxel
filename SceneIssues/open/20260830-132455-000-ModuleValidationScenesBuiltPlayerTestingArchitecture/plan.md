# Plan

## Acceptance
- CI deterministically maps production diffs to owning modules, focused tests, player-visible module validation targets, and the canonical built-player Kentridge gate.
- Module validation scenes/scenarios are module-owned; scene and scenario are distinct metadata.
- One generic standalone-player harness executes all visual targets; no test-name/scene-name feature policy remains in shared infrastructure.
- Water demonstrates the migration and automatic discovery path.
- Exact-SHA validation fails closed on missing/zero-match/skipped required targets and remains practical for routine iteration.

## Results / selected approach
- Reused the existing standalone-player build/capture path and removed feature/test-name policy from the shared harness instead of introducing a second harness.
- Added declarative `*.module-validation.json` ownership plus separate `*.player-scenario.json` scenario metadata and a diff-driven planner with dependent/fallback behavior.
- Targeted CI derives module tests/player targets from the exact feature diff and automatically adds Kentridge for production changes.
- Water is the representative player-visible migration; an independent Structures planner fixture proves metadata reuse without planner code changes.
- Required focused tests fail on missing results, zero matches, skipped/failed cases; player validation fails on missing scene/scenario, failed player execution, required/forbidden log assertions, or insufficient captures.
- Run 33334407204 exposed unintended Kentridge PlayMode coupling; integration/fallback metadata is now integration-only while owning modules still require focused tests.
- Run 33334839567 isolated relative standalone-player artifact paths as the second failure; the shared harness now normalizes the artifact root before launching the app.
- Exact-SHA run 33335079315 is green: automatic planning selected Water + Kentridge, the Water focused test passed, built-player Kentridge produced 4 frames, built-player Water produced 3 frames, and automatic module validation completed in 167.71 seconds.
- Direct review rejected that Water evidence as `prototype/blockout quality`; it showed only the old startup patch rather than the required representative Water relationships.
- Exact-SHA run 33336383760 was green after the first richer tableau attempt, but its Water frames were flat clear-colour output. The player log proved the composition aborted before creating production meshes.
- Minimal repro/root cause: `FarFieldStructureStore` samples each 512-voxel region at coarse column centres `16 + 32n` and records only sufficiently raised authored surfaces. The tableau also incorrectly checked a raised inner-pool probe against `BaseY`, guaranteeing rejection even when captured. Several rectangle start offsets never intersected the coarse-centre lattice at all. The fix authors every semantic rectangle directly on that lattice, lifts the deterministic review pedestal above terrain variation, and fail-fast probes still water, shoreline, river, cascade, receiving pool, and terrain contact before any screenshot can count.
- Exact-SHA run 33337294126 was green after the lattice fix and proved the actual `VoxelFarTerrain` production meshes were captured, but direct frame review still rejected the result: coarse rectangular authored masks plus an overhead overview camera made the real production renderer read as a flat diagram with hard slab edges and an exposed ring seam.
- Second visual root cause/minimal repro: the frozen meshes are production `VoxelFarTerrain`; the remaining blockout symptom is therefore scene composition, not a fake renderer. `AuthorTableau` authored orthogonal constant-height rectangles across the coarse lattice and `CreateCamera` looked down on them, maximizing every grid edge. The acceptance repair keeps the production renderer unchanged and varies only module-local composition: irregular rolling terrain, organic shoreline/pool masks, a wandering variable-width descending river, shaped rock banks/cascade contacts, and lower three-quarter framing.

## Blast radius / cost
CI/orchestration, validation assets, tests, and docs only; no authoritative gameplay runtime behavior changed. Measured automatic validation cost is 167.71 seconds for the earlier Water + Kentridge path. The current fix is confined to Water validation composition/content and deterministic ownership metadata; shared renderer/harness thresholds remain unchanged.

## Current commit
Visual-composition checkpoint: 02748ef59b953c4ce9d646439ed3c72cc8524fd4

## Remaining gates
- [x] Inspect current validation architecture and identify reusable harness boundary.
- [x] Implement metadata/discovery and shared/core fallback.
- [x] Migrate Water validation scene/scenario.
- [x] Remove generic feature-specific inference and update docs/workflow semantics.
- [x] Add automated regression coverage, including fail-closed execution and integration-only fallback behavior.
- [ ] Upgrade and visually accept representative production Water validation content.
- [ ] Rerun exact-SHA CI demonstrating automatic complete flow and record final cost/evidence.
- [ ] Review all 18 acceptance criteria, complete metadata, open -> closed, merge current master, and promote exact head.
