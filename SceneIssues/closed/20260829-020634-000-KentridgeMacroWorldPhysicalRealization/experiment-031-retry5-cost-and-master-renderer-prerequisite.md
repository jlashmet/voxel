# Experiment 031 — Retry 5 cost and master renderer prerequisite

## Question
What Kentridge acceptance/cost evidence can be retained from Retry 5 without misclassifying the unrelated renderer module failure, and has the external renderer prerequisite changed?

## Exact source and CI ownership
- Targeted run: `33641059051`.
- CI transport head: `a0efab2841ee7175cea6e678b0fd30ee8b724f44`.
- That CI request commit is directly based on exact feature source `7e6d30858677f2504763e891289293c9507cfd9f`; its request targets `GenericBuildingBlockoutUsesBoundedFoundationAndWallShellsInsteadOfSolidVolumes`.
- Runner admission was healthy. Repository-derived persistent validation failed in 15 unrelated renderer/GPU EditMode tests before the process-isolated requested Kentridge PlayMode regression could execute. Therefore the focused plinth regression remains unvalidated on this source.

## Independent built-player evidence retained
The standalone SceneIssue replay still ran for 180 seconds because the supported workflow replays it under `always()`.

- Runtime catalogue remains macro-complete: 480 definitions with 4/4/4 Fairy, Orc, Moordell, and Rossdam settlement definitions plus 20 roads, ridge, and water.
- Moordell's four required content columns become content-ready at about 85 s. The pre-plinth exact replay required about 175 s, so this is about 90 s faster (~51% reduction in readiness time, ~2.06x faster convergence for this target).
- At Moordell readiness the validation-only residency diagnostic reports load radius 3, 29 horizontal columns, 31 total resident snapshot, 29 resident in-radius, `featureVerticalExtra=0`, `extraColumns=0`, and `maxExtraPerColumn=0`. This is useful partial blast-radius evidence, not final multi-target closure proof.
- `fps.txt` contains 143 one-second samples after t>=30 s: median FPS 103.9, mean FPS ~110.1, median mean-frame time 9.61 ms, median sampled p95 10.47 ms. Burst stalls remain severe: worst sampled frame 1172.43 ms. These measurements are provisional because the replay never reaches the remaining evidence targets.
- Renderer/far-field telemetry reports `coverage=False` through the replay. Full-frame timed captures visibly contain large unpublished/checkerboard surface holes (for example around 93.8 s, after Moordell content readiness). The strict published-coverage gate correctly prevents named acceptance captures from advancing.

## External prerequisite status
Current `origin/master` is `b18d470f66221c7cb6091249f4683c2d994bffec`, which merged the GPU renderer production-restoration work. Thus the renderer prerequisite is no longer unavailable on master. However this assignment's explicit coordinator sequence requires green exact-SHA gates before merging current master. The feature branch therefore cannot absorb the renderer restoration merely to make its stale renderer module baseline pass without violating the requested order.

Common `SceneIssues/README.md` normally permits merging master when compatibility requires it, but the assignment-specific instruction is stricter and is followed here. No renderer code/test is copied, cherry-picked, weakened, or otherwise modified by agent-6.

## Result
- The plinth change has a strong independent runtime throughput signal and no measured Moordell vertical-residency expansion.
- Visual settlement/geography acceptance remains unproven because production renderer publication is incomplete on the pre-merge branch.
- Focused requested-test validation remains blocked before execution by stale unrelated renderer modules.
- Do not issue another identical CI request, do not claim the plinth gate green, and do not merge/cherry-pick renderer work before the coordinator-prescribed gate order permits it.
