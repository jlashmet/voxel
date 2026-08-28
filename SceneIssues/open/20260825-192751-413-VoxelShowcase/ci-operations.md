# CI operations — SceneIssue 20260825-192751-413-VoxelShowcase

Targeted CI uses only `ci-test/fixes/agent-2`; request commits stay off the feature branch.

- Exact-pose run `33029340707`: green, visible coverage and zero blocking completion violations.
- CPU-production source `1ddb80f57d06e95e53a7f9d1317d12a33ce4dd36`, run `33030560453`: all 420 moving frames retained coverage; p95/p99 missed at `18.71/29.40 ms`.
- Profiler run `33038210036`: scheduler/admission/worker prep ~`9.16/6.39/5.21 ms`, upload ~`0.16 ms`.
- GPU-v1 runs `33125988697` and `33131454442`: total visible-solid collapse at frame 8; second replay also showed severe convergence stalls. GPU-v1 rejected for production.
- CPU rollback run `33132712687`: frame-count warmup expired before convergence; replay later reached 517 visible chunks. Warmup changed to 30 s wall-clock.
- Corrected warmup run `33136824454`: moving frame 5 zero.
- Handoff run `33138524485`: moving frame 16 zero; replay later converged ~300-400 FPS.
- Priority-discovery run `33140352114`: moving frame 15 zero; replay later converged ~310-480 FPS. Priority ordering rejected.
- Frustum-handoff run `33141268338`, artifact `9674120924`: moving frame 5 zero; replay later reached 517 visible chunks and ~350-420 FPS. Frustum-only proof rejected.
- Physical-fallback request `06ea24bc32994d230facac57883aa919c51dcb25`, exact source `06e37c5526d0a6f9e16496f9f102073c5fbd36a6`, run `33142076200`, artifact `9674586172`: moving frame 5 still lost every visible solid. Replay started with flat/missing terrain and converged to the castle by ~26 s. Physical-fallback selector correction is insufficient.

Because more than three genuine production attempts are red, the next source state is test-only minimal isolation. The unchanged final traversal will report the active production scheduler's per-ring worker visibility funnel only if aggregate visibility reaches zero. If physical worker `Visible` remains positive, aggregation/selector is proven destructive; if physical worker visibility is zero, the fault is earlier in ring/worker lifecycle. No production code changes until that discriminator is captured.
