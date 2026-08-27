# Experiment 010 — Kentridge capture geometry rejection

## Hypothesis
The saved capture is looking at Kentridge-authored trees that are omitted from the runtime `TreeWorld`.

## Action
- Compared the exact saved capture camera (`z=154.718 m`, forward vector strongly toward +Z) against the authored Kentridge vegetation layout.
- Traced Kentridge's tree-capable vegetation candidates and their world bounds.
- Inspected `GalleryLifePopulation`, which independently publishes semantic `TreeInstance`s in a 46–150 m annulus around its gallery centre, as another possible scene-level tree source.

## Result
The Kentridge vegetation layout's northern authored belt ends around `z=116 m`. The saved camera is already north of that at `z=154.718 m` and looks further north, so the trees in the capture cannot plausibly be the Kentridge population in front of the player. Wiring Kentridge into `ShowcaseTreePopulation` would therefore be an unrelated speculative change.

`GalleryLifePopulation` does create real semantic trees and replaces the whole runtime `TreeWorld`, but its activation in the assigned `VoxelShowcase` scene has not yet been established.

## Conclusion
Rejected. Do not change Kentridge publication for this capture. Next trace whether `GalleryLifePopulation` is active in `VoxelShowcase`; if not, identify the wilderness/base-world tree producer north of the saved camera and connect that exact population to semantic damage/collision.
