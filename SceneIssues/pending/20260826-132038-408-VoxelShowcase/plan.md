# Plan — 20260826-132038-408-VoxelShowcase

## Observed
One saved pose, **0 marked regions**. The whole replay frame is the defect area: continuous ground has a hard boundary between fine/dark grass and pale grass with visibly oversized blade/flower motifs. Acceptance: crossing that boundary must not change the physical scale of the reused grass artwork.

## Evidence / competing hypotheses
- **Far-terrain owner:** falsified by exact saved-pose replay; settled runtime reports `FAR hole=365.9m`, while the camera is ~2.4 m above local ground, so the photographed foreground is inside the far exclusion hole.
- **Missing/semantic near terrain:** falsified by stable replay coverage (`missingVisible=0`) and probes resolving the affected near surface to Grass/Moss presentation.
- **Near LOD world/voxel units:** weakened by both CPU HLOD and GPU extraction emitting world-metre positions into the same near shader.
- **Moss coating UV scalar:** corrected from `1/22` to `1/7`, but the native replay still showed the enlarged motifs; insufficient.
- **Moss coating duplicate texture sample:** disabled while preserving tint/response, but exact run `1acf1a8037b4ca997f110dba1b147ac10da3229c` still rendered the same hard scale boundary; falsified as the remaining owner.
- **Base Moss material density:** confirmed by the production catalogue. Grass uses `GrassTexture` through `StylizedTerrain` at `uvScale=1/7`. Base Moss reuses `GrassTexture` through `Textured`, whose existing default is `1/36`. The shared artwork therefore changes physical motif size at a Grass↔Moss material boundary even with the coating's own texture contribution disabled.

## Selected fix / regression
Expose the existing `Textured` UV scale as an optional authoring parameter and set only the Moss base-material row to `1/7`. Preserve Moss tint, texture blend, triplanar projection, normal strength, roughness, coating response, material identity, and geometry. Behavioral regression remains `VoxelEngine.Tests.PlayMode.GrassCoatingPresentationTests.GrassAndMossCoatingShareAuthoredTextureDensity`, strengthened to compare Grass and Moss base-material texture layer, UV density, and projection as well as requiring the moss coating's independent texture weight to remain zero.

## Blast radius / cost
One game-owned presentation parameter plus focused regression/evidence. Every other `Textured` material retains the existing `1/36` default. No storage/generation mutation, allocation, draw, shader instruction, texture copy, mesh rebuild, or per-frame CPU work.

## Remaining gates
Run the exact focused PlayMode regression and original 30-second saved-pose replay from one fresh source SHA. Inspect native-resolution `verification-final.png`; reject the candidate if the stretched-grass boundary remains. Only after that visual gate is clean should metadata be completed and the issue closed/merged.
