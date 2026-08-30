# Plan — VoxelShowcase GPU v2

## Observed defect / acceptance
- VoxelShowcase has slow fill, seams, and hitches. Pass requires sustained GPU completion, no GPU-eligible CPU fallback/blocking waits, no holes, moving p95 <18 ms/p99 <25 ms, stationary p95 <8 ms, and reviewed screenshots.
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
- Parallel per-cell exact Planar/Sharp/Cubic and deterministic clump/fringe emission now replace supported-semantic fallback. Metal semantic/publication parity is 5/5 and arena count/write coverage is 7/7. The obsolete raw unsupported scan was removed; publication args and scratch lifetime are GPU-ordered behind an explicit fence.

## Remaining gates
- Replace per-chunk readbacks with bounded descriptor batches, GPU prefix allocation/meshing/args, version-safe publication, and explicit fences. Split 64³ work into bounded units if necessary.
- Add admission, retry, overflow, stale, disposal, arena, and lifecycle coverage.
- Do not run VoxelShowcase while semantic fallback or per-chunk handshakes remain. Then run the 150 s capture, inspect screenshots, review the diff, and keep the issue open unless every gate passes.
