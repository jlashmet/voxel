# Experiment 011 — layered canopy composition

**Hypothesis.** Experiment 010 failed visually because it enlarged individual cards while preserving a chain-like leaf-centre layout and undersized flower clusters. The same bounded topology can read closer to the reference if leaf centres are redistributed into overlapping local canopies and complete flower heads are enlarged/separated, without increasing render cost.

**Action / source.** Final source `dbd7d478616693a68dc66cf8ce9704e58ed4a46c` keeps 128 leaves, 30 flower heads, 3 hero renderers and the existing vertex budget, while redistributing leaf centres into deterministic local canopies and recomposing each flower cluster as three complete five-petal heads. Regression `ArchReferenceGrowthLushPassTests.LushPassBuildsLayeredCanopiesAndReadableFlowerClustersAcrossRebuild` measures canopy spread, bounded leaf radii, flower-head radius, 150 visible petals, unchanged budget and rebuild determinism.

**Result.** Exact request `bf94047c88732baa849a3b8e45625721ede2a413`, workflow `33136892735`, passed the regression and 45-second standalone saved-pose replay. Direct comparison of its `RealPlayer/verification-final.png` with the tracked reference **failed the visual bar**: the left growth still reads as a diagonal rope of overlapping round cards, flowers remain tiny/sparse, and the reference's separate supported masses, pointed ivy leaves and hanging drapes are absent.

**Verdict.** Falsified visually despite green behavior/CI. Lifecycle, batching, counts and budgets remain proven; leaf silhouette/spacing/depth and flower foreground readability are the remaining variables.

**Next.** Experiment 012 changes only those art variables and rejects unless the saved replay reads as distinct pointed ivy masses plus clearly readable clustered blossoms.
