# Plan — 20260826-132038-408-VoxelShowcase

## Observed
One saved pose, **0 marked regions**. The whole replay frame is the defect area: continuous ground has a hard boundary between fine/dark grass and pale grass with visibly oversized blade/flower motifs. Acceptance: crossing that boundary must not change the physical scale of the reused grass artwork.

## Evidence / competing hypotheses
- **Far-terrain owner:** falsified by exact saved-pose replay; settled runtime reports `FAR hole=365.9m`, while the camera is ~2.4 m above local ground, so the photographed foreground is inside the far exclusion hole.
- **Missing/semantic near terrain:** falsified by stable replay coverage (`missingVisible=0`) and probes resolving the affected near surface to Grass.
- **Near LOD world/voxel units:** weakened by both CPU HLOD and GPU extraction emitting world-metre positions into the same near shader.
- **Moss UV scalar only:** partially confirmed then falsified as sufficient. Grass uses texture layer 5 at `uvScale=1/7`; moss had been `1/22`, but the native final replay after correcting it to `1/7` still showed the oversized right-side motifs.
- **Second near texture policy:** confirmed by the production shader and the failed visual gate. Base Grass first reconstructs the authored texture through its material presentation; moss then independently samples layer 5 again and replaces most of that result. The duplicated sample is the remaining boundary owner.

## Selected fix / regression
Keep moss tint, blend, response, material identity, layer metadata, and UV metadata unchanged, but set its independent texture weight to zero. Moss therefore remains a presentation tint over the already-rendered Grass instead of becoming a second grass-texturing path. Behavioral regression: `VoxelEngine.Tests.PlayMode.GrassCoatingPresentationTests.GrassAndMossCoatingShareAuthoredTextureDensity` requires shared layer/density metadata and requires moss independent texture weight to remain zero.

## Blast radius / cost
One renderer-owned coating presentation scalar plus the focused regression. No storage/generation mutation, allocation, draw, shader instruction, texture copy, mesh rebuild, or per-frame CPU work. Existing moss blend/noise/orientation/roughness remain unchanged; only the duplicate grass texture contribution is removed.

## Remaining gates
Run the exact focused PlayMode regression and original 30-second saved-pose replay from one fresh source SHA. Inspect native-resolution `verification-final.png`; reject the candidate if the stretched-grass boundary remains. Only after that visual gate is clean should metadata be completed and the issue closed/merged.
