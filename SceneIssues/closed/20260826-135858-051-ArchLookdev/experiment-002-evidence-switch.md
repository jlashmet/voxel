# Experiment 002 — built-player evidence switch

**Hypothesis:** the green built-player run misses the hero portal because the ticket-specific landmark driver is not receiving the SceneIssue path, rather than because Warehouse access/geometry regressed.

**Action / source:** inspected exact request `f0657d7a5f4ba28d26296aee89b85a0647a66330` from source `37987ed3b649a68f5b5d28509e8162bcf590fc0c`, including every full-resolution real-player screenshot and `RealPlayer/player-run.log`, then compared `KentridgeLandmarkEvidenceHarness` argument parsing with `tools/showcase-player-capture.sh`.

**Result:** the production-host traversal regression passed and the built player reached usable Kentridge, but no screenshot showed the hero arch and the player log contained no `ARCH_EVIDENCE` activation. The capture script launches the player with `-voxel-scene-issue`; the landmark harness parsed only `-voxelIssue`.

**Verdict:** supports an evidence-path mismatch. The green workflow is diagnostic only and does not satisfy visual closure. The passing traversal test continues to falsify a Warehouse composition/access failure.

**Next:** parse the canonical scene-issue switch (keeping legacy compatibility), then rerun a fresh exact-SHA final request and require `ARCH_EVIDENCE` logs plus a Kentridge screenshot with readable voussoirs.
