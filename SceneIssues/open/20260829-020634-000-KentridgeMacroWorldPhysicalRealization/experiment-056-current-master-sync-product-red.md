# Experiment 056 — current-master sync and exact product failure

## Inputs
- Prior-assignment source: `abff52bba0c04c3940d98d6c24ebab2b081258ec`.
- Exact CI transport: `522fe183329e6ad645664e4bf1f88515b51ae5dc`.
- Workflow run: `34213075572`.
- Artifact: `single-test-34213075572`, id `10051977715`.
- Current master after the run: `5c9a715a42735a0b573f436af1e3700dff2f591c` (`Mounting Force town art direction source of truth (#335)`).
- Clean two-parent reconciliation merge: `0be3434b5111d9284c2b02a2586675e82ce35b66`.

## Result
The prior module-validation compile-boundary correction worked: repository module tests and the requested macro-world regression complete before player validation. The module runner then builds and runs the real `KentridgeMacroWorldValidation` player for 180 seconds.

The first required player failure remains:

`ERROR: required player-log pattern missing: MACROEVIDENCE capture-ready target=macro-network-overview`

The player reaches real Application New Game, CharacterMotor traversal, Moordell evidence, Rossdam evidence, and the `rossdam-lake-detour` phase, but does not complete strict publication/readiness far enough to reach the required network overview within the acceptance run. The separate 180-second SceneIssue replay also builds and runs to completion; that does not override the failed required module-player assertion.

## Classification
This is a product/acceptance failure, not runner infrastructure. Current master adds only the unrelated town-art-direction closure and does not contain a production Rendering correction for the demonstrated cold-view GPU publication liveness prerequisite. Therefore a same-cause retry on the reconciled head is not authorized by the CI rules.

Keep this SceneIssue open. Do not weaken coverage semantics, widen residency, raise budgets, force generation, add a Kentridge-only renderer workaround, or create another SceneIssue. Re-run exact-SHA CI only after a relevant product correction is present on this feature branch/current master; then require every repository-derived player plus the full macro replay to pass before closure and PR promotion.
