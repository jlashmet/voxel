# Plan

## Observed defect / acceptance
`KentridgePlayableSlice` directly constructed and ticked `VoxelEngine.Characters.Runtime.CharacterMotor`, owned its `IKentridgeCampaignActorHost` implementation plus player/NPC cutscene motion/animation/model selection, and a Kentridge-only component owned generic visual foot grounding. `Game.Kentridge.PlayableSlice.asmdef` therefore referenced `VoxelEngine.Characters.Runtime`. Madeline was selected through the placeholder cutscene-body path even though commit `f609fb8e76a14b5673ddfa79eef63a1b01726f3f` added the production `Assets/Resources/Characters/Madeline/Madeline.prefab` (model GUID `f593982524b374c80b946d9e4670471d`, Humanoid import, material remap, shared animation clips).

Acceptance: the scene chooses scenario/spawn/input settings only; a Game-owned runtime implements the campaign actor-host seam and owns motor/cutscene actors/model binding/animation/grounding. Madeline resolves through that runtime to the production prefab. The opening cutscene and gameplay handoff remain usable in the built player.

## Hypotheses / discriminator
1. **Falsified:** a complete reusable Game-owned actor host already existed and Kentridge merely bypassed it. Audit found only `IKentridgeCampaignActorHost`; no reusable implementation owned construction/presentation.
2. **Selected and implemented:** the lower-level seams existed, but Kentridge filled the missing implementation locally. The authoritative motor/cutscene/model/animation behavior now lives in `Game.Composition.Kentridge.Playable.KentridgeCharacterHost`.

Falsifier: if moving the same authoritative motor/cutscene behavior behind the Game-owned host changes opening choreography, player handoff, actor positions, or scene startup, ownership was not isolated cleanly.

## Implementation
- [x] Extract player motor, campaign actor host, NPC/player cutscene actors, model selection and locomotion animation policy from the scene runtime into the Game-owned playable composition module.
- [x] Move generic visual foot grounding into `CharacterVisualFootGrounding`; preserve the former scene-script GUID so the checked-in scene cannot gain a Missing Script component.
- [x] Resolve only authored Madeline identities to `Resources/Characters/Madeline/Madeline`; other identities retain their existing placeholder fallback.
- [x] Keep the imported Madeline prefab/Animator/skeleton hierarchy intact under an outer actor visual root so grounding cannot invalidate animation binding paths.
- [x] Keep movement/cutscene transforms authoritative and force production Animator root motion off; drive Idle/Walk through the controller when placeholder-specific `CharacterAnimationPolicy` is absent.
- [x] Remove `Game.Kentridge.PlayableSlice`'s direct `VoxelEngine.Characters.Runtime` assembly dependency and delegate scene movement/actor queries/ticking to the Game-owned host.

## Regression / blast radius / cost
`KentridgeOpeningProductionAcceptanceTests.RecoveredOpening_CompletesProductionCameraMovementDialogueAndStoryHandoff` now verifies the real production scene owns one shared Game character host, the authored Madeline campaign registry entry resolves to a Humanoid production Animator/controller with root motion disabled and reusable grounding, Weldon/Logan movement remains non-teleporting, all recovered dialogue and fixed-camera choreography remain intact, and gameplay returns at the architecture-owned pub handoff.

Blast radius is limited to Kentridge playable character composition, its scene-runtime seam, generic character visual grounding, and the existing Kentridge production acceptance. Other character placeholder resolution is unchanged. Cost remains one existing character-host tick plus one production prefab instance for Madeline; no additional world, streaming, or scene-local per-frame subsystem was introduced.

## Verification gates
- [x] Merged current `master` into `fixes/agent-4` after implementation and regression changes; latest incoming master delta was unrelated SceneIssue metadata.
- [ ] Exact-SHA targeted PlayMode: `VoxelEngine.Tests.PlayMode.KentridgeOpeningProductionAcceptanceTests.RecoveredOpening_CompletesProductionCameraMovementDialogueAndStoryHandoff`.
- [ ] Same exact-SHA CI request must include `scene_issue=SceneIssues/open/20260828-175437-001-KentridgeMadelineCharacterIntegration/issue.json`, causing the workflow to build/run the real `KentridgePlayableSlice` player and upload visual/runtime evidence.
- [ ] Inspect CI result/artifact; only green test + successful real-player replay may move the issue to pending/closed.
- [ ] Record final metadata, merge any newly advanced `master`, and non-force publish the exact fixed branch head to `master`.
