# Experiment 016 — per-frame worker-stage overrun

## Question

Within `Voxel.Surface.WorkerPrepare`, which synchronous stage actually consumes the multi-millisecond player-frame spikes?

## Method

Exact request `03236437c79d247539c5a713a247360060e5ef28` paired the unchanged traversal acceptance with `ShowcaseTraversalWorkerStageProfilingTests.ContinuousTraversalReportsPerFrameWorkerStages`, using the same deterministic 420-frame path and the CI lane's 8 Unity job workers.

Run `33071551733`, job `98514816991`, artifact `9646057230`. Production defaults and acceptance thresholds were unchanged.

## Result

The unchanged traversal acceptance remained red but close to budget: p95 `18.57 ms`, p99 `24.57 ms`, max `26.97 ms`.

Per-frame production markers:

- scheduler: p95 `9.498 ms`, max `75.645 ms`
- worker admission: p95 `6.767 ms`, max `72.370 ms`
- worker prepare: p50 `3.110 ms`, p95 `5.606 ms`, p99 `8.749 ms`, max `70.858 ms`
- **snapshot: p95 `4.496 ms`, max `70.829 ms`**
- topology compact: p95 `1.347 ms`, max `3.835 ms`
- faceted merge: p95 `0.098 ms`, max `0.312 ms`
- profile emit: p95/max `0 ms`

The slowest worker frame was almost entirely snapshot work: frame 272 reported worker `70.858 ms` and snapshot `70.829 ms`. Other large worker frames also tracked snapshot closely (for example `9.794/9.059`, `8.749/8.500`, `7.344/7.326`, `7.168/7.151` ms worker/snapshot).

No sampled frame synchronously completed a geometry job (`FramePathBlockingCompletionViolations == 0`), and fallback coverage remained valid.

## Source audit

`StepExactDensitySnapshot` calls `ScheduleExactMetadataSnapshot(source, cacheOrigin)` before it can return to its deadline check. That scheduling method fans one metadata job per intersecting resident region plus clear/compact dependencies, but accepts no deadline and cannot yield partway through job scheduling. Region pinning itself is only a hash lookup, pin-count increment, token capture, and borrowed-array view, so the leading cost is job scheduling/dependency fanout while the job system is already saturated rather than a large Storage copy.

## Interpretation

This is the first direct causal discriminator: exact-snapshot scheduling is the dominant synchronous worker overrun during traversal. Compaction is secondary; merge/profile are negligible.

The same run family also shows approximately 4.2 s p95 queue latency with up to 12 configured converging builds on a CI process launched with 8 Unity job workers. The next independent discriminator is therefore a test-only 12-to-8 in-flight build comparison, preserving all correctness checks.

## Decision

Do not relax frame budgets or hide work. Run the build-concurrency A/B; select a production admission change only if lower in-flight concurrency materially reduces snapshot/frame cost without creating holes.
