# Experiment 003 — SRP-native world anchor

## Hypothesis
The remaining bare standalone replay is not mesh generation or shader availability; it is the delivery mechanism for the world-space correction. The PlayMode regression manually invokes `ArchReferenceGrowthWorldSpace.AnchorCamera`, while the real URP player depends on legacy `Camera.onPreCull`. If that callback is not delivered on this Scriptable Render Pipeline camera path, the hero root stays parented to the moved camera and its world-authored vertices are displaced off the arch.

## One change
Replace the legacy `Camera.onPreCull` delivery with URP/SRP-native `RenderPipelineManager.beginCameraRendering`. Keep the exact same one-shot `AnchorCamera` operation and unsubscribe after the first successful hero-camera anchor. No mesh, shader, density, color, camera-pose, or scene-geometry constants change.

## Evidence that discriminates this hypothesis
- The exact-SHA PlayMode regression passes when it manually calls the helper, proving the detach/identity transform itself is valid.
- The same exact-SHA standalone replay is visually bare, proving that helper math alone is insufficient in the real lifecycle.
- The player build log compiles `VoxelEngine/ProceduralVegetationFoliage`, and `BuildHeroPresentation` uses that exact shader name, rejecting shader lookup/stripping as the leading explanation.
- The project is URP (`RenderPipeline=UniversalPipeline` in the foliage shader), so SRP's camera-render callback is the appropriate lifecycle boundary to test.

## Validation
1. Existing regression must continue proving the captured-camera ownership, world-identity anchor, authored representation, lifecycle restoration, and cost bounds.
2. Exact-SHA targeted PlayMode CI must pass.
3. Original saved Hero Arch replay must visibly show the authored ivy and flower mass on the masonry. A green test with a bare replay fails the experiment.
4. Final clean evidence must meet or exceed the original capture dimensions (1928x836) before closure.
