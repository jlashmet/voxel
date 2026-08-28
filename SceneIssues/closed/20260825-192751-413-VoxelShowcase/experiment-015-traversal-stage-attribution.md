# Experiment 015 — traversal stage attribution

## Question

Which production subsystem owns the remaining moving-player frame-time failure after initial camera discovery was fixed?

## Method

Exact targeted CI request `05e8e8b1f03dc7ca901d0a697c9cdd4b2987c6e2` paired the unchanged traversal acceptance with `ShowcaseTraversalProfilingTests.ContinuousTraversalReportsPlayerLoopRenderAndStreamingCosts`. The CI lane launched Unity with 8 job workers. No production budget or assertion was changed.

Run `33039246328`, job `98408862687`, result artifact `9634012424`.

## Result

The unchanged traversal acceptance remained red: p95 `19.57 ms`, p99 `26.94 ms` against the existing `18/25 ms` budgets.

The diagnostic traversal measured:

- total: p50 `13.343 ms`, p95 `20.142 ms`, p99 `27.110 ms`
- scheduler p95 `9.082 ms`
- worker admission p95 `6.762 ms`
- worker prepare p95 `5.977 ms`
- upload p95 `0.159 ms`
- GC collect p95 `0 ms`

Scheduler sub-stages excluded the obvious alternatives:

- change journal p95 `0.002 ms`
- invalidation p95 `0.056 ms`
- discovery p95 `0.105 ms`
- visibility p95 `1.323 ms`
- rule sync p95 `0.001 ms`
- residency prune p95 `0.014 ms`
- capacity p95 `0 ms`
- build selection p95 `0.008 ms`

Worker-side cumulative build timings were much larger, with snapshot p95 `7.676 ms`; density/job turnaround also showed severe backlog (queue latency p95 about `4.2 s`). Every sampled traversal frame still had streaming work.

## Interpretation

Upload, GC, invalidation, discovery, and capacity pressure are not the dominant moving-frame cost. The remaining cost is in worker admission/preparation while the geometry job system is heavily backlogged.

`SnapshotTiming` is a cumulative-per-build metric, so its `7.676 ms` value is not by itself proof of a per-frame snapshot stall. A second experiment must record the existing worker sub-markers per frame before selecting a production change.

## Decision

Keep the traversal thresholds unchanged. Proceed to per-frame worker-stage profiling; do not optimize upload/render budgets or arena size based on this run.
