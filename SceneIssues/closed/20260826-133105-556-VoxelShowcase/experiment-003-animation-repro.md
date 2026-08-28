# Experiment 003 — static reveal rejected

- **Hypothesis:** pre-authoring deeper open-state leaves is enough to turn the existing delete interaction into a valid opening.
- **Exact run:** source `ee9a6a68ed3a6d3e3743ab5085243603c0640369`, request `e6df5ac9c99efa2daff5fb78973318616e3d1534`, run `33118103870`, job `98677856814`.
- **Result:** compilation and real-player SceneIssue replay succeeded, but the focused test failed: expected a retained left open leaf after interaction; the sampled voxel was `Empty`. The replay still showed the correct authored closed timber gate at the captured pose.
- **Verdict:** falsified. The hidden-leaf geometry was not a reliable completed-world open state, and a static reveal cannot satisfy the explicit captured requirement for an opening animation.
- **Follow-up:** remove hidden authoring. Preserve the actual authored closed materials, then rotate two physical leaf halves over a bounded 0.9-second world-state transition. The focused regression now checks closed/mid/final poses, retained timber/iron leaves, a clear centre lane, and emits a native 1928x836 captured-pose verification render.
