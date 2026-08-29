# Experiment 008 — counter readback backpressure

## Hypothesis
The remaining step-1/2 CPU fallbacks are caused by transient async GPU counter-readback failures, not geometry-arena pressure. `TryCompleteStage`/`TryCompleteWriteRange` currently bubble `GpuCounterPoll.Failed` to `CpuTransvoxelChunkCache`, which immediately schedules the full CPU density/topology chain and can consume the ~200 ms solid-admission frames seen in the built player.

Competing hypothesis: the seven fallbacks are `Ready` count results with empty geometry (or deterministic count/write mismatch), in which case counter retries will remain zero and the fallback count will not improve.

## Runtime discriminator
Exact request `2f4dda63c1021ffdd05671a00174f534cb2dbe25` (feature `0417162a8d1cff743f81383c0fc5923261f6cdde`, run `33267842712`) produced:
- focused 96 m mirror-liveness regression: pass, 50.519 s;
- 210 m production traversal: fail at frame 459 with `visible=0`, `missing=638`, `gpuCompleted=64`, `gpuFallback=7`;
- built-player replay: initially ~230–280 FPS, then ~5–7 FPS with ~329 visible chunks missing and ~189–197 ms solid admission;
- geometry arena still below half capacity and `leaseFail=0`, falsifying arena exhaustion as the source of those fallbacks.

## Action
Commit `ad6c8972513c5ae272ee6189b74095d41c807180` keeps transient async count/write readback failures on the GPU path. A failed four-word counter transfer is redispatched against the same staged request, stable mirror extraction window, and (for writes) the same unpublished arena lease, at most twice before the existing deterministic/device-failure fallback remains possible. Added monotonic count/write retry diagnostics; no shader, arena, recovery, world-state, or CPU topology budget changed.

## Expected result / falsification
If transient readbacks caused the red run, exact traversal should keep eligible CPU fallback at zero and avoid the late ~200 ms CPU admission collapse. If `gpuFallback` remains nonzero while retry diagnostics stay zero, reject this hypothesis and handle the `Ready + empty`/deterministic failure path next rather than increasing budgets.
