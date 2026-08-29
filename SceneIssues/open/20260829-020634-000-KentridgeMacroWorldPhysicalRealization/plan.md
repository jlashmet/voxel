# Plan

## Observed gap / acceptance
`captures` is empty, so the feature note is the repro contract. Closure requires the authoritative Kentridge macro graph to drive deterministic physical settlements, continuous terrain-aware hard routes, reusable geographic constraints, substantial lake/ridge geography, CharacterMotor traversal, closure-quality built-player evidence, and measured cost. `SceneIssues/feature-readme.md` is absent, so canonical `SceneIssues/README.md` governs.

## Hypotheses / results
1. **Scale up legacy markers/roads.** Rejected: no reusable geography constraints/crossings, generic settlement realization, or blocked-route rejection.
2. **Keep `TopDownWorldLayout` authoritative and add a reusable physical-plan layer.** Selected/implemented.
3. **Weaken geography to fix route failures.** Rejected. Preserve substantial lake/ridge and author explicit semantic `GoAround`/pass solutions; focused regressions are green.
4. **Run `33258816868` proved master was broken.** Rejected. Agent-6 carried stale `StorySpecs.cs`; restored current-master bytes.
5. **Green workflow alone proves visual acceptance.** Rejected repeatedly: prior green runs exposed missing time, premature coverage, harmful prewarm, then occluded evidence composition.
6. **Remote blocking prewarm helps later evidence.** Rejected by same-camera no-prewarm comparison; removed without changing production streaming.
7. **Remaining missing towns imply missing generated blockouts.** Rejected by production plan/test and Moordell visual. `TopDownWorldPhysicalPlanner` places four deterministic plots at ±190 dm; run `33261299347` used ground-level center-facing cameras that macro terrain can occlude. See `experiment-007-evidence-camera-occlusion.md`.

## Implemented direction / blast radius
- Shared region vocabulary/constraints, terrain-aware solver, reusable settlement blockouts, continuous roads, carved Rossdam basin, Logan pass, Bandit/Orc route-arounds; richer Kentridge/Hightown remain intact.
- No second graph, planner weakening, eager destination hierarchy, or Kentridge-only direct voxel path.
- Current validation-only evidence source `3767205da4df9e94114871722aa0de05834a788c`: no remote prewarm; opening timeline 12x only while validation owns the opening; original `Time.timeScale` restored before normal CharacterMotor traversal and teardown; local/macro-road movement retained; every remote capture remains gated on published near-surface coverage; generic settlement cameras use generated settlement centers/terrain with elevated survey poses; Rossdam lake uses a 72 m camera keeping the constrained road and ~104x54 m water region together; ridge/pass and network survey are elevated. Production world truth/streamer/planner/catalogues are untouched by these evidence changes.
- Current master was `ff781ed26b1d9182fa8cd76e2d2da08abfa3765c` at the last refresh and is already an ancestor; refresh again before final CI/promotion.

## Remaining gate
Update `tasks.md`/CI ledger to this source, refresh master/scope, freeze one exact source SHA, and use the existing `ci-test/fixes/agent-6` transport for the focused acceptance + 60s built-player replay. Require all seven target frames plus macro-road capture, visible four-town blockouts, readable lake/ridge/constrained route/network, normal-time CharacterMotor traversal, no runtime exceptions, and measured cost telemetry. Only after every task/acceptance is supported: open->pending metadata, pending->closed fixed metadata, refresh/merge current master, verify exact head, and non-force promote to `master`.