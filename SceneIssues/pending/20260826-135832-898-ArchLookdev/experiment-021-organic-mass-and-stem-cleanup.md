# Experiment 021 — organic mass and stem cleanup

**Observed falsifier.** Exact request `44a72f7f63c93fee929fe58d947e030c2ab1707f`, run `33148437517`, passed the architectural regression and standalone replay, but direct inspection of `RealPlayer/verification-final.png` still failed the capture: a long diagonal legacy stem crosses the opening, leaf cards collapse into a few dark clumps, and the flowers remain sparse flat stamps.

**Discriminator.** This is no longer a placement/count problem. The crown now reaches the real arch frame, but stale stem topology plus excessive local overlap and dark material multiplication destroy the reference read.

**Action.** Reuse the same 128 leaves / 30 heads / three hero meshes. A final one-shot pass collapses every stem-colored quad to degenerate geometry, rewrites the 16-point leaf rims as smaller broad English-ivy silhouettes, distributes 15 clusters continuously from left pier through the haunch/crown with one sparse right accent, and recomposes the 30 heads as six five-flower bouquets with small centres. Brighten the ivy material enough that vertex-color variation survives shading.

**Regression / falsifier.** `ArchReferenceGrowthAaaPassTests.FinalAaaPassRemovesStemArtifactsAndBuildsContinuousReferenceMassAcrossRebuild` must prove all stem quads have effectively zero span, 128 leaves remain on the intended supports with crown sweep and only one right cluster, six bouquet centroids remain integrated with the mass, head/leaf sizes are bounded, three draws and <=4096 vertices are unchanged, and rebuild is deterministic. Reject even if green if the exact saved player frame still shows stems, disconnected blobs, stamp-like flowers, or otherwise misses the tracked reference's AAA bar.

**Blast radius / cost.** ArchLookdev presentation only; one-shot mutation of existing mesh buffers/materials, no new draw calls, topology, per-leaf GameObjects, or steady-state geometry work.
