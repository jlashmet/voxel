# Experiment 001 — trace semantic tree interaction paths

## Hypothesis
The visible VoxelShowcase trees were migrated to the semantic vegetation runtime, but one or more gameplay paths still query only voxel storage. That would explain the capture reporting both player pass-through and shots that do not visibly break trees.

## Method
Traced the current feature branch from population through interaction and presentation:

- `ShowcaseTreePopulation` and `GalleryLifePopulation` publish `TreeInstance` snapshots through `VegetationComposition.ReplaceTreeWorld`.
- `VegetationComposition` forwards that snapshot into `TreeWorldRuntime`; `TreeDamageService` forwards to `ProceduralTreeDamageService`.
- `VoxelShowcase.TryTornadoImpact` already calls `TreeDamage.TrySweepImpact`; `StepTornadoes` calls `ApplyBlast` for semantic hits.
- `ProceduralTreeRenderer` subscribes to branch-cut/damage events and removes cut branch geometry.
- `CharacterMotor.IsBlocked`, however, reads only `world.SurfaceQuery` voxel cells for every horizontal/vertical movement probe. It never queries the semantic tree collision/damage capability.
- Checked population lifecycle: `ShowcaseTreePopulation` publishes once and disables itself, so damage is not being immediately erased by per-frame republishing.

## Result
**Confirmed regression for player collision.** A tree can be present in the authoritative semantic tree world and rendered while being invisible to `CharacterMotor`, because the motor's blocking test is voxel-only.

The shooting chain is present end-to-end in current source, so there is no evidence for a missing registry bridge or renderer invalidation. The safe production change is to make semantic tree wood collision a first-class query and wire the motor to it, then protect the existing shot/damage chain with a deterministic regression rather than rewriting it speculatively.

## Next
Add a wood-only sweep query to the semantic tree interaction capability (so foliage does not become a solid wall), use it from `CharacterMotor`, and add a regression that proves both a player-sized sweep is blocked by trunk/branches and a representative shot mutates tree damage state.
