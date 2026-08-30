# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Observed defect / acceptance
- The VoxelShowcase player shows slow fill, seams, and hitches. Pass requires sustained step-1/2 GPU completion, no eligible CPU fallback/blocking waits, no visible holes, moving p95 <18 ms/p99 <25 ms, stationary p95 <8 ms, and reviewed screenshots.
- CPU integer voxels remain authoritative. Collision, replication, interest, tick rate, and world truth are unchanged; extracted GPU geometry is versioned presentation only.

## Evidence and hypotheses
- **Confirmed foundation:** the shared `GpuVoxelBrickMirror`, recovery, compact scatter, snapshotless admission, stale rejection, and old-mesh retention are correct. Mirror/admission is normally 0.3–4 ms with zero lease failures.
- **Falsified tails:** zero-copy water discovery and 0.794 ms word-wise summaries removed measured CPU costs but not traversal stalls. The actual 150 s player still reached p95 970.31 ms with hundreds of missing visible chunks.
- **Leading:** independent extractors write compute output directly into the large vertex/index arena concurrently consumed by rasterization. One GPU build kept ordinary walking frames near 3–9 ms but could not fill; eight produced recurring 100–350 ms stalls. Reserving every count/write/retry dispatch to one stage per frame improved the eight-build tail to 40–150 ms but still failed, proving burst admission is secondary rather than sufficient.
- **Falsified throughput experiment:** two private-scratch stages per frame left missing at 546 and restored 80–305 ms stalls. Metal extraction saturation, not publication-copy throughput, requires one global stage ticket.

## Selected architecture
- Follow Unreal RDG, Godot RenderingDevice, and Unity URP practice: declare compute/copy/draw access and let graph ordering own barriers/fences.
- Keep the shared voxel mirror as immutable extraction input. Generate into bounded per-context unpublished scratch, never into a buffer currently drawn.
- After completion, copy/compact only the produced ranges into a staging arena lease, then publish indirect args/indirection last. Retain the previous mesh or far fallback until publication; reject stale source versions.
- Remove per-chunk GPU→CPU count/write handshakes in the final path: GPU sizing/allocation and args generation stay GPU-side. CPU observes completion/version only, never geometry truth.
- Bound scratch, arena, in-flight requests, and per-frame GPU work from the device matrix. Prioritize visible holes before prefetch; presentation tiers may vary capacity, never outcomes.

## Gates
- Private scratch plus one global stage cleared moving timing gates; coverage remained 400–542 chunks short. Two scratch stages regressed timing and were reverted.

## Remaining task list
- Run the 8-build actual-player harness under the 10 GB wrapper; record timing, coverage, fallback, and memory results.
- Prove why visible-first demand churns faster than one safe extraction stage can fill; fix without weakening gates.
- Add focused scratch/copy admission, retry, disposal, arena, and lifecycle coverage; rerun relevant EditMode/PlayMode tests.
- Late screenshot inspection confirms broad rectangular ground holes/seams; visual gate remains failed.
- Run the full 150 s acceptance capture; update evidence, review/clean the diff, and keep the SceneIssue open unless every gate passes.
- Follow-up after this safe staging path: express compute/copy/draw access through RenderGraph and replace per-chunk counter readbacks with GPU-side sizing/args.
