# Plan

## Acceptance region
The capture has no marked circles, so the acceptance region is the visible opening ensemble. The reported defect is that the humanoid characters read as sunk through the pub floor; preserve the recovered opening camera and unobstructed pub interior while correcting their visible foot contact.

## Evidence and hypotheses
- `issue.json` records the production `KentridgePlayableSlice` at frame 4205 / 21.844 s from `Kentridge Player Camera` (1928x836, FOV 58), after the opening has had time to stage its cast.
- Opening stage points are realized from backend settlement geometry (`KentridgeCampaignWorldRealizer` -> `CutsceneStageRealizer`), so changing terrain or hard-coding pub Y would bypass the architecture-owned placement contract.
- `PlayerActor` and `NpcActor` currently copy those semantic stage positions directly into imported humanoid prefab roots. The code explicitly assumes those roots are authored at the soles; that assumption is not verified against renderer bounds.
- The fixed opening camera already has a captured-pose line-of-sight regression, making camera/occlusion a competing but already-covered explanation rather than the likely grounding source.

Discriminator: run the real opening and compare each initial participant's semantic stage-plane Y to the minimum enabled renderer bound. If the stage plane is correct but renderer bounds extend below it, fix presentation/root-to-feet alignment; only revisit world realization if the semantic stage plane itself is wrong.

## Fix / regression
Normalize each cutscene body's visual root-to-feet offset once from its renderer bounds, then apply that cached offset whenever the actor is placed or moved. Keep the motor/NPC semantic positions unchanged so story, collision, interaction, and camera calculations still use architecture-owned coordinates.

Add a PlayMode regression beside `KentridgeOpeningCameraReadabilityTests` that loads the production scene to the captured first dialogue beat and asserts Weldon, Madeline, and Steven renderer bottoms coincide with their semantic stage planes within a small tolerance. Log the measured offsets as repro evidence.

## Blast radius / cost
Scope is Kentridge cutscene presentation only. No voxel generation, stage realization, gameplay grounding, interaction distance, or global character prefab changes. Offset measurement happens once per cutscene body; per-frame movement adds only one cached scalar Y offset.