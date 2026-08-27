# Experiment 006 — transform lifecycle anchor

## Hypothesis
The world-space correction is correct, but render callbacks are the wrong lifecycle owner. A camera-local transform listener can anchor the hero root at the exact child-add boundary, independent of render-pipeline callback delivery and without per-frame polling.

## Action
Install `ArchReferenceGrowthWorldSpaceAnchor` on the Hero Arch camera around scene load. It anchors immediately if growth already exists and otherwise reacts to `OnTransformChildrenChanged`. The regression installs the same production listener before adding `ArchReferenceGrowth` and no longer calls `AnchorCamera` as a test-only repair.

## Falsifier
Exact-SHA CI may pass structurally, but the experiment fails unless the original saved-pose standalone replay visibly contains the authored ivy/flower mass. If the root is now visible but buried in masonry, depth/occlusion becomes the next isolated variable.

## Cost / blast radius
ArchLookdev only. One tiny camera component, no `Update`, no render callback, no per-frame mesh generation; existing 3-draw / <=4096-vertex hero budget remains unchanged.
