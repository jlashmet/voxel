# Plan

## Acceptance
- CI deterministically maps production diffs to owning modules, focused tests, player-visible module validation targets, and the canonical built-player Kentridge gate.
- Module validation scenes/scenarios are module-owned; scene and scenario are distinct metadata.
- One generic standalone-player harness executes all visual targets; no test-name/scene-name feature policy remains in shared infrastructure.
- Water demonstrates the migration and automatic discovery path.
- Exact-SHA validation fails closed on missing/zero-match/skipped required targets and remains practical for routine iteration.

## Current architecture / hypotheses
1. Existing single-test CI request and player-capture scripts already contain most reusable build/capture mechanics; the narrowest change is to add a diff-driven validation planner plus module metadata, then make CI consume that plan.
2. If current scripts are too coupled to test names/scenes, extract their generic player build/capture core rather than introduce a second harness.

## Approach
Inspect current workflow/scripts, module/test layout, Water implementation, and Kentridge player validation. Introduce declarative module validation metadata and a deterministic planner with conservative shared/core expansion. Migrate Water to module-local scene/scenario metadata, simplify generic harness special cases, update workflow/docs, add focused planner/metadata regressions, then prove an ordinary production diff triggers focused tests + Water built-player validation + built-player Kentridge.

## Blast radius / cost
CI-only orchestration plus validation assets/docs should not alter authoritative runtime behavior. Runtime cost should be bounded to affected module tests, affected player-visible module scenes, and one Kentridge integration build/run; unrelated module visual scenes must not run.

## Current commit
84b0f7f95553f1dd4e88f61d18cfe45a7ea3740f

## Remaining gates
- [ ] Inspect current validation architecture and identify reusable harness boundary.
- [ ] Implement metadata/discovery and shared/core fallback.
- [ ] Migrate Water validation scene/scenario.
- [ ] Remove generic feature-specific inference and update docs/workflow semantics.
- [ ] Add automated regression coverage.
- [ ] Run exact-SHA CI demonstrating automatic complete flow and measure cost.
- [ ] Complete metadata, pending -> closed, merge current master, and promote exact head.
