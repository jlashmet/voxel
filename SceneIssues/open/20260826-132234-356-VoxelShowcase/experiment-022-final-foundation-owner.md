# Experiment 022 — final generated-foundation owner

**Hypothesis.** The final shared-structure stage recreates the rectangular Dirt/grass defect after plot-surface generation because generated Kentridge houses use a 7 dm compiled foundation but are translated only 5 dm below their authored plot surface.

**Action / source.** Traced the exact seed `1592594996` through `KentridgeCombinedVoxelCatalogueCanonical` stage order, `KentridgeSharedStructureVoxelCatalogue`, `KentridgeDefinition.Theme`, and `HouseProgramCompiler`. Implemented source `53f76db8b6629e1de7aa3a89750ab46403b4a3d1`: generated programs carry `theme.FoundationHeightDm` as their placement sink; bespoke programs retain 5 dm. Reverted the previously falsified route and plot-shape production experiments. Replaced the isolated plot-shape test with an authoritative-storage regression that rasterizes MayorHouse through `FeatureGeneration.GenerateRegion`.

**Result before final CI.** The causal arithmetic is exact: 7 dm foundation − 5 dm sink = 2 dm protrusion above the intended surface. The new placement makes the foundation top coincide with the captured MayorHouse surface Y=221; the regression samples authoritative storage immediately above and below that surface.

**Verdict.** Leading hypothesis selected; prior route/plot ownership hypotheses remain falsified by byte-identical marked-region pixels. Await final exact-SHA regression and built-player pose replay.
