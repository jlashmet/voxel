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
- Direct review of that Water evidence rejected it as `prototype/blockout quality`: the startup-only patch does not visibly demonstrate still water, shallow shoreline, river flow, waterfall/cascade, and terrain contact. The validation composition must be upgraded before closure; automation green alone does not satisfy visual acceptance.

## Blast radius / cost
CI/orchestration, validation assets, tests, and docs only; no authoritative runtime behavior changed. Measured automatic validation cost is 167.71 seconds for the current Water + Kentridge path. The visual fix is confined to Water validation composition/content and must continue to exercise production Water rendering.

## Current commit
Post-green visual-review checkpoint: 609b42778ff6f4d6a5a380723d5aef600710cace

## Remaining gates
- [x] Inspect current validation architecture and identify reusable harness boundary.
- [x] Implement metadata/discovery and shared/core fallback.
- [x] Migrate Water validation scene/scenario.
- [x] Remove generic feature-specific inference and update docs/workflow semantics.
- [x] Add automated regression coverage, including fail-closed execution and integration-only fallback behavior.
- [ ] Upgrade and visually accept representative production Water validation content.
- [ ] Rerun exact-SHA CI demonstrating automatic complete flow and record final cost/evidence.
- [ ] Review all 18 acceptance criteria, complete metadata, open -> closed, merge current master, and promote exact head.
