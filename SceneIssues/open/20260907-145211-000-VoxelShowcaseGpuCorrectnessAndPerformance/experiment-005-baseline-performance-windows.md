# Experiment 005 — matched baseline performance windows

## Purpose

Freeze same-scene performance windows from the exact pre-repair standalone baseline so later optimization is compared against identical VoxelShowcase workload, not a simpler scene or cherry-picked moment. Correctness remains the gate; these numbers are not acceptance signoff.

## Exact input

- Feature source: `887a819f1ef6f795e546be73783ece40c3dca052`
- CI request: `fd092c40e8d19ddc5f67d7a1f2165ae74f865864`
- Run/job: `34155859120` / `101847425373`
- Unity: 6000.5.6f1
- Device/API: Apple M4 Max / Metal
- Production VoxelShowcase standalone, 180 s
- Surface in this baseline: 1600x900; VSync off; targetFrameRate unset

The current task target is 400 FPS at 1920x1080, render scale 1.0 on M4 Max/Metal. Historical 1,000 FPS remains context only; final signoff must use the canonical 1920x1080 configuration.

## Frozen baseline windows

Parsed from `SceneIssue/fps.txt` one-second timing windows:

| Window | Mean FPS | Median 1s FPS | Median frame p50 | Median frame p95 | Median frame p99 |
|---|---:|---:|---:|---:|---:|
| 60–90 s | 482.3 | 489.1 | 2.00 ms | 3.89 ms | 4.81 ms |
| 120–180 s | 226.6 | 212.2 | 4.23 ms | 6.88 ms | 8.55 ms |
| 150–180 s | 183.7 | 183.4 | 4.75 ms | 7.29 ms | 12.70 ms |

For context, 20–60 s averaged 548.7 FPS with median frame p50 1.70 ms. The late collapse coincides with experiment 003's source mirror saturation (`40270/40270`), continuously increasing `NoSlot`, visible misses, and ~151.7 s oldest request. The 120–180 s window's worst one-second p99 reached 54.86 ms.

## Verdict / next step

The stationary renderer is not uniformly slow: performance decays as the correctness backlog accumulates. Therefore the admission repair must be re-run before attributing the remaining stationary bottleneck. After correctness is green, repeat the 60–90 s stationary and 120–180 s traversal windows at least three times at canonical 1920x1080 scale 1.0, then use Metal System Trace to attribute any residual deficit below 400 FPS.
