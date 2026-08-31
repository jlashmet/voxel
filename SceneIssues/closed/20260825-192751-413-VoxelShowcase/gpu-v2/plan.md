# Plan — VoxelShowcase GPU v2

## Observed defect / acceptance
- VoxelShowcase has slow fill, seams, and hitches. Pass requires GPU FPS at least 2× an identical CPU-backend run, sustained completion, no eligible fallback/blocking waits or holes, moving p95 <18 ms/p99 <25 ms, stationary p95 <8 ms, and reviewed screenshots.
- Deterministic integer CPU voxels remain authoritative; GPU geometry is presentation.

## Evidence and hypotheses
- **Confirmed foundation:** shared mirror, recovery, compact scatter, snapshotless admission, stale rejection, and old-mesh retention work; admission is 0.3–4 ms with zero lease failures.
- **Falsified tails:** zero-copy water discovery and word summaries removed CPU costs but not traversal stalls; a 150 s run reached p95 970 ms with hundreds missing.
- **Confirmed stall owner:** eight direct live-arena extractors caused 100–350 ms stalls. Private scratch plus one global stage produced late p95 15.7–17.6 ms/p99 16.8–17.9 ms, but 400–542 chunks remained missing. Two stages/frame restored stalls.
- **Next discriminating experiment:** complete parallel semantic emission and GPU-owned batch publication, then measure whether one bounded batch raises publication throughput without exceeding timing gates.

## Selected architecture
- Follow render-graph practice: immutable mirror input; bounded unpublished scratch output; explicit ordering/fences; copy to an invisible arena lease; publish args/version last; retain old geometry until publication.
- Implement all GPU-eligible semantics before scene measurement: continuous Smooth/Rounded, exact Planar/Sharp/Cubic, deterministic clump/fringe decorations, transitions, batched sizing/allocation/args, and fence-owned scratch reuse.
- CPU observes completion/version, never geometry. Bound work/resources from the device matrix; prioritize holes.

## Material results
- Baseline p95 16.7–17.5 ms, missing 598. Removing write verification without a scratch fence caused p95 201 ms; reverted.
- 1,162/1,703 counts rejected for reconstruction versus one decoration. Serialized exact-face greedy emission removed fallback and raised publications, but p95 became 49.92 ms; reverted. Parallel compaction is required.
- GPU classification skips density dispatch for unsupported chunks; throughput rose slightly, walking p99 remained 38.29 ms.
- Parallel exact Planar/Sharp/Cubic and deterministic clump/fringe emission replace supported-semantic fallback; the raw scan was removed. Reserved write/copy/args/scratch are one fence-ordered stage, eliminating write readback and a third dispatch frame. Arena count/write is 7/7; policy is 5/5.
- Double-buffered two-descriptor lanes now perform one shared transfer, GPU-aligned prefix/totals, one atomic arena reservation, version-tokened subleases, and GPU args. Workers retain private sampled fields. Metal semantic/publication/batch/pressure coverage is 8/8; per-chunk readback and allocation searches are gone.

## Remaining gates
- Complete stale/disposal and production coordinator coverage; keep retry, overflow, arena, and lifecycle gates green.
- Run identical actual-app CPU/GPU 150 s captures, inspect screenshots, and split 64³ work only if timing shows a large-stage stall. Review the diff and keep the issue open unless every gate, including 2× FPS, passes.
