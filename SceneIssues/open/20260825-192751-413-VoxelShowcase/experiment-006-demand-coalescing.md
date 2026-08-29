# Experiment 006 — coalesce pending GPU mirror demand

## Trigger evidence
Exact optional-halo request `33241309873` executed both requested PlayMode tests and failed as product behavior. The focused harness used a 360-frame startup loop; world generation consumed almost the entire 30.45 s test (`Showcase castle complete` at 27.4 s), so it reached its startup assertion before a meaningful post-generation liveness window. The production 210 m traversal did reach GPU work but lost all visible voxel draws at frame 129 with `gpuCompleted=8`, `gpuFallback=1`, `gpuWaitSlices=2055`, `jobs=12`, and `missing=724`.

The exact built-player replay no longer stayed permanently frozen after the optional-halo change: t15.7 was sparse, t25.7 restored substantial castle geometry, and t35.7 restored more. Late telemetry was still only about `348 drawn / 351 missing`, so convergence exists but is too slow for moving coverage and does not settle by the 45 s gate.

## Hypotheses
1. **Pending demand is serialized by the global prepare gate.** `BeginPersistentStage` used `if (!PrepareFromBridge(...) || !Covers(...))`. Once one worker's `Covers` enqueues recovery, `PrepareFromBridge` closes admission. Other workers therefore short-circuit before `Covers` and cannot register their own exact block demand. When the first queue drains, one extraction starts; the next worker then discovers its missing blocks, but recovery cannot mutate the shared mirror until active extraction returns to zero. This repeats recovery → one extraction → recovery across the 12 workers.
2. **GPU count/write itself is the remaining bottleneck or semantic mismatch.** The single observed `gpuFallback=1` could indicate a count/readback/write disagreement unrelated to demand scheduling. If demand coalescing does not restore traversal coverage, instrument fallback reasons and count/write latency rather than increasing recovery budgets.

## Discriminator / change
Commit `0d88e6460c4a8a0c18fc6c68d7fc39c5855f893b` separates preparation from coverage discovery. Every pending stage now evaluates `Covers` even when `PrepareFromBridge` is false. `Covers` does not mutate GPU mirror payloads; it validates readiness/history and deduplicates exact missing resident block coordinates into the existing recovery queue. Dispatch still requires both `prepared` and `covered`, and mirror mutation remains forbidden while any extraction is active.

This lets waiting workers coalesce their overlapping footprints before the next safe recovery drain without increasing either the 64-block recovery slice or 128-record journal slice.

Commit `862a857eeecefc1ada848cc2cf1f532b58878500` replaces the focused test's 360-frame startup cap with a 60 s wall-clock initial-coverage window. Post-startup recovery, optional-halo, completion, and >180-frame stall assertions are unchanged.

## Expected result
If hypothesis 1 is causal:
- queued demand from multiple pending workers is drained as a union rather than one chunk at a time;
- GPU completions continue beyond the prior eight-completion traversal ceiling;
- the focused 96 m liveness test reaches its post-startup assertions and passes;
- the 210 m traversal never reaches `visible=0`, has zero GPU-eligible CPU fallback, and settles to zero missing visible chunks;
- the built-player replay restores the full near/mid field instead of slowly rebuilding only part of it by 45 s.

If the same plateau remains, reject hypothesis 1 and pursue hypothesis 2 with explicit fallback-reason/count-write instrumentation.

## Blast radius / cost
The production change is limited to step-1/step-2 GPU mirror admission ordering. No storage semantics, world generation, shader layout, visibility policy, arena capacity, recovery budget, journal budget, or CPU fallback policy changes. Existing recovery queue deduplication bounds repeated demand discovery, and no new per-frame allocation is introduced.

## Result
Pending exact-SHA targeted CI and exact built-player replay.
