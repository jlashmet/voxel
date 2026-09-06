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

## Capacity experiment and next discriminator

`category-trace/`: completed 85-second standalone capture, four PNGs; temporary category readbacks archived and removed. Across 849 requests before70s: regular36,184,979 vertices, faceted26,342,636, decoration6,220 (before transitions/profiles). Step2 alone contributes regular30,655,946 and faceted19,388,944 across315 requests. These are requested counts, not simultaneous residency. Decorations are falsified as the dominant pressure source. Faceted merging alone cannot resolve the measured demand. Scheduler relief watches only `_geometryArena` (CPU), so GPU rejection bypasses recovery.

1. Step2 regular geometry is legitimately large but duplicates shareable vertices; compact representation can fit the fixed budget.
2. Step2 prepared density/reconstruction produces excess surfaces; compression would hide an extraction defect.

Next capture actual prepared inputs and pre-page geometry for one high-count step2 chunk: origin(0,256,512), observed216691 regular vertices. Compare occupancy/density boundary expectations and duplicate geometry before choosing vertex reuse or extraction repair. Independently require GPU-owned bounded rejection/retry and truthful host readiness. No budget/content changes.

## Remaining gates

Full-scene coverage/visuals -> GPU step4/8/water migration -> physical CPU-backend deletion -> independent-consumer/edit/lifecycle proof -> locked repeated performance/memory workloads -> final local regression/artifact review.
