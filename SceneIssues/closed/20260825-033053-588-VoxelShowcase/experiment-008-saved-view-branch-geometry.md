# Experiment 008 — saved-view semantic branch geometry

## Hypothesis
Walking the generated branch geometry for all 36 authored semantic Showcase trees and testing it directly against the recorded camera frustum would identify a tree actually visible in the capture, avoiding the sparse-ray sampling limitation from experiment 007.

## What was performed
Synchronized `fixes/agent-6` with current `master`, then changed the capture-specific replay to frustum-test every generated `TreeBranchSegment`, sample visible points along candidate branches, and confirm the saved-camera ray through that point with the production `TreeDamageService.TrySweepImpact` path. Feature head: `b16ba6acbd038c07a1afbdc30bbf5044b1d793cf`. CI request commit: `6e9e7be21d0a5304a6e3c32e060224e75e6af6d7`. Workflow run: `32930629985`.

## Result
**Failed after executing exactly one PlayMode test.** VoxelShowcase loaded successfully and logged `Procedural vegetation: worldgen published 36 semantic Showcase trees`, but the replay failed with `No authored semantic branch geometry is visible and shootable from the saved camera view.` The player-overlap and damage assertions were not reached. The generic real-player screenshot step had no profile for this filter and was skipped.

## What was learned
**Hypothesis disproven.** The failure is no longer explained by sparse 250 m viewport sampling: the replay inspected the generated branch geometry itself against the recorded camera frustum. Under the saved camera fixture, the 36 `ShowcaseTreePopulation` semantic tree instances are not the tree geometry implicated by this capture. Continuing to tune selectors for that population would validate the wrong object set.

## Next
Inspect the original screenshot and trace every VoxelShowcase tree/vegetation producer outside `ShowcaseTreePopulation`. Identify the population that can render the captured trees while remaining absent from player collision and shot damage, then move the regression to that actual runtime path.
