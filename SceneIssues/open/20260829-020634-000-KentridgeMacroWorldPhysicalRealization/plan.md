# Plan

## Observed gap / acceptance
`captures` is empty, so the feature note is the repro contract. Closure requires the authoritative Kentridge macro graph to drive deterministic physical settlements, continuous terrain-aware hard routes, reusable geographic constraints, substantial lake/ridge geography, CharacterMotor traversal, closure-quality built-player evidence, and measured cost. `SceneIssues/feature-readme.md` is absent, so canonical `SceneIssues/README.md` governs.

## Hypotheses / results
1. **Scale up legacy markers/roads.** Rejected: no reusable geography constraints/crossings, generic settlement realization, or blocked-route rejection.
2. **Keep `TopDownWorldLayout` authoritative and add a reusable physical-plan layer.** Selected/implemented.
3. **Weaken geography to fix route failures.** Rejected. Preserve substantial lake/ridge and author explicit semantic `GoAround`/pass solutions; focused regressions are green.
4. **Run `33258816868` proved master was broken.** Rejected. Agent-6 carried stale `StorySpecs.cs`; restored current-master bytes.
5. **Green workflow alone proves visual acceptance.** Rejected repeatedly: prior green runs exposed missing time, premature coverage, harmful prewarm, occluded evidence composition, then incomplete evidence scheduling.
6. **Remote blocking prewarm helps later evidence.** Rejected by same-camera no-prewarm comparison; removed without changing production streaming.
7. **Remaining missing towns imply missing generated blockouts.** Rejected by production plan/test and Moordell visual. `TopDownWorldPhysicalPlanner` places four deterministic plots at ±190 dm; run `33261299347` used ground-level center-facing cameras that macro terrain can occlude. See `experiment-007-evidence-camera-occlusion.md`.
8. **Run `33261744161` is closure-quality because CI is green.** Rejected. Exact focused acceptance and built player are green, production metrics are bounded, and CharacterMotor moves at restored `Time.timeScale=1`, but the 60-second visual sequence finishes only macro-road, Moordell, Rossdam, Rossdam Lake, and Fairy Village. Orc Village, southern ridge/pass, and network overview are absent, while generic-town focus still emphasizes the empty circulation center. See `experiment-008-visual-evidence-scheduling-and-focus.md`.

## Implemented direction / blast radius
- Shared region vocabulary/constraints, terrain-aware solver, reusable settlement blockouts, continuous roads, carved Rossdam basin, Logan pass, Bandit/Orc route-arounds; richer Kentridge/Hightown remain intact.
- No second graph, planner weakening, eager destination hierarchy, or Kentridge-only direct voxel path.
- Production world truth/streamer/planner/catalogues remain untouched by evidence-only corrections. The current evidence driver owns validation-only opening acceleration, restores normal `Time.timeScale=1` before CharacterMotor traversal and teardown, keeps remote screenshots gated on published near-surface coverage, and uses elevated survey framing.
- Exact green source `34bcba11c160c36f110390c875df5d77e260d49d` produced 6 regions, 6 settlements, 16 blockout buildings, 20 hard routes, 833 route tiles, 5 constrained routes, 1108 solve steps, max road rise 2 voxels/30 dm, and 46-voxel water depth; local/macro CharacterMotor evidence traveled ~6.81 m/~8.20 m at normal time scale.
- Current master has advanced to `3ed69b00d8264b3bbaf72cd582a1571af2345dbf`; do not merge it until the final exact-SHA workflow/metadata gates are complete, then refresh again before promotion.

## Current experiment
Keep the production macro implementation frozen. Change only `KentridgeMacroWorldEvidenceDriver` to make the existing physical result provable inside the fixed replay window: focus generic settlements on real generated building geometry; retain published-near-coverage as the hard readiness predicate but reduce redundant fixed post-coverage dwell to a minimal camera-settle floor; compress only validation evidence motion where it does not weaken real normal-time CharacterMotor proof; and reuse the southern-ridge streaming location for the final overview where possible rather than paying another remote convergence cycle.

## Remaining gate
After the evidence-driver-only edit, self-review exact scope and cost, then freeze one source SHA and use the existing `ci-test/fixes/agent-6` transport for one fresh focused acceptance + 60-second built-player replay. Require all seven survey target frames plus macro-road capture, coverage-ready markers, visible four-town blockouts, readable lake/ridge/constrained route/network, normal-time CharacterMotor traversal, no runtime exceptions, and measured cost telemetry. Only after every task/acceptance is supported: complete the workflow/metadata transition required by `SceneIssues/README.md`, move only this assignment through pending to closed with fixed metadata, refresh/merge current master, verify the exact head, and non-force promote that exact head to `master`.