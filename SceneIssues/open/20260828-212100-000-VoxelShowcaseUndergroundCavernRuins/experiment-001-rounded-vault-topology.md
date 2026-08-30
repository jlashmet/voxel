# Experiment 001 — Rounded vault passage topology

## Hypothesis
The failed built-player corridor silhouette is caused by primitive topology, not sampling cadence or material selection. The generic cave route exposes a rectangular safety core; later cavern passes union vertical cylinders whose walls are vertical and caps are planar, so variable spacing alone cannot reliably hide the box/rib silhouette.

## Baseline discriminator
Exact run `33284693031` / source `492ea820...` was functionally green (38/38 route waypoints, zero harness assertions, 34,798,060 total writes, 4,416,056 naturalization writes, 8 total lights) but every useful underground frame still showed repeated vertical ribs/planar ceiling bands and the destination read as a rectangular masonry throat. That falsifies cadence-only, movement, removed-box, and capture-window explanations.

## Production change
On `fixes/agent-3`, introduce one deterministic rounded-vault profile whose clearance slices never shrink below a mathematically derived radius that covers the rectangular route core halfway between maximum-spaced nodes. Vary wall radius vertically and taper a crown to a one-voxel apex. Reuse the same profile for full-route naturalization, doglegs, and destination circulation. Preserve the generic cave core, public authoring API, renderer, material catalogue, camera, floor support, lights, and normal CharacterMotor path.

For cost, emit the stacked-disc profile as equivalent contiguous `FillColumnBulk` radial spans rather than slow per-voxel disc writes; geometry is unchanged while retaining the existing batched authoring path.

## Regression / expected result
Focused production tests require deterministic repeated profiles, multiple wall radii, tapered crown, worst-case adjacent-node cover of the rectangular core, normal WorldBuilder generation, normal CharacterMotor traversal, <=15M naturalization writes, <=55M total writes, and <=8 lights.

**Expected rendered discriminator:** the long descent no longer reads as repeated vertical cylinders or a rectangular hallway; the player reaches a large irregular dark cavern and the final composition clearly reads as aged ruin plus exactly two grounded flanking humanoid statues.

## Current status
Implementation compiles in the existing PR runner. That unsolicited broad PR run still stops on unrelated historical EditMode failures, so it is not acceptance evidence. Rendered result, exact write/render/FPS cost, and final disposition remain pending the single canonical exact-SHA targeted run.
