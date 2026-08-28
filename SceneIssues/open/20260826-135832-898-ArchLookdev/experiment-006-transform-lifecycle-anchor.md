# Experiment 006 — transform lifecycle anchor

## Hypothesis
The world-space correction is correct, but render callbacks are the wrong lifecycle owner. A camera-local transform listener can anchor the hero root at the exact child-add boundary, independent of render-pipeline callback delivery and without per-frame polling.

## Action
Install `ArchReferenceGrowthWorldSpaceAnchor` on the Hero Arch camera around scene load. It anchors immediately if growth already exists and otherwise reacts to `OnTransformChildrenChanged`. The regression installs the same production listener before adding `ArchReferenceGrowth` and no longer calls `AnchorCamera` as a test-only repair.

## Result
The exact-SHA request for `f41ab02913e8bb62f2748e4a720cb2160326f3d0` failed at the regression's first `FindHeroRoot()` assertion, but the same run's standalone RealPlayer replay visibly rendered the detached ivy and flower meshes at the arch. The product lifecycle therefore crossed the construction boundary successfully. The regression was observing the detached `HideFlags.DontSave` root with `Object.FindObjectsByType`, which no longer returns that object once it leaves the camera hierarchy.

The replay also exposed the next visual discriminator: the broad mass is present, but the leaves read as sharp starbursts and the blossoms as repeated six-petal daisies rather than the reference's broad ivy and delicate pink clusters.

## Conclusion
Transform lifecycle anchoring is accepted as the production lifecycle. Correct the test's observation mechanism without adding product hooks, then isolate leaf/flower silhouette refinement as a new art experiment.

## Cost / blast radius
ArchLookdev only. One tiny camera component, no `Update`, no render callback, no per-frame mesh generation; existing 3-draw / <=4096-vertex hero budget remains unchanged.
