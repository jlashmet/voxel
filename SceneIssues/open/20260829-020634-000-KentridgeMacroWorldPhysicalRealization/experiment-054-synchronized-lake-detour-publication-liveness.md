# Experiment 054 — synchronized lake-detour publication liveness

## Exact source / request
- Current master: `b56436f198702fa91bfaccec69a30df25a52a2cd`
- Feature source: `0169a1eeeed943eec88667d23996420a11cea867` (0 behind current master)
- CI transport: `7ce512c0383d758ea470c162ca3d1fdc89f87693`
- Workflow run: `34200648642`
- Artifact: `single-test-34200648642` (`10046811045`)

## Result
The synchronized source keeps persistent repository tests and the explicitly requested macro-world PlayMode regression green. The repository-derived player gate is product-red because `KentridgeMacroWorldValidation.player-scenario.json` requires `MACROEVIDENCE capture-ready target=macro-network-overview`, and the 180-second focused player never reaches that final target. The focused harness itself finishes with zero assertion failures and no forbidden runtime exception pattern.

The current-master synchronization materially advances acceptance beyond experiment 053. Both the focused Kentridge player and the full SceneIssue replay now request real Application New Game, execute local and macro-road `CharacterMotor` traversal, strictly capture Moordell, capture the road arrival, strictly capture Rossdam, and then enter the authored `rossdam-lake-detour` target. The full SceneIssue replay also completes its 180-second harness step normally.

## New first incomplete phase
`rossdam-lake-detour` content becomes settled in both players, but strict production GPU publication does not converge afterward:

- Full SceneIssue player: lake content becomes ready at about `t=160.7s`. At that transition the renderer reports `missingVisible=387`, `pub=3691`, `noSlot=128147`. At the end of the replay it reports `missingVisible=391`, `pub=3693`, `noSlot=131936`, and oldest in-flight GPU work aged to about `32.2s`.
- Focused module player: lake content becomes ready at about `t=170.7s`. At that transition it reports `missingVisible=386`, `pub=3296`, `noSlot=127817`. At the end it reports `missingVisible=391`, `pub=3297`, `noSlot=129153`, and oldest in-flight GPU work aged to about `33.6s`.

This is not the old survey-demand mismatch. `KentridgeMacroWorldEvidenceDriver.PinToTargetDemand` explicitly places the CharacterMotor streaming authority at the same resolved survey camera point used by rendering, and the synchronized branch has no Rendering production diff. The same cold-view publication symptom occurs in the focused and full application paths, while content settlement succeeds and publication is essentially flat under sustained `NoSlot` pressure.

## Classification / next prerequisite
The remaining acceptance blocker is shared production GPU publication liveness at the next cold macro viewpoint, not an agent-6 WorldBuilder/Kentridge orchestration defect demonstrated by this run. Current `master` remains `b56436f198702fa91bfaccec69a30df25a52a2cd`; it contains no newer production renderer correction to reconcile.

Keep this SceneIssue open. Do not weaken `HasCompletePublishedNearSurfaceCoverage`, increase renderer/residency budgets, force generation, add a Kentridge-only renderer workaround, or start another SceneIssue from agent-6. Re-run this same macro acceptance after the shared renderer can publish a relocated cold view under the existing budgets; only then continue to Fairy/Orc/ridge/network captures and final cost/visual closure gates.
