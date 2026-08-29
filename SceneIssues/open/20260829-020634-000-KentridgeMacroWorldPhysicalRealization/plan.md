# Plan

## Observed gap / acceptance
`captures` is empty, so the feature note is the repro contract. Closure requires the authoritative Kentridge macro graph to drive deterministic physical settlements, continuous terrain-aware hard routes, reusable geographic constraints, substantial lake/ridge geography, CharacterMotor traversal, closure-quality built-player evidence, and measured cost. `SceneIssues/feature-readme.md` is absent, so canonical `SceneIssues/README.md` governs.

## Hypotheses / results
1. **Scale up legacy markers/roads.** Rejected: no reusable geography constraints/crossings, generic settlement realization, or blocked-route rejection.
2. **Keep `TopDownWorldLayout` authoritative and add a reusable physical-plan layer.** Selected/implemented.
3. **Weaken geography to fix route failures.** Rejected. Preserve substantial lake/ridge and author explicit semantic `GoAround`/pass solutions; focused regressions are green.
4. **Run `33258816868` proved master was broken.** Rejected. Agent-6 carried stale `StorySpecs.cs`; restored current-master bytes.
5. **Green workflow alone proves visual acceptance.** Rejected repeatedly: prior green runs exposed missing time, premature coverage, harmful prewarm, occluded evidence composition, incomplete evidence scheduling, and finally false-positive near-surface coverage for elevated views.
6. **Remote blocking prewarm helps later evidence.** Rejected by same-camera no-prewarm comparison; removed without changing production streaming.
7. **Remaining missing towns imply missing generated blockouts.** Rejected by production plan/test and portions of visual evidence. `TopDownWorldPhysicalPlanner` produces four deterministic generic-town blockouts.
8. **Run `33261744161` is closure-quality because CI is green.** Rejected. It omitted Orc/ridge/overview evidence and generic-town framing remained weak. See `experiment-008-visual-evidence-scheduling-and-focus.md`.
9. **Run `33263409994` is closure-quality because all eight PNGs exist and `coverage=True`.** Rejected by full-resolution inspection. Rossdam/Fairy/Orc do not visibly prove their four-building blockouts, Moordell is clipped, the lake frame is dominated by a translucent plane with only a small water sliver, and the overview/road views contain large unstreamed holes. `HasCompletePublishedNearSurfaceCoverage()` is only a resident-neighborhood readiness predicate, not proof that an elevated camera frustum is fully rendered. See `experiment-009-frustum-coverage-and-readable-evidence.md`.

## Implemented direction / blast radius
- Shared region vocabulary/constraints, terrain-aware solver, reusable settlement blockouts, continuous roads, carved Rossdam basin, Logan pass, Bandit/Orc route-arounds; richer Kentridge/Hightown remain intact.
- No second graph, planner weakening, eager destination hierarchy, or Kentridge-only direct voxel path.
- Production world truth/streamer/planner/catalogues remain frozen while visual-proof defects are isolated to validation/evidence plumbing.
- Exact green source `7c9e0dac78331f51cd5ce310956ec799df21f00e` still passes the focused production path and built-player harness, restoring `Time.timeScale=1` before real CharacterMotor evidence and emitting all eight named screenshots, but the screenshots are not closure-quality.
- Current `origin/master` is `ff781ed26b1d9182fa8cd76e2d2da08abfa3765c`; defer final merge until every acceptance/evidence/cost gate is complete, then refresh again immediately before promotion.

## Current experiment
Keep production macro implementation frozen. First inspect existing renderer/streamer readiness APIs. Prefer an existing coverage query that can prove the bounded evidence footprint. If no frustum-aware API exists, make the validation driver require complete published coverage at a deterministic bounded set of camera/focus/intermediate sample points needed by each capture rather than changing production streaming. Tighten generic-town framing around the four production blockout centers, choose lake/ridge views from resolved physical extents that prove the feature cleanly, and replace the too-wide overview with a bounded connected-route overview that resident streaming can actually render completely. Preserve real normal-time CharacterMotor motion and existing production budgets.

## Remaining gate
After the evidence-only repair, self-review exact scope/blast radius and cost. Then freeze the final source SHA and run the assigned exact-SHA focused acceptance + built-player replay without modifying the feature branch transport contract. Full-resolution artifact inspection must show four readable settlement blockouts, continuous road/motor traversal, substantial clean Rossdam basin/shoreline with geography-constrained route, readable ridge/pass, and a connected route overview without large unstreamed holes. Runtime must remain exception-free and cost telemetry must remain within existing budgets. Only after every task/acceptance is supported: complete pending metadata, move only this assignment open -> pending -> closed with fixed metadata, refresh/merge current master, verify exact head, and non-force promote that exact head to `master`.