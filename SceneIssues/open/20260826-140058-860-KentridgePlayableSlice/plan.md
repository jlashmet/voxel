# Plan

## Acceptance region
No circles are marked, so acceptance is the visible opening ensemble at the captured pub camera. Preserve the recovered camera/interior while keeping humanoid feet on the generated stage plane.

## Evidence / discriminator
- Capture: production `KentridgePlayableSlice`, frame 4205 / 21.844 s, 1928x836, `Kentridge Player Camera`, FOV 58.
- Stage points come from settlement geometry (`KentridgeCampaignWorldRealizer` -> `CutsceneStageRealizer`); changing terrain or hard-coding pub Y would bypass architecture-owned placement.
- Imported humanoid renderer bounds are independent of the semantic actor root used by story/collision/camera.
- Exact targeted CI proved the semantic stage root stayed at Y=21.900. Weldon first normalized from rendererMinY=21.897 to 21.900, but after the idle animator advanced the renderer minimum became 21.871 (delta -0.029 m). That falsifies a bad stage-plane hypothesis and shows the one-time visual correction becomes stale under animation.

## Fix / regression
Keep each actor root unchanged. Put only its visual children under a Kentridge-only offset and reconcile current enabled renderer minimum to semantic root Y in `LateUpdate`, after animation. Do not weaken the 0.025 m acceptance threshold.

Behavioral regression: `VoxelEngine.Tests.PlayMode.KentridgeOpeningGroundingTests.InitialOpeningCastRendererFeetRestOnSemanticStagePlane` loads the production scene to the first opening dialogue beat and checks Weldon, Madeline, and Steven renderer bottoms against their semantic stage roots.

## Blast radius / cost
Kentridge cutscene presentation only: no voxel generation, stage realization, gameplay grounding, interactions, camera math, or shared character prefabs. Cost is one scene-root scan plus renderer-bounds reconciliation per active humanoid per `LateUpdate` in this playable slice; the opening cast is small and no physics/story coordinates move.
