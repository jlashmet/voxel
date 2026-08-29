# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Observed behavior / acceptance
- Exact built-player evidence at `db1230b` reached 636–770 ms solid admission, 757 missing visible chunks, and four drawn. Exact-block/optional-halo iterations removed that stall but still starved or converged too slowly.
- Preserve deterministic CPU world truth, collision, replication, residency, and stale-publication rejection. Presentation may lag behind one immutable generation while old/far geometry covers it.
- Pass requires sustained step-1/step-2 GPU completion, zero eligible CPU fallback/blocking waits, no visible holes, moving p95 <18 ms/p99 <25 ms, stationary p95 <8 ms, and inspected exact-pose built-player evidence.

## Hypotheses / discriminators
- H1 (**confirmed**): whole-region recovery and per-chunk CPU snapshots dominated admission. Demand-only blocks plus direct `RegionReadView` payload copy remove both costs.
- H2 (**confirmed**): global/region-wide mutation gates and optional nonresident halo mismatch starved unrelated work. Exact active footprints and canonical-empty optional halo preserve liveness.
- H3 (**confirmed**): obsolete chunk requests were never withdrawn. A local failure had 358,697 ready and 53,567 pending blocks with zero active extraction; O(1) footprint cancellation restored convergence.
- H4 (**under test**): synchronous 1,000–5,832-block coverage walks and bursty simultaneous compute dispatches create the remaining tail. Per-worker 128-block cursors reduced local moving p95 from 97.624 to 42.250 ms; one count dispatch/frame is the next discriminator.

## Selected fix / material results
- Use one bounded, demand-filled, versioned GPU mirror shared by workers. GPU candidates enter compute before CPU metadata/classification/payload snapshot work; compute classifies raw semantics. Unsupported decorated/faceted/profile content retains the CPU fidelity path.
- Keep geometry GPU-resident through count/reserve/write/draw. Retry transient Metal count/write bookkeeping without CPU fallback.
- Reference-count live demand footprints, discard cancelled queue entries before Storage access, cap retained ready descriptors at 65,536, and evict only inactive undemanded entries. Verify coverage incrementally and reject changed/pending bricks before dispatch.
- Local gates: policy/classification 5/5 passed; real-device arena bridge 4/4 passed with zero geometry readback; liveness passed. Demand cancellation restored settling and zero eligible fallback; latest pre-throttle traversal failed only moving p95 at 42.250 ms with zero count/write retries.

## Remaining gates
- Run merged liveness, traversal, arena, and zero-allocation regressions; inspect exact built-player screenshots/logs.
- Commit final evidence, create one exact feature-parented CI request on `ci-test/fixes/agent-2`, monitor its exact SHA, then update issue state only if every gate passes.
