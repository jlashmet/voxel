# Experiment 004 — exact-head interaction regression

## Hypothesis
After repairing the stale `ITreeDamageService` test double, the focused interaction regression passes on the exact feature head and proves both wood-only player collision and semantic tree damage.

## What was performed
Reset `ci-test/fixes/agent-6` to feature commit `b3418ca27bf3755296ffedc8d6f1d3931df8bc51` and requested `VoxelEngine.Tests.PlayMode.ShowcaseTreeInteractionRegressionTests`. Request commit: `a36b130c3774d4290443b485f20e9f6829d4317e`; workflow: `32927755132`.

## Result
**Passed.** `ci/single-test` completed successfully. The uploaded `single.xml` contains exactly three passing cases: `RepresentativeTreeBlocksPlayerSizedWoodAabb`, `RepresentativeTreeShotCutsAndMarksDamage`, and `ShowcaseMotorAndProjectileUseSemanticTreeInteractionPath`.

## What was learned
The final semantic API compiles across the project, authored semantic wood blocks a player-sized AABB without reusing leaf-sensitive projectile collision, and the existing semantic damage path removes branches and marks a representative tree severed.

## Next
Replay the original saved VoxelShowcase camera pose in a scene-specific regression and prove the same contracts against an actual tree populated by the captured scene before closing the issue.
