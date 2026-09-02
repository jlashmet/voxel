# GPU renderer production restoration — implementation plan

**Target:** `Assets/VoxelEngine/Rendering` GPU surface extraction, persistent mirror, production cutover, and production consumers.  
**Starting SHA:** `b18d470f66221c7cb6091249f4683c2d994bffec` (current `origin/master` and `fixes/agent-1` when investigation resumed).

## Observed behavior

Exact-SHA targeted run `33665456593` reproduced the density divergence on current master in the existing production oracle `GpuDensityMatchesTheCpuJobSampleForSample(1)`: **1300/2197 samples disagree**, worst at sample 0 (`CPU 0.50000`, `GPU -0.14000`). Unity/Metal compilation also warns that `IsSolidSample`, `ReadMaterial`, `AddTap`, and `SampleField` may use uninitialized state in the full `VoxelBrickMesher` kernels. This is a product failure, not runner infrastructure (33 GB free at launch, 6.1 GB peak RSS).

Exact-SHA diagnostic run `33666261165` localized the first divergence to world voxel `(-2,-2,-2)`: density CPU `0.50000` vs GPU `-0.14000`; material CPU/GPU both `1`; boundary CPU/GPU both `0`; surface CPU `0x04000001` vs GPU `0x00000001`. The GPU preserves presentation style/material but loses the transient authoritative-solid bit.

Exact-SHA discriminator run `33666796147` ran the same full shader/cache under material-default Planar and Smooth. **Both are identically wrong** at `(-2,-2,-2)` (CPU +0.50, GPU -0.14; material/style correct; authoritative-solid bit missing). Planar returns from `SampleField` immediately after `centreSolid`, before weighted `AddTap`, so a smooth-tap-only defect is falsified. The public storage wire enum is `Empty=0, Uniform=1, Mixed=2`, matching the shader's hardcoded convention, and `PackBrickCacheEntry` writes the content value directly into the low bits.

A first production attempt to mirror the water mask from `SolidMaterialClassification.SetWaterMaterialMask` did not change the parity symptom on run `33667605313`; production already publishes `_SolidWaterMaterialMask` at `VoxelMaterialPresentationInstaller.Apply`, so that duplicate ownership was reverted.

The isolated include probe `GpuSolidClassificationProbeTests.MaterialOneIsSolidWhenWaterMaskIsZero` then passed on retry attempt 2 of exact-SHA run `33668009978` after attempt 1 suffered a native Burst import crash. The probe includes the same `VoxelBrickDensity.hlsl`, observes `_SolidWaterMaterialMask == 0`, and confirms `IsSolidSample(1) == true` on Metal. Therefore the helper expression and global scalar work in isolation; the failure requires the full `VoxelBrickMesher.compute` compilation/execution context.

Exact-SHA run `33668767375` then bound `_SolidWaterMaterialMask=0` directly on the production `ComputeShader` instance before the same full-mesher Planar/Smooth discriminator. The result was unchanged: Planar and Smooth both returned GPU `-0.14`, material `1`, correct resolved style, boundary `0`, and no authoritative-solid bit. That falsifies global-vs-compute-local mask binding as the cause.

Runs `33669435649`, `33670311041`, `33671055825`, and `33671401799` established a useful compiler-shaping repro: in the current full shader, local `SampleField` evaluation can return `+0.5` while the production sampling kernel returns `-0.14`; SRV/UAV aliasing, coordinates, dense-cache addressing, material reads, and the scalar solid classifier were all independently falsified. However subsequent history evidence disproved the stronger conclusion that the direct `SampleField(... out ...)`-to-UAV expression itself is the root cause. Exact history run `33677903232` executed both production density oracle cases on passing commit `5716e56a0f72fadedda54c8a5727f5dd61ca60ee`, whose `CSSampleDensity` uses the same direct UAV assignment shape, and both cases passed. Exact wrapper run `33677993597` also materialized the return value locally while retaining the current density include and still reproduced Planar/Smooth GPU `-0.14`. Therefore the expression shape is only a reproducer/compiler-shaping discriminator, not the introducing defect.

Historical probe run `33678983799` pins the regression to immediate child commit `b4de1b576dfb06821ada42b6094bf9cbe7c9c31f` (`Resolve persistent mirror bricks on GPU`): the preceding `5716e56` passes both source steps, while `b4de1b5` fails step 1 at 1300/2197 and step 2 at 645/2197, both CPU +0.50 vs GPU +0.26. That commit adds persistent GPU directory resolution to `VoxelBrickDensity.hlsl`; dense oracle data do not intentionally execute that branch.

Three later construct-level discriminators further narrow the compiler defect without fixing semantics. Run `33679450700` removed only the nested persistent helper `out uint entry`; the wrong result changed from the later `-0.14` signature to GPU `+0.10`. Run `33680452567` forced the persistent-vs-dense selection with `[branch]`; the result stayed GPU `+0.10`. Run `33681093762` kept the same dynamic directory probe but remapped its explicit `[loop]` optimization attribute to `[fastopt]`; Planar and Smooth again stayed GPU `+0.10` with material/style correct and the authoritative-solid bit absent. Therefore nested `out`, branch flattening, and the explicit loop optimization attribute are not the sole trigger. The stable boundary is the persistent-directory helper body/compiler context itself being reachable from `ReadMaterial` in the density shader compilation unit.

## Acceptance

1. GPU density/sample semantics match the real CPU jobs for every supported reconstruction path and supported source step exercised by production.
2. Regular topology, attributed geometry, negative-shell ownership, transition faces, faceted surfaces, coatings/decorations, and material semantics match CPU expectations where GPU support is claimed.
3. Persistent mirror publication, edits, eviction/recovery, generation handling, and world-coordinate lookup remain correct with no stale/wrong-brick rendering.
4. VoxelShowcase and at least one independent production consumer render GPU-eligible solid chunks through GPU extraction with zero silent eligible CPU fallbacks.
5. Built-player traversal/edit evidence is visually production-correct: no holes, cracks, stale geometry, missing surfaces, wrong materials, or fallback-hidden success.
6. Frame-path blocking, frame latency, upload/memory, and committed GPU resource cost remain within repository budgets.

## Current root-cause boundary / repair direction

The introducing defect is not the UAV assignment, nested `out`, branch hint, or loop hint. The latest evidence localizes the Metal miscompile to the persistent-directory lookup implementation being part of the same reachable density-sampling compiler path, even when dense mode does not execute it. After repeated materially different failed syntax fixes, do not keep reshaping the same function. The next production change should isolate persistent world-brick resolution from density sampling while preserving GPU-only mirror semantics — for example, resolve the required persistent brick entries into GPU-owned dense per-chunk/per-batch cache storage before density dispatch, then let the proven dense `ReadMaterial` path service sampling. This must not reintroduce CPU per-chunk brick staging or weaken persistent mirror correctness.

## Architecture / blast radius

Keep CPU voxel/storage truth authoritative. GPU code is a derived presentation backend. Fix shared GPU rendering semantics rather than VoxelShowcase policy. Unsupported inputs must be explicit eligibility results, not wrong geometry or incidental fallback. Preserve the existing world-scoped mirror and production composition unless evidence proves a boundary defect.

Production inventory confirms the current cutover policy defaults GPU **off** unless `VOXEL_ENABLE_EXPERIMENTAL_GPU_CUTOVER=1`; source steps 1/2 are the claimed GPU-supported solid rings; device/context failures increment eligible GPU fallback counters and resume CPU density extraction. GPU semantics already implement all five built-in reconstruction modes plus material-default/blend, water classification, coating/boundary, regular/faceted/negative-shell/transition/profile paths; unsupported results are classified by reconstruction/decoration semantics.

## Remaining gates

Root-cause isolate -> focused density parity validation -> CPU/GPU semantic suite -> explicit no-silent-fallback/recovery contract -> automatic module validation -> exact-SHA built-player VoxelShowcase plus independent-consumer evidence -> performance/memory review -> close.
