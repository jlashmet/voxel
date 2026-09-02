# GPU renderer production restoration — implementation plan

**Target:** `Assets/VoxelEngine/Rendering` GPU surface extraction, persistent mirror, production cutover, and production consumers.  
**Starting SHA:** `b18d470f66221c7cb6091249f4683c2d994bffec` (current `origin/master` and `fixes/agent-1` when investigation resumed).

## Observed behavior

Exact-SHA targeted run `33665456593` reproduced the density divergence on current master in the existing production oracle `GpuDensityMatchesTheCpuJobSampleForSample(1)`: **1300/2197 samples disagree**, worst at sample 0 (`CPU 0.50000`, `GPU -0.14000`). Unity/Metal compilation also warns that `IsSolidSample`, `ReadMaterial`, `AddTap`, and `SampleField` may use uninitialized state in the full `VoxelBrickMesher` kernels. This is a product failure, not runner infrastructure (33 GB free at launch, 6.1 GB peak RSS). The prior raw-read diagnostic remains only a clue: the current failure is proven in the real full-mesher path.

## Acceptance

1. GPU density/sample semantics match the real CPU jobs for every supported reconstruction path and supported source step exercised by production.
2. Regular topology, attributed geometry, negative-shell ownership, transition faces, faceted surfaces, coatings/decorations, and material semantics match CPU expectations where GPU support is claimed.
3. Persistent mirror publication, edits, eviction/recovery, generation handling, and world-coordinate lookup remain correct with no stale/wrong-brick rendering.
4. VoxelShowcase and at least one independent production consumer render GPU-eligible solid chunks through GPU extraction with zero silent eligible CPU fallbacks.
5. Built-player traversal/edit evidence is visually production-correct: no holes, cracks, stale geometry, missing surfaces, wrong materials, or fallback-hidden success.
6. Frame-path blocking, frame latency, upload/memory, and committed GPU resource cost remain within repository budgets.

## Hypotheses / next experiment

- **H1:** full-mesher compilation/execution corrupts `SampleField` centre occupancy or smooth-tap state even though isolated raw material reads are correct. The Metal uninitialized-state warnings strengthen this hypothesis but do not yet prove causality.
- **H2:** shared production state/buffer binding or persistent-lookup mode contaminates otherwise-correct sampling.

Next compare (a) isolated raw read, (b) planar early-return `SampleField`, and (c) smooth full `SampleField` in the same full mesher/bindings. This discriminates lookup/binding from smooth-field logic/codegen before changing production behavior.

## Architecture / blast radius

Keep CPU voxel/storage truth authoritative. GPU code is a derived presentation backend. Fix shared GPU rendering semantics rather than VoxelShowcase policy. Unsupported inputs must be explicit eligibility results, not wrong geometry or incidental fallback. Preserve the existing world-scoped mirror and production composition unless evidence proves a boundary defect.

Production inventory already confirms the current cutover policy defaults GPU **off** unless `VOXEL_ENABLE_EXPERIMENTAL_GPU_CUTOVER=1`; source steps 1/2 are the claimed GPU-supported solid rings; device/context failures increment eligible GPU fallback counters and resume CPU density extraction. TGPU-002 will finish tracing all selection/publication/fallback paths before changing that policy.

## Remaining gates

Complete production-path inventory -> discriminate root cause -> focused regression/fix -> CPU/GPU semantic suite -> explicit no-silent-fallback/recovery contract -> automatic module validation -> exact-SHA built-player VoxelShowcase plus independent-consumer evidence -> performance/memory review -> close.
