# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Defect / acceptance
The sole marked region is the top-left FPS/surface telemetry at the saved Showcase camera. Acceptance remains the production 420-frame/~210 m traversal: solids visible every moving frame, <=5 cm far fallback while near coverage is incomplete, zero frame-path blocking completions, >=4 streamed regions, p95 <18 ms and p99 <25 ms, plus the 45 s real-player replay with intact geometry.

## Evidence / competing hypotheses
1. **CPU preparation is the throughput pressure, not the original coverage baseline.** Profiling measured scheduler/admission/worker prep near 9.16/6.39/5.21 ms versus ~0.16 ms upload. CPU source `1ddb80f...` kept solids visible for all 420 moving frames and failed only p95/p99.
2. **Legacy GPU-v1 safely accelerates production.** Falsified by runs `33125988697` and `33131454442`, both losing every visible solid at frame 8. GPU-v1 stays explicit-experiment only.
3. **Priority discovery, frustum handoff, or proof-only logical fallback is the movement root cause.** Falsified/incomplete. Runs `33140352114`, `33141268338`, and `33142076200` still lost all draws at moving frames 15/5/5; their replays later converged.
4. **The traversal harness changed from the coverage-safe control.** Falsified: safe source `1ddb80f...` used the same direct `showcase.transform.position`, `yield return null`, `camera.Render()` movement sequence.
5. **Remaining discriminator: physical worker visibility vs logical aggregation.** Workers already apply ring-band/frustum checks and retain stale ready geometry. Current aggregate failure does not show whether worker `Visible` lists are non-zero at the first zero-draw frame.

## Required minimal reproduction
After >3 failed production attempts, make no further renderer change until the existing behavioral traversal reports the worker visibility funnel at the first zero frame. Test-only reflection reads the active production pass/scheduler/rings and records each ring's known/in-band/frustum/ready/empty counters plus physical `worker.Visible` count.

- `physicalTotal > 0` with aggregate `VisibleSolidChunks == 0` proves aggregation/selector loss.
- `physicalTotal == 0` proves the loss occurs earlier in ring/worker visibility.

The traversal thresholds and assertions are unchanged.

## Blast radius / cost
This discriminator changes tests/evidence only: no production behavior, runtime API, budget, allocation, storage/gameplay authority, meshing, upload, shader, or LOD policy changes. Reflection executes only on the already-failing zero-draw frame.
