# Experiment 038 — elevated survey streaming alignment

## Trigger
Exact-SHA run `33804821454` on feature source `50bc9533b811ac587d24503844d3d2c73d0c37bf` passed the requested `KentridgeStreamingCoveragePolicyTests.RadiusThreeDoesNotPromiseNominalThreeRegionMetricDisk`, repository-derived module validation, and the 180-second standalone player process. The requested radius fix is active in the player: the renderer reports `step1[0-57.6m]`, `step2[57.6-102.4m]`, with step4/step8 collapsed at `102.4m`.

The SceneIssue acceptance symptom nevertheless remained: evidence reached Moordell content-ready but never reached `capture-ready`, so Rossdam/Fairy/Orc/lake/ridge/network evidence was not produced.

## Discriminator
Compare renderer diagnostics immediately before and after the macro evidence driver enters the first elevated settlement survey.

- Immediately before `MACROEVIDENCE target=moordell ... cameraHeightM=70.0`, the renderer reports `missingVisible=0`, `coreAbsent=0`, with populated fine/coarse near rings.
- On the first survey diagnostic, `coreAbsent` jumps to `2178`; on the next it reaches `2922`. Missing visible chunks then grow to hundreds and strict coverage remains false through the end of the replay.
- During the stalled survey, the GPU extraction state remains admission-bound (`phases=0x2`) while the oldest request ages into tens of seconds. This is not a geometry-arena capacity failure: CPU arena lease failures remain zero, and `batchArenaWait` is the extraction-fence backpressure counter rather than an allocation failure counter.

The transition is owned by validation composition. `KentridgeMacroWorldEvidenceDriver.PinToTargetDemand` pinned the authoritative CharacterMotor/ShowcaseWorld streaming demand to the settlement focus on the ground, while `ApplySurveyCamera` placed the actual renderer camera roughly 70 m above the survey camera point. By contrast, the shipped `KentridgePlayableSlice.StepAutoSurvey` already keeps `CharacterMotor` at `camera - eyeHeight`, so the world streamer and renderer consume the same 3D demand point.

## Root cause
The macro evidence automation violated the scene's existing survey streaming contract. Storage residency followed the ground-level CharacterMotor while near-surface rendering followed the elevated camera. The strict renderer correctly refused to classify non-resident GPU core coverage as complete; narrowing the metric ring alone could not make mismatched 3D demand centres converge.

This is a validation/composition defect, not a reason to weaken `RenderingComposition.HasCompletePublishedNearSurfaceCoverage`, increase the Kentridge load radius, change device/scheduler budgets, or modify shared GPU publication semantics.

## Selected fix
Keep the fix inside `KentridgeMacroWorldEvidenceDriver`:

1. Give the validation driver an explicit negative `DefaultExecutionOrder` so its demand pose is established before the playable slice executes streaming. This removes dependence on incidental MonoBehaviour ordering.
2. Resolve one survey camera world position from the authored camera DM coordinate + terrain height + survey height.
3. Pin `CharacterMotor.Position` to `surveyCamera - eyeHeight` before the playable slice streams, matching the existing `StepAutoSurvey` contract.
4. Reuse the exact same resolved camera position for the rendered survey camera.
5. Add `KentridgeMacroWorldSurveyStreamingAlignmentTests` to prove the explicit execution ordering and that `CharacterMotor.EyePosition`, renderer camera position, and `ShowcaseWorld.RegionAt(...)` coincide for an elevated survey.

No shared renderer/world API, horizontal load radius, generation budget, device policy, far-field streamed radius, or strict coverage condition changes.

## Next gate
Run the new focused survey-streaming alignment regression on the exact feature SHA through `ci-test/fixes/agent-6` with the required 180-second SceneIssue replay. Require the player to eliminate the survey-owned nonresident-core stall and advance beyond Moordell before interpreting screenshots or proceeding to another fix.
