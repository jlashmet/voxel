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
- Targeted CI now derives module tests/player targets from the exact feature diff and automatically adds Kentridge for production changes.
- Water is the representative player-visible migration; an independent Structures planner fixture proves metadata reuse without planner code changes.
- Required focused tests fail on missing results, zero matches, skipped/failed cases; player validation fails on missing scene/scenario, failed player execution, required/forbidden log assertions, or insufficient captures.

## Blast radius / cost
CI/orchestration, validation assets, tests, and docs only; no authoritative runtime behavior changed. Expected validation cost is affected focused tests plus affected module player scenes plus one Kentridge player build/run; unrelated module visual scenes are excluded.

## Current commit
Pre-CI checkpoint: da6add5e012f7fc549d96fc2a8fec6b15d808c63

## Remaining gates
- [x] Inspect current validation architecture and identify reusable harness boundary.
- [x] Implement metadata/discovery and shared/core fallback.
- [x] Migrate Water validation scene/scenario.
- [x] Remove generic feature-specific inference and update docs/workflow semantics.
- [x] Add automated regression coverage, including fail-closed execution.
- [ ] Run exact-SHA CI demonstrating automatic complete flow and measure cost.
- [ ] Review all 18 acceptance criteria, complete metadata, open -> closed, merge current master, and promote exact head.
