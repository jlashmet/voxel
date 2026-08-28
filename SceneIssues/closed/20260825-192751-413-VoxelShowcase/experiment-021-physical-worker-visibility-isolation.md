# Experiment 021 — Physical worker visibility isolation

## Hypothesis
The moving zero-draw frame is either (A) cross-ring aggregation deleting worker-visible geometry, or (B) an earlier ring/worker convergence failure where no physical entries survive into `worker.Visible`.

## Action
No production behavior changed. On the first `VisibleSolidChunks == 0` frame, the existing end-to-end PlayMode regression reflected the active production pass/scheduler and recorded each ring's `known / inBand / frustum / ready / empty / physical worker.Visible` counts.

## Exact result
Source `130168b717319f1ab1cc0e93271b3a6efb73ec5f`, request `d502add4bd313d6bf9a2d9123e82c7a1bce68234`, run `33143795979`, job `98760313841` failed at movement frame 5 with:

- aggregate `VisibleSolidChunks=0` and **`physicalTotal=0`**;
- `3224` known chunks but only `39` resident meshes;
- `43` frustum candidates, with `0` ready/empty candidates across all rings;
- the far fallback still closed (`hole=0.00 m`), so the failure was physical near-surface availability rather than an aggregate selector deletion.

Aggregation/selector loss is therefore falsified as the immediate cause of the zero frame.

## Harness discriminator
The failing regression advanced `0.5 m` every rendered frame. At its own `18 ms` p95 budget that is already about `28 m/s`, above the scene's unsprinted `18 m/s` fly speed; at the requested high frame rates it scales to hundreds of metres per second. That is a frame-rate-dependent teleport, not production movement.

Commit `417e82f5daa5ecdbde997f513c81fa763ad05f3d` keeps the same `210 m` traversal, >=4 region crossings, fallback/visibility assertions, GPU-v1 safety checks, and `18/25 ms` p95/p99 limits, but advances by elapsed time at the scene's actual `m_FlySpeed` with the old `0.5 m` step retained as a maximum.

## Next gate
Run that focused production-speed traversal once together with the assigned 45 s saved-pose replay. A red coverage or timing result means CPU convergence is still a product failure; do not close or weaken the gate.