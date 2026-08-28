# Experiment 016 — rounded rosettes and verified mass parsing

## Runtime evidence
- Exact experiment-015 request `1c4c1347165ab0d3a735d2abe641ef37f2d63cc9`, run `33144764363`, executed the 45-second real-player replay and failed the new regression at `ArchReferenceGrowthMassBreakupPassTests.cs:73` with `Expected: less than Infinity / But was: Infinity`.
- That `Infinity` is a test discriminator defect: the helper assumed a fixed interleaving of path stems and leaves instead of positively locating the 128 leaf cards.
- Direct inspection of the same run's `RealPlayer/verification-final.png` also rejects the production result independently of the test bug: the left/crown foliage still bridges into a repeated band, while the light blossoms remain five-point/star-like icons rather than layered bouquets.

## Competing hypotheses
1. **Experiment 015 is visually acceptable and only the test is wrong — rejected.** The captured frame still misses both the foliage-mass and blossom-silhouette acceptance criteria.
2. **More path compression alone is sufficient — incomplete.** It can improve negative space but cannot change the radial pointed flower silhouette.
3. **Current experiment.** Locate leaves by their durable non-stem color runs, compact the same 12 left clusters into three masonry-supported zones, and reconstruct each existing seven-vertex petal as a broad overlapping oval lobe. Gather the same 30 heads into three rosette bouquets with retained depth and centre discs.

## Gate / cost
Regression must positively recover 128 leaves, measure finite pre/post mass metrics, require separated/compact left zones, rounded head aspect/overlap and bouquet depth, unchanged mesh identity, 3 draws, <=4096 vertices, and deterministic rebuild. ArchLookdev only; no renderer/vertex/GameObject increase and no per-frame work.