# GPU renderer production restoration — implementation plan

**Target:** `Assets/VoxelEngine/Rendering` GPU surface extraction, persistent mirror, production cutover, and production consumers.  
**Starting SHA:** `b18d470f66221c7cb6091249f4683c2d994bffec` (current `origin/master` and `fixes/agent-1` when investigation resumed).

## Observed behavior

Exact-SHA targeted run `33665456593` reproduced the density divergence on current master in the existing production oracle `GpuDensityMatchesTheCpuJobSampleForSample(1)`: **1300/2197 samples disagree**, worst at sample 0 (`CPU 0.50000`, `GPU -0.14000`). Unity/Metal compilation also warns that `IsSolidSample`, `ReadMaterial`, `AddTap`, and `SampleField` may use uninitialized state in the full `VoxelBrickMesher` kernels. This is a product failure, not runner infrastructure (33 GB free at launch, 6.1 GB peak RSS).

Exact-SHA diagnostic run `33666261165` localized the first divergence to world voxel `(-2,-2,-2)`: density CPU `0.50000` vs GPU `-0.14000`; material CPU/GPU both `1`; boundary CPU/GPU both `0`; surface CPU `0x04000001` vs GPU `0x00000001`. The GPU preserves presentation style/material but loses the transient authoritative-solid bit.

Exact-SHA discriminator run `33666796147` then ran the same full shader/cache under material-default Planar and Smooth. **Both are identically wrong** at `(-2,-2,-2)` (CPU +0.50, GPU -0.14; material/style correct; authoritative-solid bit missing). Planar should return from `SampleField` immediately after `centreSolid` and before any weighted `AddTap`, so a smooth-tap-only defect is falsified. The failure is in or above centre occupancy. The public storage wire enum is `Empty=0, Uniform=1, Mixed=2`, matching the shader's hardcoded convention, and `PackBrickCacheEntry` writes the content value directly into the low bits; kind encoding is not the leading hypothesis.

## Acceptance

1. GPU density/sample semantics match the real CPU jobs for every supported reconstruction path and supported source step exercised by production.
2. Regular topology, attributed geometry, negative-shell ownership, transition faces, faceted surfaces, coatings/decorations, and material semantics match CPU expectations where GPU support is claimed.
3. Persistent mirror publication, edits, eviction/recovery, generation handling, and world-coordinate lookup remain correct with no stale/wrong-brick rendering.
4. VoxelShowcase and at least one independent production consumer render GPU-eligible solid chunks through GPU extraction with zero silent eligible CPU fallbacks.
5. Built-player traversal/edit evidence is visually production-correct: no holes, cracks, stale geometry, missing surfaces, wrong materials, or fallback-hidden success.
6. Frame-path blocking, frame latency, upload/memory, and committed GPU resource cost remain within repository budgets.

## Hypotheses / next experiment

- **H1 (leading):** the full Metal mesher miscompiles or otherwise mis-evaluates centre occupancy (`IsSolidSample` or its immediate control flow) even though `ReadMaterial` returns the expected material/style/boundary. This fits the missing authoritative-solid bit, identical Planar/Smooth failure, and compiler warnings around `IsSolidSample`/`SampleField`.
- **H2:** lower cache/binding state is corrupt. This is now weak: dense-cache diagnostics return the correct material/style/boundary, public brick-kind values match the shader, and persistent lookup is not involved in the failing fixture.

Next isolate centre occupancy in the same full compute shader with a diagnostic output that records `ReadMaterial(...).x` and the direct result of `IsSolidSample(material)` before `SampleField`. If material=1 and `IsSolidSample` itself is false, rewrite only that proven compiler-hazard expression into explicit non-short-circuit branches and validate both diagnostics/oracles. If `IsSolidSample` is true but `SampleField` loses it, isolate the immediate Planar branch/control-flow state instead.

## Architecture / blast radius

Keep CPU voxel/storage truth authoritative. GPU code is a derived presentation backend. Fix shared GPU rendering semantics rather than VoxelShowcase policy. Unsupported inputs must be explicit eligibility results, not wrong geometry or incidental fallback. Preserve the existing world-scoped mirror and production composition unless evidence proves a boundary defect.

Production inventory confirms the current cutover policy defaults GPU **off** unless `VOXEL_ENABLE_EXPERIMENTAL_GPU_CUTOVER=1`; source steps 1/2 are the claimed GPU-supported solid rings; device/context failures increment eligible GPU fallback counters and resume CPU density extraction. GPU semantics already implement all five built-in reconstruction modes plus material-default/blend, water classification, coating/boundary, regular/faceted/negative-shell/transition/profile paths; unsupported results are classified by reconstruction/decoration semantics.

## Remaining gates

Isolate centre occupancy -> focused regression/fix -> CPU/GPU semantic suite -> explicit no-silent-fallback/recovery contract -> automatic module validation -> exact-SHA built-player VoxelShowcase plus independent-consumer evidence -> performance/memory review -> close.
