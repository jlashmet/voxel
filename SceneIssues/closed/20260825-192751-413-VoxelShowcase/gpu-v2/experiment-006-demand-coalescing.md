# Experiment 006 — coalesce pending GPU mirror demand

## Trigger evidence
Exact optional-halo request `33241309873` executed both requested PlayMode tests and failed as product behavior. The focused harness used a 360-frame startup loop; world generation consumed almost the entire 30.45 s test (`Showcase castle complete` at 27.4 s), so it reached its startup assertion before a meaningful post-generation liveness window. The production 210 m traversal did reach GPU work but lost all visible voxel draws at frame 129 with `gpuCompleted=8`, `gpuFallback=1`, `gpuWaitSlices=2055`, `jobs=12`, and `missing=724`.

The exact built-player replay no longer stayed permanently frozen after the optional-halo change: t15.7 was sparse, t25.7 restored substantial castle geometry, and t35.7 restored more. Late telemetry was still only about `348 drawn / 351 missing`, so convergence exists but is too slow for moving coverage and does not settle by the 45 s gate.

## Hypotheses
1. **Pending demand is serialized by the global prepare gate.** `BeginPersistentStage` used `if (!PrepareFromBridge(...) || !Covers(...))`. Once one worker's `Covers` enqueues recovery, `PrepareFromBridge` closes admission. Other workers therefore short-circuit before `Covers` and cannot register their own exact block demand. When the first queue drains, one extraction starts; the next worker then discovers its missing blocks, but recovery cannot mutate the shared mirror until active extraction returns to zero.
2. **GPU count/write itself is the remaining bottleneck or semantic mismatch.** The single observed `gpuFallback=1` could indicate a count/readback/write disagreement unrelated to demand scheduling.

## Discriminator / change
Commit `0d88e6460c4a8a0c18fc6c68d7fc39c5855f893b` separated preparation from coverage discovery so every pending stage could register missing blocks even while preparation was blocked. Commit `862a857eeecefc1ada848cc2cf1f532b58878500` made the focused harness startup-aware without changing its post-startup liveness assertions.

## Exact result — hypothesis rejected as a production strategy
Exact request `a41b2797c716762b8b31401b5f45a472cb0470b4`, direct parent of feature SHA `d9e846203010de71a261660fa1a20f134f787db2`, ran as GitHub Actions run `33254303476` / job `99105238335`; artifact `9715463385` was inspected.

The focused 96 m behavioral regression **passed** in 53.6 s. That confirms the previously observed starvation mechanism was real: with all pending workers allowed to register demand, recovery no longer hits the focused no-progress condition and sustains the required additional GPU completions.

The production 210 m regression **failed** at traversal frame 112: `visible=0`, `missing=716`, `jobs=12`, `gpuBackends=12`, `gpuCompleted=0`, `gpuFallback=0`, `gpuWaitSlices=1833`. The test output begins from a fully generated exact scene (`199 regions generated, 0 pending, terrain 100%`, castle complete). The later `16 pending, terrain 31%` line is new streaming induced by traversal, not premature test startup; the startup-confounder hypothesis is therefore rejected and the regression remains unchanged.

The exact built-player replay independently remained a product failure. Geometry recovered progressively rather than freezing, but at ~44.9 s it was still only about `103 visible / 661 missing`. Runtime remained fast enough to rule out general frame saturation (roughly 142 FPS average, p95 ~10.65 ms, normal exit).

## Interpretation
All-worker demand discovery traded starvation for over-batching. A step-2 GPU chunk has an 18^3 brick-cache footprint (5,832 block coordinates). Letting all 12 pending workers enqueue their footprints while `PrepareFromBridge` requires the *entire* global recovery queue to drain can create a very large union. Because extraction admission stays closed until that union is empty, the full traversal produced zero GPU completions even though the focused liveness test passed.

Therefore experiment 006 establishes two facts simultaneously:
- recovery admission fairness remains necessary; a queued backlog must not be leapfrogged indefinitely by already-covered extraction work;
- unbounded cross-worker demand coalescing is not the correct fairness mechanism because it delays the first useful completion too long.

Commit `4a51638a080bfdd6d226257b1dd4da5c235ea168` removes the experiment-006 all-worker coalescing behavior and restores one-footprint-at-a-time demand discovery while retaining the proven fairness gate and optional-halo semantics. The next experiment addresses the cost inside that bounded footprint rather than growing the global batch.

## Blast radius / cost
This experiment changed only step-1/step-2 GPU mirror admission ordering. No storage semantics, world generation, shader layout, visibility policy, arena capacity, journal budget, or CPU fallback policy changed. The rejected code has been removed; see experiment 007 for the bounded replacement.
