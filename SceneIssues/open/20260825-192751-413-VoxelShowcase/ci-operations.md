# CI operations — SceneIssue 20260825-192751-413-VoxelShowcase

Targeted CI uses only `ci-test/fixes/agent-2`; request commits stay off the feature branch.

- Focused exact-pose regression request `3a06a96fccd5c928caebbaa54bdf03f43ccf26fd`, run `33029340707`, artifact `9630019522`: green, 1/1 PlayMode pass. Exact-pose late replay retained full visible coverage; no frame-path blocking completion violations.
- Final traversal/replay request `6da72281f0f741c0b254681b337fe1f807c47b29`, run `33030560453`: shared-runner queue monitoring retained the exact request without replacement.
- Profiler request `dde805128ca2593cbd0208a4d4a8161e6a3fa8ee`: diagnostic compile failure (`VoxelSurfaceMetrics` namespace missing); no production conclusion.
- Corrected profiler request `4646956d02044ac4f3e54c8aa6b02317e78cb02b`, run `33037559657`, artifact `9632771001`: one-worker diagnostic lost visible draws and was not treated as production evidence.
- Paired eight-worker request `fdbf89dcb53cce24096ff0060e1893480d8f31bf`, run `33038210036`, artifact `9633087469`: profiler passed; unchanged traversal missed p95/p99 at `18.72/25.64 ms`. Scheduler/admission/worker preparation were ~`9.16/6.39/5.21 ms`, versus ~`0.16 ms` upload.
- Final isolated snapshot candidate request `71e826fb73f3f6902f1da0d718a2e8e9ed849ef4`, source `2d4c0a090f37cefc6d60db1ccd1ca68c04190815`, run `33125988697`, artifact `9668432388`: product failure. The requested traversal lost every visible solid draw at frame 8. The 45 s replay still captured the saved pose, but this run cannot satisfy any gate.
- Subsequent source work reproduces the CPU raw-voxel eligibility rules on the GPU mirror: packed surface semantics are decoded before support classification, and unsupported exact-ring chunks return zero count into the existing CPU fallback rather than publishing empty geometry. No replacement was issued for the failed request; the next CI transport is the final request for the completed source state.
