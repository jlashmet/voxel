# Plan

## Acceptance
- Production diffs deterministically select owning modules; **all EditMode and PlayMode tests owned by each affected module run automatically**. Agents do not register individual tests or maintain per-test filters.
- Module ownership comes from repository/module structure and Unity assembly boundaries. Shared/core changes expand through the production assembly dependency graph where practical, with conservative fallback only when ownership/dependency inference is unavailable.
- Module-local built-player validation is optional declarative metadata; canonical `KentridgePlayableSlice` remains the mandatory top-level built-player integration gate for production diffs.
- Module scene/scenario metadata is declarative and separate; one generic player harness executes it fail-closed.
- Water proves migration/reuse with production rendering and production-quality standalone-player evidence.
- Missing/zero-match/skipped/failed required targets fail validation; routine targeted cost remains practical.

## Architecture correction before closure
The first implementation required each `*.module-validation.json` manifest to enumerate explicit test filters. That is unnecessary bookkeeping and creates a stale-registration failure mode. Replace it with convention/assembly-driven discovery:

1. A production diff identifies the owning Unity module/assembly from its module root and/or production `.asmdef`.
2. CI discovers every test assembly belonging to that module and runs the complete EditMode and PlayMode test sets for those assemblies. Adding a test under the module requires no manifest edit.
3. Test ownership is inferred from module-local test assembly definitions / module structure, not from hand-maintained test method or class lists.
4. Shared/core production assembly changes expand to dependent production modules through `.asmdef` references where practical; unresolved shared ownership remains conservative/fail-closed rather than silently narrowing validation.
5. Player-validation metadata remains only for things that cannot be inferred structurally: the module-local validation scene/scenario. Kentridge remains automatic and is not selected by agents.
6. Remove explicit `tests` arrays and planner logic/tests that require per-test registration. Add regression coverage proving that a newly added module test is discovered without changing module metadata or planner code.

## Implemented before architecture correction
- Repository-driven `*.module-validation.json`, diff ownership/shared-core expansion, separate player scenarios, mandatory Kentridge integration, generic readiness/evidence windows, and independent Structures reuse fixture.
- Exact run `33375145205` proved automatic Water focused tests -> Water player -> Kentridge player in 179.82s (~10.3% over the prior 163s path).
- Required Water correctness defects were isolated with behavioral regressions: shared-arena addressing, upward topology, greedy planar merging blocking waves, presentation-only displacement, and bulk terrain authoring budget.
- Runs `33385476451`, `33388147850`, and `33390924383` passed automated module/player gates, but the first two retained Water tableaus failed direct production-quality review.
- Ownership/fallback audit confirmed unknown production paths cannot degrade to integration-only validation: fallback coverage expands to every discovered owning module, keeps their module-local gates, fails closed outside declared fallback scope, and includes independently discovered module manifests (`b8c891d3456a5ec4e9ee10da154324c072920a73`, `3251bc7a0d53926d6ef055f4ed53633deac648e8`).
- Post-master exact run `33431392723` proved the requested PlayMode regression and automatic planning but failed closed when the then-explicit Water EditMode filter matched zero tests. Root-cause comparison found the master merge had dropped both the flat-water top-tessellation regression and its narrow production invariant; both were restored while retaining the newer canonical Water material/topology/spray behavior.
- Run `33432210469` passed the prior exact-head architecture, after which direct artifact review exposed a generic evidence-window filename parsing defect; the current branch contains the generic parser fix and regression, which still requires exact-head validation after the architecture correction.

## Reuse boundary / resolved prerequisite
- After two materially different scene-level corrections, root-cause review found Agent-8 was duplicating Water showcase composition policy. Agent-9 owns the canonical production `WaterRenderingShowcase`; its scene is a thin shell around shared `VoxelEngine.Showcase.WaterRenderingShowcase` composition and has semantic production-path regressions.
- Agent-9's canonical Water work landed on `master` in close commit `0de38ba704be999c13c9c9aa59237efa65405144`, clearing the external prerequisite without copying/cherry-picking another assignment.
- The module-local Water scene already consumes the canonical `WaterRenderingShowcase` component. Integration therefore stays at the semantic metadata boundary: own `WaterRenderingShowcase.cs` instead of the removed startup prototype. No third bespoke tableau/camera/shader tweak is permitted or needed.

## Blast radius
CI/orchestration, module/test discovery, validation assets/tests/docs, and the Water validation adapter/metadata. No new simulation/collision policy or adjacent-system refactor.

## Remaining gates
- [ ] Replace explicit test registration with convention/assembly-driven discovery that runs every affected module EditMode/PlayMode test.
- [ ] Prove a new module test is automatically included without manifest/planner registration and prove shared/core dependency expansion remains conservative.
- [ ] Run exact-head module tests, Water built-player, and mandatory Kentridge built-player validation.
- [ ] Inspect every retained Water standalone frame and verify production quality/evidence-window pruning.
- [ ] Review all 18 criteria; then complete metadata, move open -> closed, merge current master, and promote non-force.
