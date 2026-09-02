# Plan — VoxelShowcase GPU v2

## Observed behavior / acceptance
- The old per-chunk path saturated Metal with thousands of small encoders and up to 260 ms render-queue latency. Deterministic integer CPU voxels remain authoritative; GPU geometry is presentation only.
- Pass requires identical 150 s GPU/CPU captures: GPU FPS at least 2x CPU, moving p95 <18 ms/p99 <25 ms, stationary p95 <8 ms, no fallback/holes, sustained completion, and reviewed screenshots.

## Hypotheses and next experiment
- **Confirmed:** removing counter/geometry readback alone did not solve throughput; unbounded, coarse compute chains accumulated GPU queue debt.
- **Confirmed:** nonblocking CPU-synchronization fences plus four two-chunk cross-chunk lanes preserve eight-chunk admission while eliminating the queue stalls. A 90 s real-player traversal had late p95 3-11 ms, GPU p95 2-3 ms, and effectively zero present wait.
- **Fixed gap:** retained profile blocks were excluded before GPU staging. GPU batch count/write now consumes packed profile descriptors, emits authored profile geometry, and suppresses owned continuous triangles without CPU fallback. Requests continue through the settlement, but a first 90 s traversal still reached 688 missing chunks.
- **Next discriminating experiment:** rerun the identical traversal with the profile AABB fast reject. If coverage remains incomplete, profile spatial indexing or scheduler residency/LOD publication—not queue admission—is the next target.

## Selected architecture
- One demand-filled GPU voxel mirror receives changed voxel pages and compact metadata.
- Steps 1/2 use four independent two-chunk chains for count, prefix, all-category generation, transitions, retained profiles, page allocation, and generation-checked publication. GPU fences provide queue backpressure only; no count, geometry, allocation, range, completion payload, or indirect-argument data is read back.
- CPU assigns stable handles/generations and uploads visible handles. GPU page stacks allocate/retire geometry, filter live records, compact draw metadata, build fixed indirect args, and render paged vertices directly. Step 4 feature-preserving extraction and step 8 block HLOD remain CPU by design.

## Material validation / remaining gates
- Passing: exact profile count/write parity 1/1; normal parity 3/3; semantic parity 8/8; page/batch/bridge 17/17; focused no-readback architecture invariant 1/1. Reviewer fixes carry explicit planar intent, wrap-safe capacity checks, editor-gated blocking legacy APIs, the documented 17-word record, and a Dynamic visible-handle buffer.
- Finish the AABB traversal and resolve any remaining coverage deficit; then run identical 150 s GPU/CPU captures, screenshot/Profiler review, and final diff review.
