# Experiment 013 — English ivy masses and blossom bouquets

**Hypothesis.** Experiment 012 failed because silhouette cues and connective structure were wrong: deep radial notches read as maple/star leaves, and the visible inter-cluster connector turns otherwise bounded clusters into one diagonal garland. The same topology can read materially closer to the reference if the leaves use a broad asymmetric five-lobed English-ivy outline with softer shoulders, the inter-cluster connector is collapsed, the left growth is separated into lower/mid/crown masses with depth layers, and the existing 30 flower heads are grouped into tighter bouquets in front of foliage.

**Action.** Change only `ArchReferenceGrowthLushPass`: reshape the 16 boundary vertices of each existing leaf; retain deterministic per-leaf rotation/scale but add shallow bowl depth and center/perimeter color contrast; collapse path-connector quads rather than drawing them through the foliage; retune left mass offsets to create visible gaps; bring flower heads forward and tighten the three heads per cluster into denser bouquets. Keep world anchoring, 128 leaves, 30 heads, 3 renderers, existing vertex topology, and <=4,096 vertices unchanged.

**Falsifier.** Reject even with green metrics/CI if the saved 1928x836 replay still reads as star/maple cutouts, a continuous diagonal garland, flat isolated flowers, or otherwise remains visibly below the tracked reference's AAA foliage quality.

**Blast radius / cost.** ArchLookdev presentation only; no shared vegetation/world truth, renderer count, vertex count, GameObject count, or steady-state work changes.
