# Plan — 20260826-132038-408-VoxelShowcase

## Observed
One saved pose, **0 marked regions**. The whole replay frame is the defect area: continuous ground has a hard boundary between fine/dark grass and pale grass with visibly oversized blade/flower motifs. Acceptance: crossing that boundary must not change the physical scale of the reused grass artwork.

## Evidence / competing hypotheses
- **Far-terrain owner:** falsified by exact saved-pose replay; settled runtime reports `FAR hole=365.9m`, while the camera is ~2.4 m above local ground, so the photographed foreground is inside the far exclusion hole.
- **Missing/semantic near terrain:** falsified by stable replay coverage (`missingVisible=0`) and probes resolving the affected near surface to Grass.
- **Near LOD world/voxel units:** weakened by both CPU HLOD and GPU extraction emitting world-metre positions into the same near shader.
- **Second near texture policy:** confirmed. Grass is authored on texture layer 5 at `uvScale=1/7`; renderer moss coating row 1 reuses layer 5 but sampled it at `1/22`, a 3.14x motif-size mismatch that directly explains the stretched side.

## Selected fix / regression
Change only moss coating row 1 UV scale from `1/22` to `1/7`; preserve tint, blend, response, material identity, and geometry. Behavioral regression: `Game.Materials.Tests.GameMaterialRenderingTests.GrassAndMossCoatingShareAuthoredTextureDensity` compares the game-owned Grass rendering definition with the renderer-owned moss coating table and requires both shared texture layer and UV density to match.

## Blast radius / cost
Presentation constant only: no storage/generation change, allocation, draw, shader instruction, texture copy, mesh rebuild, or per-frame CPU work. Moss coloration/response remains independent.

## Remaining gates
Sanitize the feature-only diff to this capture + proven production/test files; move open→pending with metadata; run one exact-SHA targeted request including the focused EditMode regression and original pose replay; inspect native-resolution `verification-final.png`; then close, merge current master, and fast-forward master non-force.
