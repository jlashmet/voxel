# Experiment 012 — pointed supported foliage masses

**Hypothesis.** The remaining mismatch is presentation geometry, not ownership or density: experiment 011's normalized shallow leaf outline and centre layout merge into a rope from the saved camera. The same bounded topology should read like the reference if each leaf has a deeper but non-star-shaped ivy silhouette, left clusters sit farther onto the masonry as separate masses with a few hanging drapes and depth offsets, and flower heads are larger/separated in front of the leaves.

**Action.** Keep world anchoring, 128 leaves, 30 flower heads, 3 renderers and the <=4,096-vertex budget unchanged. Change only the authored leaf outline plus the one-shot final composition: smaller individual leaf radii with wider spacing, height-dependent left-mass offsets, a few deterministic downward drapes/depth layers, and larger foreground five-petal flower clusters with stronger color-family separation.

**Falsifier.** Reject even with green metrics/CI if the 1928x836 saved replay still reads as a continuous green rope, rounded cards, star stamps, or flowers too small to judge directly against the tracked reference.

**Blast radius / cost.** ArchLookdev presentation only; no shared vegetation, world truth, renderer count, vertex count, GameObject count or steady-state work changes.
