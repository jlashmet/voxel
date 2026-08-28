# Experiment 012 — pointed supported foliage masses

**Hypothesis.** The remaining mismatch is presentation geometry, not ownership or density: experiment 011's normalized shallow leaf outline and centre layout merge into a rope from the saved camera. The same bounded topology should read like the reference if each leaf has a deeper but non-star-shaped ivy silhouette, left clusters sit farther onto the masonry as separate masses with a few hanging drapes and depth offsets, and flower heads are larger/separated in front of the leaves.

**Action / source.** Source `182d2939c1ab2c54865c211506008e6ea1be1ce6` changes only `ArchReferenceGrowthLushPass`: the same 128 leaf polygons are rewritten to a deterministic deeply-lobed ivy outline at smaller radii, height-dependent left-mass offsets move growth onto the masonry, selected leaves form downward drapes with extra depth layering, and the same 30 five-petal heads are separated, enlarged and moved in front with stronger white/pink/blue/orange families. World anchoring, topology, renderers and budgets are unchanged.

**Falsifier.** Reject even with green metrics/CI if the 1928x836 saved replay still reads as a continuous green rope, rounded cards, star stamps, or flowers too small to judge directly against the tracked reference.

**Blast radius / cost.** ArchLookdev presentation only; no shared vegetation, world truth, renderer count, vertex count, GameObject count or steady-state work changes.

**Next.** Run the existing production-mesh regression plus the original 45-second saved-pose standalone replay and compare pixels directly with the tracked reference.
