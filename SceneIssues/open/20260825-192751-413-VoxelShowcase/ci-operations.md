# CI operations — SceneIssue 20260825-192751-413-VoxelShowcase

Targeted CI uses only `ci-test/fixes/agent-2`; request commits stay off the feature branch.

- Focused exact-pose request `3a06a96fccd5c928caebbaa54bdf03f43ccf26fd`, run `33029340707`: green, 1/1 PlayMode pass with visible coverage and no blocking completion violation.
- Eight-worker profiler request `fdbf89dcb53cce24096ff0060e1893480d8f31bf`, run `33038210036`: traversal p95/p99 `18.72/25.64 ms`; scheduler/admission/worker preparation ~`9.16/6.39/5.21 ms`, upload ~`0.16 ms`.
- CPU-production traversal request `6da72281f0f741c0b254681b337fe1f807c47b29`, source `1ddb80f57d06e95e53a7f9d1317d12a33ce4dd36`, run `33030560453`: coverage stayed intact for the full traversal; performance missed p95 only (`18.71 ms`, p99 `29.40 ms`). Replay later settled around ~265-352 FPS with no total-draw collapse.
- Legacy GPU-v1 candidate request `71e826fb73f3f6902f1da0d718a2e8e9ed849ef4`, source `2d4c0a090f37cefc6d60db1ccd1ca68c04190815`, run `33125988697`: product failure; traversal lost every visible solid draw at frame 8.
- Exact final request `ed69bef4d823b57cf5cd2647b8b47d8488302951`, source `0c4a5b160a8b283cb657913c0cc6f94d7ba1b840`, run `33131454442`, artifact `9670588467`: completed red. Even after GPU-side semantic classification and CPU fallback, traversal again lost every visible solid draw at frame 8. The replay showed severe convergence stalls and incomplete flat terrain before late castle recovery. This falsifies semantic eligibility as the primary GPU-v1 failure.

Selected correction: keep the optimized CPU renderer as production behavior and gate legacy GPU-v1 behind explicit `VOXEL_ENABLE_EXPERIMENTAL_GPU_CUTOVER=1`, while retaining `VOXEL_DISABLE_GPU_CUTOVER=1` as the stronger override. The next request is permitted only after this source change and is the final exact-SHA traversal/replay verification; it does not replace any queued or running request.
