# Plan — VoxelShowcase GPU v2

## Observed defect / acceptance
- GPU extraction previously saturated Metal with about 5,100 compute and 5,000 upload encoders in 35 seconds; render-queue latency reached 260 ms while CPU main/render time stayed small.
- Pass requires an identical 150 s GPU/CPU comparison, GPU FPS at least 2× CPU, moving p95 <18 ms/p99 <25 ms, stationary p95 <8 ms, sustained completion without fallback/holes, and reviewed screenshots.
- Deterministic integer CPU voxels remain authoritative; GPU geometry is presentation only.

## Hypotheses / discriminating result
- **Confirmed:** per-chunk Unity command buffers were not GPU batching. Dispatch-Y cross-chunk work is required.
- **Falsified:** completion readback was the primary bottleneck. Removing it made poll cost zero but did not fix sustained throughput.
- **Next experiment:** after the complete cutover passes focused tests, run the identical player harness and compare GPU/CPU Profiler and FPS evidence. This distinguishes encoder/dispatch relief from remaining kernel or raster cost.

## Implemented final architecture
- One shared, demand-filled GPU voxel mirror receives changed voxel pages plus compact metadata.
- Near rings (steps 1/2) batch up to eight chunks across count, prefix, all-category generation, transitions, and publication. Unsupported semantic fallback is zero.
- CPU assigns only stable chunk handles and desired generations. GPU page stacks allocate staging banks, generation-check atomic publication, retain the previous live mesh on stale/exhausted updates, and reclaim retired pages after four epochs.
- Production has no GPU count, geometry, allocation, range, completion, or indirect-argument readback. The obsolete CPU arena bridge phases are removed from the worker/coordinator path.
- CPU uploads visible stable handles only. GPU filters live records, buckets/compacts draw metadata, and builds 128 fixed indirect argument records. The shader fetches paged indices/vertices directly. Step 4 feature-preserving and step 8 block HLOD extraction remain CPU by design and use the separate CPU arena.

## Validation / remaining tasks
- GPU page/batch/draw tests pass 16/16; semantic parity passes 8/8. Stale rejection, exhaustion old-mesh retention, retirement delay, handle reuse, all-category writes, and zero production readback are covered. The full architecture suite passes 44/45; its unrelated existing arena source-string assertion expects an older guard in an unchanged file.
- Review the final diff, commit, and push the implementation.
- Then—and only then—run the VoxelShowcase player harness, capture identical GPU/CPU 150 s measurements plus screenshots/Xcode evidence, fix any measured regression, and push the validated branch to remote `gpu-v2`.
