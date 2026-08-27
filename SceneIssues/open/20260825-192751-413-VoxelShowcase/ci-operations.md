# CI operations — SceneIssue 20260825-192751-413-VoxelShowcase

Targeted CI uses only `ci-test/fixes/agent-2`; request commits stay off the feature branch.

- Focused exact-pose regression request `3a06a96fccd5c928caebbaa54bdf03f43ccf26fd`, run `33029340707`, artifact `9630019522`: green, 1/1 PlayMode pass. Exact-pose late replay retained full visible coverage; no frame-path blocking completion violations.
- Final traversal/replay request `6da72281f0f741c0b254681b337fe1f807c47b29`, run `33030560453`: shared-runner queue monitoring retained the exact request without replacement.
- Profiler request `dde805128ca2593cbd0208a4d4a8161e6a3fa8ee`: completed red because the diagnostic test did not compile (`VoxelSurfaceMetrics` namespace missing); no production conclusion drawn.
- Corrected profiler request `4646956d02044ac4f3e54c8aa6b02317e78cb02b`, run `33037559657`, artifact `9632771001`: compiled and executed, but CI supplied one Unity job worker and the diagnostic lost all visible draws at frame 5. This differs from the production traversal gate's eight-worker configuration and is diagnostic only.
- Paired eight-worker request `fdbf89dcb53cce24096ff0060e1893480d8f31bf`, run `33038210036`, artifact `9633087469`: both tests executed under the real eight-worker configuration. The profiler passed; the unchanged traversal acceptance failed at p95 `18.72 ms` (limit `18.0 ms`) and p99 `25.64 ms` (limit `25.0 ms`). Profiling attributed p95 cost primarily to scheduler/admission/worker preparation (`~9.16 / 6.39 / 5.21 ms`) rather than upload (`~0.16 ms`) or GC.
- Diagnostic source `b4f7727d0d761a15872ba51c8437a80e132be498` adds only end-of-run stage timing logs from existing `VoxelSurfaceMetrics`; no production behavior or acceptance budget changes.
- Exact stage-profile request `05e8e8b1f03dc7ca901d0a697c9cdd4b2987c6e2` is a direct child of `b4f7727d0d761a15872ba51c8437a80e132be498` and pairs the unchanged traversal acceptance with the profiler under eight workers.
