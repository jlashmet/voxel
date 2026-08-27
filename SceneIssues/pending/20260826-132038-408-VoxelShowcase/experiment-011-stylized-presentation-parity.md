# Experiment 011 — stylized material presentation parity

**Hypothesis.** Near and far grass select the same material ID, texture layer, UV scale, and texture sample, but do not apply the same presentation policy after sampling. `GameMaterialRuntimeCatalogue.StylizedTerrain` marks grass `luminanceOnly: true` with detail/chroma/macro variation. `SmoothSurface.shader` applies those fields so the authored green albedo owns hue; `FarTerrain.shader` previously ignored `_MaterialVariation` and `materialSurface.w` and directly blended raw grass RGB.

**Minimal reproduction / discriminator.** A production-shader framebuffer regression, `FarTerrainHonorsLuminanceOnlyMaterialPresentation`, renders `VoxelEngine/FarTerrain` with a green vertex albedo, a white texture, and a luminance-only row. Correct presentation must remain strongly green; the old direct-texture path renders nearly neutral. This is the same policy boundary implicated by the saved pose without depending on scene streaming or screenshot coordinates.

**Action.** Far terrain now consumes `_MaterialVariation` and applies the same base-material sequence as `SmoothSurface`: distance texture weight, luminance/detail/chroma reconstruction for luminance-only rows, then fine/macro variation. Coatings, authored surface-style patterns, near-only normal relief, and far aerial perspective remain representation-specific.

**Blast radius / cost.** One extra material-row lookup and the same scalar/noise math already paid by near fragments. No new texture, pass, draw call, allocation, mesh rebuild, or world work. The falsified mip allocation was removed.

**Falsifier / gate.** If the framebuffer regression fails or the original saved camera still shows the dark/fine-left versus pale/oversized-right grass treatment, the hypothesis is rejected and the issue remains open.
