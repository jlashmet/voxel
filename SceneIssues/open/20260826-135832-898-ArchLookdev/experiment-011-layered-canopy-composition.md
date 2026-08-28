# Experiment 011 — layered canopy composition

**Hypothesis.** Experiment 010 failed visually because it enlarged individual cards while preserving a chain-like leaf-centre layout and undersized flower clusters. The same bounded topology can read closer to the reference if leaf centres are redistributed into overlapping local canopies and complete flower heads are enlarged/separated, without increasing render cost.

**Action.** Source `9449bacce37d7b10d88a208a81418ae7d7b96ca2` replaces the final lush pass only. It keeps 128 leaves, 30 flower heads, 3 hero renderers and the existing vertex budget, but rewrites leaf centres/radii/stems into deterministic local canopy layouts and recomposes each flower cluster as three readable five-petal heads with a mixed reference-like palette. Current regression commit `0b9009c2241169e9cb93c2bcdb070eae340159d0` measures canopy spread, leaf-radius ceiling, flower-head radius, petal completeness, budget and rebuild determinism through the production meshes.

**Falsifier.** Reject even with green CI if the saved 1928×836 replay still reads as repeated blobs/stamps rather than layered ivy with clearly readable clustered blossoms.

**Next.** Run exact-SHA targeted PlayMode CI plus 45-second saved-pose RealPlayer replay and compare directly with the tracked reference.
