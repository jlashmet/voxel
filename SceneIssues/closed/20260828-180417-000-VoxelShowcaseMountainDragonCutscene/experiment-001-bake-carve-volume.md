# Experiment 001 — traversal carve volume

**Hypothesis:** the post-`3059c8c119a7` full-width headroom carve is the product-side delta that pushed the already-marginal VoxelShowcase bake past its four-minute guard.

**Action / source:** at source `09649970214b06235c08d22525cb9ab5fc703587`, keep the 30-voxel visible path and 24-voxel vertical headroom, but constrain carve boxes to a centered 16-voxel traversal lane. The 0.6 m production motor retains 0.5 m lateral margin per side. No region bounds, primitive count, path floor, support ordering, or runtime loop changed.

**Result:** static program accounting keeps 13 carve primitives / 76 total primitives and reduces carve raster volume from 5,097,000 to 2,718,400 voxels (-46.7%). The 1200 x 306 x 1200 feature footprint is unchanged. The focused production-program regression now enforces lane width, carve count, <=2.8M carve voxels, tapered support, and shared primitive budgets.

**Verdict:** supports the leading cost hypothesis and is the smallest semantic optimization that preserves traversal acceptance. Runtime/bake timing remains unvalidated because the assignment's single CI ref update and one retry were already consumed by the prior source candidate.

**Next:** obtain an authorized exact-SHA validation path, then require source-matched bake/manifest, focused acceptance, grounded full-route replay, and human-reviewed captures before promotion.
