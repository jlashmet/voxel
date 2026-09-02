# GPU renderer production restoration — implementation plan

**Target:** `Assets/VoxelEngine/Rendering` GPU surface extraction, persistent mirror, production cutover, and production consumers.  
**Starting SHA:** `b18d470f66221c7cb6091249f4683c2d994bffec` (current `origin/master` and `fixes/agent-1` when investigation resumed).

## Observed behavior

Exact-SHA targeted run `33665456593` reproduced the density divergence on current master in the existing production oracle `GpuDensityMatchesTheCpuJobSampleForSample(1)`: **1300/2197 samples disagree**, worst at sample 0 (`CPU 0.50000`, `GPU -0.14000`). Unity/Metal compilation also warns that `IsSolidSample`, `ReadMaterial`, `AddTap`, and `SampleField` may use uninitialized state in the full `VoxelBrickMesher` kernels. This is a product failure, not runner infrastructure (33 GB free at launch, 6.1 GB peak RSS).

Exact-SHA diagnostic run `33666261165` then localized the first divergence to world voxel `(-2,-2,-2)`: density CPU `0.50000` vs GPU `-0.14000`; material CPU/GPU both `1`; boundary CPU/GPU both `0`; surface CPU `0x04000001` vs GPU `0x00000001`. The GPU therefore preserves the expected presentation style/material but loses the transient authoritative-solid bit while computing a negative smooth density. This makes a raw cache/material fetch defect less likely and directly implicates `centreSolid` or the full smooth `SampleField` execution path.

## Acceptance

1. GPU density/sample semantics match the real CPU jobs for every supported reconstruction path and supported source step exercised by production.
2. Regular topology, attributed geometry, negative-shell ownership, transition faces, faceted surfaces, coatings/decorations, and material semantics match CPU expectations where GPU support is claimed.
3. Persistent mirror publication, edits, eviction/recovery, generation handling, and world-coordinate lookup remain correct with no stale/wrong-brick rendering.
4. VoxelShowcase and at least one independent production consumer render GPU-eligible solid chunks through GPU extraction with zero silent eligible CPU fallbacks.
5. Built-player traversal/edit evidence is visually production-correct: no holes, cracks, stale geometry, missing surfaces, wrong materials, or fallback-hidden success.
6. Frame-path blocking, frame latency, upload/memory, and committed GPU resource cost remain within repository budgets.

## Hypotheses / next experiment

- **H1:** full-mesher compilation/execution corrupts `SampleField` centre occupancy or smooth-tap state even though material lookup data survives. The Metal uninitialized-state warnings and missing authoritative-solid bit strengthen this hypothesis.
- **H2:** shared production state/buffer binding or persistent-lookup mode contaminates otherwise-correct sampling. This is weaker because the failing diagnostic uses the explicit dense cache and returns the expected material/boundary.

Next run the same full mesher and uniform-brick bindings under material-default `Planar` and `Smooth` reconstruction. Planar takes the `centreSolid` early return before `AddTap`; Smooth executes the weighted tap path. If Planar is correct while Smooth fails, isolate to smooth/tap codegen. If Planar also treats the same voxel as air, isolate `centreSolid`/`IsSolidSample` in the full method before any production fix.

## Architecture / blast radius

Keep CPU voxel/storage truth authoritative. GPU code is a derived presentation backend. Fix shared GPU rendering semantics rather than VoxelShowcase policy. Unsupported inputs must be explicit eligibility results, not wrong geometry or incidental fallback. Preserve the existing world-scoped mirror and production composition unless evidence proves a boundary defect.

Production inventory confirms the current cutover policy defaults GPU **off** unless `VOXEL_ENABLE_EXPERIMENTAL_GPU_CUTOVER=1`; source steps 1/2 are the claimed GPU-supported solid rings; device/context failures increment eligible GPU fallback counters and resume CPU density extraction. GPU semantics already implement all five built-in reconstruction modes plus material-default/blend, water classification, coating/boundary, regular/faceted/negative-shell/transition/profile paths; unsupported results are classified by reconstruction/decoration semantics.

## Remaining gates

Discriminate root cause -> focused regression/fix -> CPU/GPU semantic suite -> explicit no-silent-fallback/recovery contract -> automatic module validation -> exact-SHA built-player VoxelShowcase plus independent-consumer evidence -> performance/memory review -> close.
