# Experiment 022 — exact ivy topology cleanup

**Observed falsifier.** Request `1ed117f3272db270783c0bded33206d25536cdc2`, run `33149348379`, was green but direct replay inspection still showed a long legacy stem. The first exact-topology request `9bdb8038fc44fc294941515ce919fb93b1df8ebc`, run `33149674980`, then failed at `TryBuildTopology(...)`: expected `true`, got `false`, before cleanup ran.

**Root cause.** Color-based discovery is invalid after prior passes mutate vertex colors, but the initial replacement also modeled one stem that production never authors. `BuildIvyMesh` calls `AddStem`, which suppresses segments under 0.01 m; deterministic right-path cluster 1 / leaf 2 (global cluster 13 / leaf 2) falls inside that guard. The real fixed layout is 12 left + 4 right clusters, 128×17 leaf vertices, 14 inter-cluster stems, and 63 leaf stems: **2,484 vertices / 77 stem quads**.

**Action.** Model that exact production topology, including the single omitted leaf stem, and collapse all 77 authored stem quads directly by index. Do not move leaves, flowers, renderers, or add topology.

**Regression / falsifier.** `ArchReferenceGrowthTopologyCleanupPassTests.FinalTopologyCleanupRemovesAllStemQuadsWithoutRegressingAaaMassAcrossRebuild` must prove the production ivy has exactly 2,484 vertices / 77 stem quads, every authored stem span is effectively zero after cleanup, AAA support/crown/right distribution survives, 128 leaves / 30 heads / 3 draws / <=4,096 vertices are unchanged, and rebuild repeats exactly. Reject if exact CI fails or the saved player frame still shows a long stem / falls below the tracked reference bar.

**Blast radius / cost.** ArchLookdev-only one-shot vertex rewrite; no new vertices, draws, GameObjects, shared renderer behavior, or steady-state work.
