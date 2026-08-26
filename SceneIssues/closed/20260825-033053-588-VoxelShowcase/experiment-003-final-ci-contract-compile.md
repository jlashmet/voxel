# Experiment 003 — final CI contract compile

## Hypothesis
The finalized wood-only collision API and regression compile and pass unchanged on the exact current feature head.

## What was performed
Reset `ci-test/fixes/agent-6` to feature commit `15e446e274270d374b54123231a0f2fb32169268`, then requested `VoxelEngine.Tests.PlayMode.ShowcaseTreeInteractionRegressionTests` in PlayMode. Request commit: `136af23784e25ba25dd86b18dbfdb0c92ac2ca88`; workflow: `32927649874`.

## Result
**Failed before the requested test ran.** The VoxelShowcase startup bake hit compiler error CS0535: `ChainCombatVegetationV11Tests.RecordingTreeDamageService` no longer implemented the newly added `ITreeDamageService.OverlapsWoodAabb(float3, float3)` member.

## What was learned
The tree-collision design is not disproven; the interface expansion exposed one stale test double outside the showcase test. The feature cannot be considered verified until all implementers compile.

## Next
Add the missing no-op wood-overlap implementation to the combat test double, create a new feature head, then rerun the same targeted showcase interaction regression from that exact commit.
