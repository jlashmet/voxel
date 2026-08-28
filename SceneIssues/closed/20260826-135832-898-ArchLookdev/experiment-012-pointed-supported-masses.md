# Experiment 012 — pointed supported foliage masses

**Hypothesis.** The remaining mismatch is presentation geometry, not ownership or density: experiment 011's normalized shallow leaf outline and centre layout merge into a rope from the saved camera. The same bounded topology should read like the reference if each leaf has a deeper but non-star-shaped ivy silhouette, left clusters sit farther onto the masonry as separate masses with a few hanging drapes and depth offsets, and flower heads are larger/separated in front of the leaves.

**Action.** Source `182d2939c1ab2c54865c211506008e6ea1be1ce6` keeps world anchoring, 128 leaves, 30 flower heads, 3 renderers and the <=4,096-vertex budget unchanged. It changes only the authored leaf outline plus the one-shot final composition: smaller individual leaf radii with wider spacing, height-dependent left-mass offsets, deterministic downward drapes/depth layers, and larger foreground five-petal flower clusters.

**Evidence.** Exact request `b2bbd95f7349542a2ea526664d3ecdeb260138ac`, workflow run `33138311319`, passed the focused production-mesh regression and completed the 45-second standalone replay from exact feature SHA `d1b9dfd79bd5d2348d2d21815bc9cff4a69ecf26`.

**Verdict — rejected visually.** The inspected saved-pose player frame still reads as a diagonal garland. More importantly, the deeper notches resolve as obvious radial maple/star cutouts instead of English ivy, and the blossoms remain sparse flat accents rather than integrated bouquets. Green geometry metrics therefore do not satisfy the capture's AAA bar.

**Falsifier result.** Triggered: continuous diagonal chain + star-like leaves remain visible at 1928x836.

**Blast radius / cost.** ArchLookdev presentation only; no shared vegetation, world truth, renderer count, vertex count, GameObject count or steady-state work changes.

**Next.** Experiment 013 removes the inter-cluster connector, uses a broad five-lobed English-ivy silhouette with softer shoulders, separates lower/mid/crown masses, strengthens per-leaf depth/color hierarchy, and tightens the same 30 flower heads into bouquets without increasing topology or draw cost.
