# Experiment 010 — texture-array minification

**Hypothesis.** The saved-pose handoff survives identical material IDs, world-space UV math, explicit 0.1 m voxel scale, and earlier presentation publication because the shared runtime `Texture2DArray` has no mip chain. Near fragments cover a small enough texel footprint to look finely tiled; far clipmap fragments heavily minify the same full-resolution grass layer.

**Action / discriminator.** Candidate source added mipmapped shared arrays with GPU-generated pyramids and trilinear filtering plus `SharedPresentationTextureArraySupportsMinification`. Exact-source EditMode run `33084912826` passed. The same integrated source `476c5deef322510bd438aeaf421c1cd7f34214d0` then ran saved-pose replay request `6e8af2e880932b7a505f9a7a4a28a9544f954a21` in run `33087477898`.

**Result.** The PlayMode test and real-player capture both completed successfully, but `verification-final.png` still shows the original hard handoff: dark/fine near grass on the left and pale oversized blade/flower texture on the right. GitHub also labelled the overall workflow cancelled after all substantive steps, so it could not be a final gate regardless.

**Verdict.** Falsified. Mip generation did not remove the product defect and added roughly 21 MiB to the two 8-layer 1024² RGBA32 arrays, so the change and its regression were reverted. The captured artifact instead points back to a presentation-policy difference, not minification.
