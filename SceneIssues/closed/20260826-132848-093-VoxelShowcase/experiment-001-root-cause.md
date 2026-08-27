# Experiment 001 — water-data loss vs presentation-state leak

## Question
Did VoxelShowcase lose its water data/render path, or is an intact water path being suppressed by persistent diagnostic presentation state?

## Evidence
- `ShowcaseWorld` still registers the semantic water material.
- The production `VoxelRenderPass` still gates and draws water geometry.
- `VoxelRenderBridge.WaterRenderEnabled` is a mutable static defaulting true and was introduced as a diagnostic A/B switch.
- `ShowcasePlayerHarness` explicitly sets that switch false for `-voxel-disable water`.
- `VoxelShowcase` restores other persistent rendering globals when entered but never restores the water switch.

## Result
Hypothesis 2 is the smallest supported cause: a false diagnostic switch can survive into a normal Showcase presentation and suppress all water despite intact generation/meshing/render code. The fix should restore the production default at the VoxelShowcase scene boundary without removing the diagnostic switch or changing unrelated scenes.
