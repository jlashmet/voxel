# Experiment 022 — exact ivy topology cleanup

**Observed falsifier.** Exact request `1ed117f3272db270783c0bded33206d25536cdc2`, run `33149348379`, passed the new AAA regression and standalone replay, but direct inspection still showed the long diagonal/vertical ivy stem artifact. The leaf/crown/bouquet composition itself was materially improved.

**Root cause.** The cleanup and its regression both discovered leaves/stems by mesh vertex color. Earlier one-shot passes mutate those colors, so the heuristic can skip real stem quads and still report green. `BuildIvyMesh` already has deterministic topology: 12 left clusters + 4 right clusters, 8×17-vertex leaves per cluster, a 4-vertex connector before every non-first path cluster, and a 4-vertex leaf stem after each even leaf. That yields exactly 78 stem quads in the 2,488-vertex ivy mesh.

**Action.** Add a final one-shot exact-topology cleanup that derives all 128 leaf starts and all 78 stem starts from that production layout and collapses every stem quad directly, independent of color. Do not move leaves, flowers, renderers, or topology.

**Regression / falsifier.** `ArchReferenceGrowthTopologyCleanupPassTests.FinalTopologyCleanupRemovesAllStemQuadsWithoutRegressingAaaMassAcrossRebuild` must prove all 78 deterministic stem quads have effectively zero span, the AAA supports/crown/right-side distribution remains intact, 128 leaves / 30 heads / 3 draws / <=4096 vertices are unchanged, and rebuild repeats exactly. Reject if the exact saved player frame still shows any long stem or the foliage otherwise falls below the tracked reference bar.

**Blast radius / cost.** ArchLookdev-only one-shot vertex rewrite; no new vertices, draws, GameObjects, shared renderer behavior, or steady-state work.
