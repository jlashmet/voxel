# Experiment 013 — moss coating texture density

## Hypothesis
The photographed boundary is not a second terrain renderer. It is one near voxel surface crossing into the renderer-owned moss coating: both paths sample the same authored grass artwork, but the coating uses a different UV density, making the right-side blades/flowers look enlarged.

## Action / evidence
The saved-pose replay already falsified far-terrain ownership (`FAR hole=365.9m`) and showed stable near coverage. Tracing the remaining near presentation tables found that `GameMaterialRuntimeCatalogue` authors `Grass` with texture layer 5 and `uvScale=1/7`, while `VoxelPresentationCatalogue` coating row 1 also samples layer 5 but at `uvScale=1/22`. That is a 3.14x motif-size discontinuity on otherwise continuous ground and directly matches the note that the same grass texture looks stretched on one side.

## Fix / regression
Keep coating tint/blend/noise behavior unchanged and set only coating row 1 UV scale to `1/7`. `GameMaterialRenderingTests.GrassAndMossCoatingShareAuthoredTextureDensity` goes through the game material definition and renderer coating table and requires both the texture layer and physical UV density to match.

## Blast radius / cost
Presentation-only constant change. No world/storage mutation, allocation, extra draw, shader instruction, mesh rebuild, texture copy, or per-frame CPU work. Moss keeps its independent tint and response; only the apparent size of the reused grass artwork changes.

## Gate
Run the focused regression and original saved-pose replay from one exact final source SHA. Reject the candidate if the hard stretched-grass boundary is still visible.