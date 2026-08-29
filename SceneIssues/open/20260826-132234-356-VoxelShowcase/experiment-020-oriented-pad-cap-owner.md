# Experiment 020 — oriented generated-pad cap owns the upper rectangle

## Hypothesis
The surviving upper Dirt/grass defect is not the generated-house support footprint itself; it is the visible rectangular Moss cap at the rotated MayorHouse pad corner. Rounding only that material owner should remove the 90-degree grass tongue without moving or weakening the house foundation.

## Runtime discriminator
Workflow `33226248513` tested feature source `ecec9b71119ff2dfbe250c5d8b6d7d994193e11b`. The focused regression passed, the workflow freshly baked `ShowcaseWorld.bytes`, built the real VoxelShowcase player, replayed the exact 1928×836 saved camera, and reached stable residency. Direct inspection of `RealPlayer/verification-final.png` with the original capture circles overlaid still shows a hard 90-degree green corner in the upper mark. Pixels inside both marks are byte-for-byte unchanged from rejected workflow `33225240544`, so the prior generated-foundation shrink is visually falsified as sufficient.

## Source / geometry discriminator
The old regression subtracted MayorHouse's placement origin and tested the pad program as though orientation were zero. Production passes `plot.Frontage` into `ShapeProgram`, and MayorHouse uses orientation `2` inside the `132×132dm` WideHouse envelope. Its generated `98×86dm` pad at local `(17,10)dm` therefore rotates to world `X=927..1024dm, Z=286..371dm`. The corrected camera-ray upper envelope is approximately `X=910..938dm, Z=286..304dm`; the pad's southwest top corner `(927,286)dm` lands exactly inside it. This explains the false green regression and the unchanged replay.

## Change
For organic Kentridge generated houses only, keep the role-specific rectangular support and clearance exactly where they are. Fill Dirt through the existing surface voxel, then restore Moss with a `12dm`-radius rounded-box `PaintSurface` cap. `PaintSurface` changes material only and creates no occupancy. Bespoke/non-generated and legacy pads keep the existing rectangular top Fill. Round organic route stamps remain unchanged.

## Behavioral regression
The exact-seed PlayMode test evaluates the production MayorHouse definition through `ShapeProgram` with its real orientation. It requires three primitives total, verifies Dirt support remains `X=927..1024dm, Z=286..371dm` with top `Y=221dm` and unchanged clearance above, then verifies the Moss primitive is a radius-12 rounded `PaintSurface`: the captured corner `(927,221,286)` and a near-corner sample are excluded while interior and both tangent samples remain included. The route half of the test continues to require equal-width round carve/fill stamps.

## Blast radius / cost
No changes to support occupancy, house placement, route topology, surface elevation, or primitive count. Only visible material ownership on organic generated-house caps changes. VoxelShowcase is prebaked, so there is no per-frame cost; generation adds bounded surface reads over the existing house-pad footprint and writes fewer Moss voxels at the four corners.

## Falsifier / acceptance gate
Run one final exact-SHA targeted request with a forced showcase bake and built-player saved-pose replay. Both immutable marked circles must be visually free of metre-scale right-angle/stair-step Dirt/grass contacts. A green test without that visual result is a rejection, not closure.
