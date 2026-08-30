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
- Exact-SHA run 33334407204: explicit Water test and all 9 planner/runner regressions passed. Automatic planning selected Water + Kentridge. The run then failed because fallback Kentridge injected the broad `KentridgePlayableScenePlayTests.*` suite, whose unrelated NPC range assertion failed before either built-player gate. Fix: integration/fallback metadata may omit focused tests; owning modules still require them. Kentridge remains mandatory as the built-player integration gate.
- Exact-SHA run 33334839567: explicit Water test and all 10 regressions passed; automatic plan correctly contained only the Water focused test plus Water/Kentridge player targets. Kentridge built successfully and ran for 60 seconds, but the harness found no log/captures because relative player artifact paths were resolved from the app runtime context. After two automatic-stage failures, root cause was isolated to relative `--output` handling before another fix. The shared capture harness now normalizes its artifact root to an absolute path before launching the player.

## Blast radius / cost
CI/orchestration, validation assets, tests, and docs only; no authoritative runtime behavior changed. Expected validation cost is affected owning-module focused tests plus affected module player scenes plus one Kentridge player build/run; unrelated module visual scenes and integration-only fallback PlayMode suites are excluded.

## Current commit
Post-root-cause fix checkpoint: 2223bde6a46aa5ee5af7d34970f818ad0ef51246

## Remaining gates
- [x] Inspect current validation architecture and identify reusable harness boundary.
- [x] Implement metadata/discovery and shared/core fallback.
- [x] Migrate Water validation scene/scenario.
- [x] Remove generic feature-specific inference and update docs/workflow semantics.
- [x] Add automated regression coverage, including fail-closed execution and integration-only fallback behavior.
- [ ] Run exact-SHA CI demonstrating automatic complete flow and measure cost.
- [ ] Review all 18 acceptance criteria, complete metadata, open -> closed, merge current master, and promote exact head.
