# Experiment 012 — zero-copy water classification

## Hypothesis
The deadline-aware discovery slice remains red because its indivisible brick operation copies materials, surface semantics, and boundary samples before scanning materials. A borrowed-view material query removes the irrelevant copies while preserving CPU-derived water truth.

## Evidence
Exact feature `5443cd73f5991d37dffbe5a2f1023ea162d35013`; request `5dfb7a91bd5d774452db8b1a7f322d457ca124b8`; run/job `33282801017` / `99180710200`; artifact `9723624497`.

- Liveness passed in 51.78 s.
- Migration failed only moving p99: 79.164 ms versus 25 ms.
- Player converged to `missingVisible=0` and settled near 200–600 FPS.
- Peak admission was `total=39.702`, `water=39.214`, `solid=0.485` ms. Arena upload peaked on frame 221; water peaked on frame 299, falsifying arena upload as that spike's owner.
- All timed images were inspected: startup was sparse at 15.8 s, the castle was substantially visible at 25.8 s, and the 35.8 s view was complete. The final image artifact is only a thin strip and is not valid visual evidence.

## Action / gate
Add a `RegionReadView` query that scans only borrowed material bytes for water ids and use it for water-brick classification. Preserve pinned snapshots for meshing and immediate mutation invalidation. Run focused storage and bounded-water tests locally; rerun unchanged exact thresholds only with a newly authorized CI transport.

Local result: `StorageRegionReadViewTests` passed 2/2 and the bounded-water contract passed 1/1 through `tools/unity-run.sh`.
