# Experiment 022 — exact ivy topology cleanup

**Observed falsifiers.** Run `33149348379` was green but direct replay inspection still showed a long legacy stem. Request `9bdb8038...`, run `33149674980`, failed because the first topology model expected 2,488 vertices / 78 stems; production has one omitted near-zero leaf stem. Corrected request `4808a1de...`, run `33150510855`, then reached the next discriminator: after cleanup an exact leaf cluster remained **0.4398 m** from its intended AAA support, and direct replay still showed the frame-spanning green sliver.

**Root cause.** `BuildIvyMesh` is deterministic but earlier AAA rewriting still discovers leaf starts by mutable vertex color. Once those colors change it can rewrite the wrong 17-vertex ranges, corrupting real leaf triangles before the exact stem cleanup runs. Production topology is 12 left + 4 right clusters, 128×17 leaf vertices, 14 inter-cluster stems, 63 leaf stems: **2,484 vertices / 77 stem quads**; the omitted stem is global cluster 13 / leaf 2 because `AddStem` suppresses its <0.01 m segment.

**Action.** Use the exact topology as the final authority: rebuild all 128 real leaf polygons at the intended AAA supports by exact index, then collapse all 77 real stem quads. Preserve flowers, meshes, counts, draws, and topology.

**Regression / falsifier.** `ArchReferenceGrowthTopologyCleanupPassTests.FinalTopologyCleanupRemovesAllStemQuadsWithoutRegressingAaaMassAcrossRebuild` proves exact 2,484/77 topology, zero authored stem span, every exact cluster within 0.10 m of its support, **maximum ivy triangle edge <0.30 m** (no opening-spanning sliver), preserved 128 leaves / 30 heads / 3 draws / <=4,096 vertices, and deterministic rebuild. Reject if exact CI fails or direct saved-pose inspection remains below the tracked reference bar.

**Blast radius / cost.** ArchLookdev-only one-shot rewrite of existing ivy vertices; no new vertices, draws, GameObjects, shared renderer behavior, or steady-state work.
