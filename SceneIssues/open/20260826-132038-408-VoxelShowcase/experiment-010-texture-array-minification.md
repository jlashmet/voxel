# Experiment 010 — texture-array minification

**Hypothesis.** The saved-pose handoff survives identical material IDs, world-space UV math, explicit 0.1 m voxel scale, and earlier presentation publication because the shared runtime `Texture2DArray` has no mip chain. Near fragments cover a small enough texel footprint to look finely tiled; far clipmap fragments heavily minify the same full-resolution grass layer, so bilinear-only sampling aliases into oversized/stretched-looking features.

**Evidence / discriminator.** `FarTerrain.shader` and `SmoothSurface.shader` use the same `SurfaceUV`, `SampleMaterialAlbedo`, material row, and world-position-to-voxel conversion. `VoxelPresentationCatalogue.BuildTextureArray`, however, constructed both shared arrays with `mipChain: false` and copied only mip 0. The focused regression `SharedPresentationTextureArraySupportsMinification` now requires more than one mip level and trilinear filtering.

**Action.** Build the shared arrays with a mip chain. Normalize each source into a temporary mipmapped render texture, generate its mip pyramid on the GPU, copy every generated mip into the corresponding array layer, and use trilinear filtering. No shader-specific grass path, per-frame pass, mesh rebuild, or runtime sampling policy was added.

**Blast radius / cost.** The change affects every material using the renderer-owned texture arrays, which is intentional because minification is a shared presentation property. Work remains catalogue-build/startup only. Full mip pyramids add about one third to each uncompressed array's base-level memory (roughly +21 MiB total for two 8-layer 1024² RGBA32 arrays), within the existing 1024 cap; they do not add per-frame allocations or draw calls.

**Verdict.** Candidate selected. The regression and exact saved-pose replay are the gates: if the original camera still shows a near/far grass scale discontinuity, this hypothesis is falsified and the issue stays open.
