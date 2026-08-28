# Plan

## Observed defect / acceptance
`KentridgePlayableSlice` directly constructs and ticks `VoxelEngine.Characters.Runtime.CharacterMotor`, owns its `IKentridgeCampaignActorHost` implementation plus player/NPC cutscene motion/animation/model selection, and a Kentridge-only component owns generic raycast foot grounding. `Game.Kentridge.PlayableSlice.asmdef` therefore references `VoxelEngine.Characters.Runtime`. Madeline is selected through the placeholder cutscene-body path even though commit `f609fb8e76a14b5673ddfa79eef63a1b01726f3f` added the production `Assets/Resources/Characters/Madeline/Madeline.prefab` (model GUID `f593982524b374c80b946d9e4670471d`, Humanoid import, material remap, shared animation clips).

Acceptance: the scene chooses scenario/spawn/input settings only; a Game-owned runtime implements the campaign actor-host seam and owns motor/cutscene actors/model binding/animation/grounding. Madeline resolves through that runtime to the production prefab. The opening cutscene and gameplay handoff remain usable in the built player.

## Hypotheses / discriminator
1. **Falsified:** a complete reusable Game-owned actor host already exists and Kentridge merely bypasses it. Audit found only `IKentridgeCampaignActorHost`; no reusable implementation owns construction/presentation.
2. **Selected:** the lower-level seams exist, but Kentridge filled the missing implementation locally. Extract that implementation to Game composition/character runtime and bind Madeline there.

Falsifier: if moving the same authoritative motor/cutscene behavior behind the Game-owned host changes opening choreography, player handoff, actor positions, or scene startup, ownership was not isolated cleanly.

## Fix / regression / cost
Add a reusable Game-owned Kentridge character host using the existing `CharacterMotor` and campaign actor contracts; move generic actor model/animation/grounding policy out of `Assets/Scenes/Kentridge`; map only Madeline to `Resources/Characters/Madeline/Madeline`, leaving other placeholders unchanged. Remove the scene assembly's direct Characters.Runtime dependency and make the slice delegate movement/actor queries/ticking to the host.

Behavioral regression: instantiate the production host, prepare Kentridge actors, prove Madeline uses prefab GUID `346db891eb674f52a9ddb0d18fd5ef74` / model GUID `f593982524b374c80b946d9e4670471d`, verify humanoid Animator/material/root-motion policy and actor movement through the host, then exercise opening-cutscene-to-gameplay handoff through the real slice/session.

Blast radius: KentridgePlayableSlice + reusable Game character composition only; other character placeholders unchanged. Cost is ownership relocation plus one existing prefab instance per Madeline; no new per-frame systems beyond the actor tick already paid today.

Remaining gates: production/test implementation → exact-SHA targeted PlayMode CI → exact-SHA built `KentridgePlayableSlice` player launch and visual inspection → pending/closed bookkeeping → merge current master and non-force push.