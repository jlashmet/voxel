# Plan

## Acceptance
- Production diffs deterministically select owning modules, focused tests, module-local built-player validation, and canonical `KentridgePlayableSlice` integration.
- Module scene/scenario metadata is declarative and separate; one generic player harness executes it fail-closed.
- Water proves migration/reuse with production rendering and production-quality standalone-player evidence.
- Missing/zero-match/skipped/failed required targets fail validation; routine targeted cost remains practical.

## Implemented
- Repository-driven `*.module-validation.json`, diff ownership/shared-core expansion, separate player scenarios, mandatory Kentridge integration, generic readiness/evidence windows, and independent Structures reuse fixture.
- Exact run `33375145205` proved automatic Water focused tests -> Water player -> Kentridge player in 179.82s (~10.3% over the prior 163s path).
- Required Water correctness defects were isolated with behavioral regressions: shared-arena addressing, upward topology, greedy planar merging blocking waves, presentation-only displacement, and bulk terrain authoring budget.
- Runs `33385476451`, `33388147850`, and `33390924383` passed automated module/player gates, but the first two retained Water tableaus failed direct production-quality review.
- Ownership/fallback audit confirmed unknown production paths cannot degrade to integration-only validation: fallback coverage expands to every discovered owning module, keeps their focused tests/module-local player gates, fails closed outside declared fallback scope, and includes independently discovered module manifests (`b8c891d3456a5ec4e9ee10da154324c072920a73`, `3251bc7a0d53926d6ef055f4ed53633deac648e8`).

## Reuse boundary / blocker
- After two materially different scene-level corrections, root-cause review found Agent-8 was duplicating Water showcase composition policy. Agent-9 owns the canonical production `WaterRenderingShowcase`; its scene is a thin shell around shared `VoxelEngine.Showcase.WaterRenderingShowcase` composition and has semantic production-path regressions.
- Do not make a third bespoke tableau tweak or copy/cherry-pick another assignment. Once Agent-9's Water work reaches `master`, make the module-local Water validation scene a thin adapter using that canonical composition, keep capture/scenario policy in Water module metadata, and select semantic focused regressions rather than PlayMode visual proof.
- Latest verified `master` is `2edf4c2e151492f67c4a1c1b846a9b7948284aba`; Agent-9 Water work is not there yet. No additional independent acceptance work remains after the ownership/fallback audit.

## Blast radius
CI/orchestration, validation assets/tests/docs, and the Water validation adapter/metadata. No new simulation/collision policy or adjacent-system refactor.

## Remaining gates
- [x] Audit ownership/shared-core fallback against all production-change acceptance requirements and fix only demonstrated gaps.
- [ ] After canonical Water lands on `master`, reuse it from module-local Water validation and run exact-head CI.
- [ ] Inspect every retained Water standalone frame and verify production quality.
- [ ] Review all 18 criteria; then complete metadata, move open -> closed, merge current master, revalidate exact head as required, and promote non-force.
