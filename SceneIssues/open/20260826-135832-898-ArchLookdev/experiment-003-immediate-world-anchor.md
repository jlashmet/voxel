# Experiment 003 — immediate world anchor

## Hypothesis
The remaining bare standalone replay is not mesh generation or shader availability; it is the delivery mechanism for the world-space correction. The PlayMode regression manually invokes `ArchReferenceGrowthWorldSpace.AnchorCamera`, while the real URP player depends on `Camera.onPreCull`. If that callback is not delivered for this camera/render path, the hero root stays parented to the moved camera and its world-authored vertices are displaced off the arch.

## One change
Anchor `Arch Reference Hero Growth` to world identity immediately at the end of `BuildHeroPresentation`, after its three mesh children exist. Remove the render-time callback/scene-hook dependency; keep the anchoring helper as a synchronous operation only.

## Evidence that discriminates this hypothesis
- The exact-SHA PlayMode regression passes when it manually calls the helper, proving the detach/identity transform itself is valid.
- The same exact-SHA standalone replay is visually bare, proving that helper math alone is insufficient in the real lifecycle.
- The player build log compiles `VoxelEngine/ProceduralVegetationFoliage`, and `BuildHeroPresentation` uses that exact shader name, rejecting shader lookup/stripping as the leading explanation.

## Validation
1. Regression must observe the root already detached and world-identity immediately after `ArchReferenceGrowth` is enabled; no manual anchor call may be required.
2. Exact-SHA targeted PlayMode CI must pass.
3. Original saved Hero Arch replay must visibly show the authored ivy and flower mass on the masonry. A green test with a bare replay fails the experiment.
4. Final clean evidence must meet or exceed the original capture dimensions (1928x836) before closure.
