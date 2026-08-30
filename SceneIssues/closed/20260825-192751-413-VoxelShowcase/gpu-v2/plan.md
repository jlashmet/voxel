# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Observed defect / acceptance
- The VoxelShowcase player shows slow fill, seams, and hitches. Pass requires sustained step-1/2 GPU completion, no eligible CPU fallback/blocking waits, no visible holes, moving p95 <18 ms/p99 <25 ms, stationary p95 <8 ms, and reviewed screenshots.
- CPU integer voxels remain authoritative; GPU geometry is presentation only.

## Evidence and hypotheses
- **Confirmed foundation:** shared mirror, recovery, compact scatter, snapshotless admission, stale rejection, and old-mesh retention are correct; admission is 0.3–4 ms with zero lease failures.
- **Falsified tails:** zero-copy water discovery and word-wise summaries removed CPU costs but not traversal stalls; the 150 s player reached p95 970.31 ms with hundreds missing.
- **Confirmed stall owner/fix:** eight direct live-arena extractors caused 100–350 ms stalls. Private per-context scratch plus one global stage produced late p95 15.7–17.6 ms/p99 16.8–17.9 ms; coverage still missed 400–542 chunks.
- **Falsified throughput:** two scratch stages/frame left 546 missing and restored 80–305 ms stalls. Metal saturation requires one global ticket.

## Selected architecture
- Follow Unreal RDG, Godot RenderingDevice, and Unity URP: declare resource access; graph ordering owns barriers/fences.
- Keep the mirror immutable. Generate into bounded unpublished scratch, never a drawn buffer.
- Copy produced ranges into a staging lease, then publish args/indirection last. Retain old/far geometry until publication; reject stale versions.
- Final GPU sizing/allocation/args remove per-chunk readbacks. CPU observes completion/version, never geometry truth.
- Bound resources/work from the device matrix. Prioritize holes; tiers vary capacity, never outcomes.

## Gates
- Private scratch plus one global stage cleared moving timing gates; coverage remained 400–542 chunks short. Two scratch stages regressed timing and were reverted.
- Baseline: p95 16.7–17.5 ms, missing 598, final 30 s 396 counts/120 publications, age 1.32 s. Removing write readback without a scratch fence caused p95 201 ms; reverted.
- Funnel split found 1,162/1,703 counts rejected for reconstruction versus one decoration. A GPU exact-face greedy pass eliminated fallback and raised publications to 1,571 by 59 s, but serialized plane merging drove p95 to 49.92 ms, age 1.85 s, and missing to 571; reverted. Parallel compaction is required.
- GPU classification now zeros indirect density-dispatch args for unsupported chunks. At 59 s counts rose 1,703→1,897 and publications 541→588, but walking p99 hit 38.29 ms; acceptance still fails.

## Remaining task list
- Funnel telemetry records request → mirror → count → write → copy → publish, rejection categories, age, stale, retry, and in-flight counts.
- Replace readbacks with bounded descriptor batches, parallel GPU prefix allocation/meshing/face compaction, version-safe args, and explicit fences before scratch reuse.
- Split extraction into measured bounded GPU work units if full 64³ kernels still prevent safe batch progress; do not merely raise concurrency.
- Add scratch/copy admission, retry, disposal, arena, and lifecycle coverage; rerun relevant tests.
- Late screenshot inspection confirms broad rectangular ground holes/seams; visual gate remains failed.
- Run the full 150 s acceptance capture; update evidence, review/clean the diff, and keep the SceneIssue open unless every gate passes.
