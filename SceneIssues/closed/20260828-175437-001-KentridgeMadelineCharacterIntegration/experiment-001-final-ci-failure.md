# Experiment 001 — first final CI failure

**Hypothesis:** the ownership extraction is assembly-complete and the production acceptance plus scene-issue replay can validate source `19fd18cd794ffa63a1d6330cc320e83a545f2f0d`.

**Action:** targeted PlayMode request `49e3843dba4176d545c95fb6bd51b52c3dbd1fdf`, run `33213859697`, test `KentridgeOpeningProductionAcceptanceTests.RecoveredOpening_CompletesProductionCameraMovementDialogueAndStoryHandoff`, with the assigned Architecture SceneIssue supplied for replay.

**Result:** product failure before test execution. `Game.Composition.Kentridge.Playable` could not resolve extracted host dependencies because its asmdef omitted Campaign, WorldBuilderWorldGen and Showcase references. The real-player step independently rejected the assigned issue because `captures: []` provides no captured screen dimensions.

**Verdict:** hypothesis falsified. Add the actual host assembly dependencies. Do not invent capture dimensions for an Architecture issue; validate with the existing `KentridgePlayableScenePlayTests.*` harness profile, which builds the exact Kentridge player without capture metadata.

**Next:** run the production Kentridge playable acceptance after updating it to drive the new host and assert the authored Madeline production visual.
