# GPU renderer production restoration — GPU-first plan

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through the GPU backend, physically delete the retired CPU-only renderer, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable measured result under [tasks.md](tasks.md). Preserve authoritative CPU storage/generation/collision/simulation and GPU host orchestration. No hidden content, weaker budgets, reduced distance or permanent CPU fallback. All task gates remain mandatory.

## Execution and prior results

User directs local harness/tests and screenshot review; **no further origin pushes**. Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`. Existing remote request/run preserved; local work does not wait on it.

Local watchdog repair passes five behavioral tests. Shared 44-byte allocator descriptor fixes second-record identity corruption. Common-point capacity status write fixes Metal Exhausted/TooLarge results; descriptor aliases/default-store changes alone were falsified. Thirteen focused GPU tests passed previously. Three 180-second player runs remained unacceptable; their incomplete geometry invalidates performance acceptance.

## New discriminating evidence

`Artifacts/LocalGpuShowcase/publication-trace/`: 80-second standalone Metal capture, 1920x1080, five PNGs. Temporary opt-in synchronous readbacks are archived as `diagnostic-source.diff` and removed from runtime. At 60s, visible=461, live-ready=295, empty=208, pending=0, indirect instances=253: all nonempty live records reach draw compaction. Vertex arena has one free page; 259 allocations report Exhausted by 65s. Host fence completion conceals rejected geometry. First rejection: handle408, origin(640,0,640), step2, 156812 vertices. Global publication-pump failure is falsified; capacity failure is proven.

Separately, direct `SV_InstanceID` addressing ignored nonzero indirect bucket prefixes on local Metal. Explicit GPU bucket-prefix lookup with zero indirect start-instance restores most castle facade/towers in `explicit-bucket-offset/` standalone screenshots. A production-shader raster test renders three spatially separated buckets: previous shaders fail at handle1, fixed shaders pass. The 600-handle compaction test also passes. Final focused publication/draw regression results are recorded in tasks.md.

Visual classification remains **unacceptable**: missing capacity-rejected chunks, fragmented structures, cyan water, blockout far terrain and seams. No performance claim; late replay GPU timing windows have zero samples.

## Hypotheses and next experiment

1. Unmerged faceted emission consumes the fixed arena excessively; exact semantic-preserving GPU face merging can reduce demand.
2. Residency/reclamation retains unnecessary geometry or loses recoverable rejected requests; correct eviction/retry can restore coverage within budget.

Next correlate arena ownership/demand with per-chunk geometry counts, then implement the earliest proven capacity/recovery fix. Keep budgets and scene content unchanged. Validate exact boundary/material coverage independently and rerun the full player, including traversal.

## Remaining gates

Full-scene coverage/visuals -> GPU step4/8/water migration -> physical CPU-backend deletion -> independent-consumer/edit/lifecycle proof -> locked repeated performance/memory workloads -> final local regression/artifact review.
