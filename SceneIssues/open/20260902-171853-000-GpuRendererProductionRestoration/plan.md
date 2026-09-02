# GPU renderer production restoration — implementation plan

**Target:** `Assets/VoxelEngine/Rendering` GPU surface extraction, persistent mirror, production cutover, and production consumers.  
**Starting SHA:** `b18d470f66221c7cb6091249f4683c2d994bffec` (current `origin/master` and `fixes/agent-1` when investigation resumed).

## Observed behavior

Exact-SHA targeted run `33665456593` reproduced the density divergence on current master in the existing production oracle `GpuDensityMatchesTheCpuJobSampleForSample(1)`: **1300/2197 samples disagree**, worst at sample 0 (`CPU 0.50000`, `GPU -0.14000`). Unity/Metal compilation also warns that `IsSolidSample`, `ReadMaterial`, `AddTap`, and `SampleField` may use uninitialized state in the full `VoxelBrickMesher` kernels. This is a product failure, not runner infrastructure (33 GB free at launch, 6.1 GB peak RSS).

Exact-SHA diagnostic run `33666261165` localized the first divergence to world voxel `(-2,-2,-2)`: density CPU `0.50000` vs GPU `-0.14000`; material CPU/GPU both `1`; boundary CPU/GPU both `0`; surface CPU `0x04000001` vs GPU `0x00000001`. The GPU preserves presentation style/material but loses the transient authoritative-solid bit.

Exact-SHA discriminator run `33666796147` ran the same full shader/cache under material-default Planar and Smooth. **Both are identically wrong** at `(-2,-2,-2)` (CPU +0.50, GPU -0.14; material/style correct; authoritative-solid bit missing). Planar returns from `SampleField` immediately after `centreSolid`, before weighted `AddTap`, so a smooth-tap-only defect is falsified. The failure is in or above centre occupancy. The public storage wire enum is `Empty=0, Uniform=1, Mixed=2`, matching the shader's hardcoded convention, and `PackBrickCacheEntry` writes the content value directly into the low bits.

A first production attempt to mirror the water mask from `SolidMaterialClassification.SetWaterMaterialMask` did not change the parity symptom on run `33667605313`; production already publishes `_SolidWaterMaterialMask` at `VoxelMaterialPresentationInstaller.Apply`, so that duplicate ownership was reverted.

The isolated include probe `GpuSolidClassificationProbeTests.MaterialOneIsSolidWhenWaterMaskIsZero` then passed on retry attempt 2 of exact-SHA run `33668009978` after attempt 1 suffered a native Burst import crash. The probe includes the same `VoxelBrickDensity.hlsl`, observes `_SolidWaterMaterialMask == 0`, and confirms `IsSolidSample(1) == true` on Metal. Therefore the helper expression and global scalar work in isolation; the failure requires the full `VoxelBrickMesher.compute` compilation/execution context.

Exact-SHA run `33668767375` then bound `_SolidWaterMaterialMask=0` directly on the production `ComputeShader` instance before the same full-mesher Planar/Smooth discriminator. The result was unchanged: Planar and Smooth both returned GPU `-0.14`, material `1`, correct resolved style, boundary `0`, and no authoritative-solid bit. That falsifies global-vs-compute-local mask binding as the cause.

The required minimal-root-cause isolation is now complete. Exact-SHA run `33669435649` compiled the complete production mesher into a test-only probe and proved a normal local `SampleField` call returns material 1, solid occupancy, and density `+0.5`. Run `33670311041` then removed the sample kernel's read-only aliases and still reproduced `-0.14`, falsifying SRV/UAV aliasing. Run `33671055825` reproduced the production 64-thread dispatch shape while reporting thread-zero state and proved the computed coordinate `(-2,-2,-2)`, `_BrickCacheOrigin=(-1,-1,-1)`, edge `4`, cache word `0x00000101`, brick index `0`, raw material `1`, direct solid classification, and local `SampleField=+0.5` are all correct. Finally, exact-SHA run `33671401799` changed only the expression shape: assigning `SampleField` to a local returns `+0.5`, while writing the same function call directly into the UAV (`_DensityWrite[index] = SampleField(... out locals ...)`) returns `-0.14` and drops authoritative occupancy. This reproduces the production failure without changing coordinate, cache, classifier, catalogue, or density semantics and matches the Metal uninitialized-state warnings. The same hazardous expression exists in both `CSSampleDensity` and production `CSBatchSampleDensity`; transition-face sampling does not use `SampleField`.

## Acceptance

1. GPU density/sample semantics match the real CPU jobs for every supported reconstruction path and supported source step exercised by production.
2. Regular topology, attributed geometry, negative-shell ownership, transition faces, faceted surfaces, coatings/decorations, and material semantics match CPU expectations where GPU support is claimed.
3. Persistent mirror publication, edits, eviction/recovery, generation handling, and world-coordinate lookup remain correct with no stale/wrong-brick rendering.
4. VoxelShowcase and at least one independent production consumer render GPU-eligible solid chunks through GPU extraction with zero silent eligible CPU fallbacks.
5. Built-player traversal/edit evidence is visually production-correct: no holes, cracks, stale geometry, missing surfaces, wrong materials, or fallback-hidden success.
6. Frame-path blocking, frame latency, upload/memory, and committed GPU resource cost remain within repository budgets.

## Proven root cause / next fix

The density mismatch is a Metal compiler/code-generation hazard caused by directly assigning the return value of `SampleField`, which also fills three `out` locals, into a UAV. The identical function/input state is correct when the return value is first materialized in a local scalar. TGPU-006 is therefore satisfied and the next allowed production change is deliberately narrow: in both `CSSampleDensity` and `CSBatchSampleDensity`, evaluate `SampleField` into a local `float density` and then write that scalar to the output UAV. Do not change classifier rules, cache addressing, material policy, or CPU semantics. Validate the original CPU/GPU density oracle and a batched-production sampling consumer on the exact resulting SHA before advancing TGPU-010.

## Architecture / blast radius

Keep CPU voxel/storage truth authoritative. GPU code is a derived presentation backend. Fix shared GPU rendering semantics rather than VoxelShowcase policy. Unsupported inputs must be explicit eligibility results, not wrong geometry or incidental fallback. Preserve the existing world-scoped mirror and production composition unless evidence proves a boundary defect.

Production inventory confirms the current cutover policy defaults GPU **off** unless `VOXEL_ENABLE_EXPERIMENTAL_GPU_CUTOVER=1`; source steps 1/2 are the claimed GPU-supported solid rings; device/context failures increment eligible GPU fallback counters and resume CPU density extraction. GPU semantics already implement all five built-in reconstruction modes plus material-default/blend, water classification, coating/boundary, regular/faceted/negative-shell/transition/profile paths; unsupported results are classified by reconstruction/decoration semantics.

## Remaining gates

Proven density root-cause fix -> focused parity validation -> CPU/GPU semantic suite -> explicit no-silent-fallback/recovery contract -> automatic module validation -> exact-SHA built-player VoxelShowcase plus independent-consumer evidence -> performance/memory review -> close.
