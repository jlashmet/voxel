# Experiment 039 — survey-alignment regression compile boundary

## Exact request
- CI run: `33811917743`
- transport: `f3f2f0dfd174488bc0196c0ee283a20f57af5b2f`
- exact feature source: `a0f9998b755ab9bd467c05a2b1c478598ec84734`
- requested test: `VoxelEngine.Tests.PlayMode.KentridgeMacroWorldSurveyStreamingAlignmentTests.ElevatedSurveyPinsStreamingDemandToRenderedCameraBeforeSliceStreaming`
- SceneIssue replay request: 180 seconds

## Result
This source is test-harness compile red and provides no product/runtime signal.

Both the persistent module-validation Unity invocation and the standalone player build stop on the same compiler error before the selected Kentridge survey-alignment product path can execute:

`Assets/Tests/PlayMode/KentridgeMacroWorldSurveyStreamingAlignmentTests.cs(39,32): error CS0246: The type or namespace name 'Int2' could not be found`

The test imported neither the namespace that owns `MountingForce.WorldGen.Int2` nor a fully-qualified type. Module validation therefore reports `Scripts have compiler errors`; the player build fails for the identical reason four seconds after launch. There is no NUnit result and no built-player replay/capture to interpret.

## Correction
Add only `using MountingForce.WorldGen;` to the focused regression. The production evidence-driver alignment code is unchanged. This is not eligible for a same-SHA rerun because the source itself cannot compile; submit the corrected exact feature head through the same `ci-test/fixes/agent-6` transport after the completed run.
