# Plan

## Acceptance region
No circles are marked, so acceptance is the full opening ensemble at the captured pub camera. Preserve the recovered camera/interior while keeping humanoid feet on the generated stage plane.

## Evidence / discriminator
- Capture: production `KentridgePlayableSlice`, frame 4205 / 21.844 s, 1928x836, `Kentridge Player Camera`, FOV 58.
- Stage points come from settlement geometry (`KentridgeCampaignWorldRealizer` -> `CutsceneStageRealizer`); terrain/Y hard-coding would bypass architecture-owned placement.
- Imported renderer bounds are independent of semantic actor roots used by story/collision/camera.
- Failed exact CI measured Weldon at -0.029 m after idle animation despite correct initial normalization, falsifying the bad-stage-plane hypothesis and proving a one-time visual correction goes stale.

## Fix / regression
Keep actor roots unchanged; reconcile only Kentridge visual-child offsets to current enabled renderer minima in `LateUpdate`, after animation. Development replay also emits a clean camera-only 1928x836 verification frame; release gameplay is unchanged.

Regression: `VoxelEngine.Tests.PlayMode.KentridgeOpeningGroundingTests.InitialOpeningCastRendererFeetRestOnSemanticStagePlane` loads the production opening and checks Weldon, Madeline, and Steven against their semantic stage roots without weakening the 0.025 m threshold.

Final source `3768f011b2d80069c8783694ae1c8179e6f6c4b9`; exact request `13b39e1c23d20c59375fe2e0d2221d28f64252b4`, run `33120343242`: success. Final runtime deltas: Weldon 0.000 m, Madeline 0.000 m, Steven 0.000 m. Green replay produced clean native 1928x836 `verification-final.png`.

## Blast radius / cost
Kentridge cutscene presentation only: no voxel generation, stage realization, gameplay grounding, interactions, camera math, or shared character prefabs. Cost is one scene-root scan plus renderer-bounds reconciliation per active humanoid per `LateUpdate`; the cast is small and no physics/story coordinates move.
