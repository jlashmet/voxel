# GPU renderer production restoration — tasks

**Plan:** [plan.md](plan.md)  
**Owning area:** `Assets/VoxelEngine/Rendering`  
**Execution rule:** restore one trustworthy production GPU backend; do not hide GPU defects behind scene policy, broad CPU fallback, weakened parity assertions, or test-only rendering paths.

## Establish the failure and ownership boundary

- [x] **TGPU-001 — Rebase the investigation on current master.** Starting SHA `b18d470f66221c7cb6091249f4683c2d994bffec`; exact-SHA run `33665456593` reproduced `GpuDensityMatchesTheCpuJobSampleForSample(1)` with 1300/2197 mismatches, worst sample 0 CPU 0.50000 vs GPU -0.14000.
- [x] **TGPU-002 — Inventory production GPU selection and fallback.** `VoxelSurfaceScheduler` creates sharded `CpuTransvoxelChunkCache` workers for each ring. GPU support is claimed only for exact source steps 1/2, non-mip workers with a slot grid and enabled cutover. `GpuSurfaceExtractionContext` is created lazily from the shared `GpuSurfaceMirrorCoordinator`; successful builds publish a GPU page handle through the normal `Entry.PublishGpuPaged` draw authority and increment `GpuCompletedBuildCount`. Current policy defaults cutover off unless `VOXEL_ENABLE_EXPERIMENTAL_GPU_CUTOVER=1`. Two demonstrated eligible-work failure paths (context creation failure and stage/context failure) set `GpuCpuSnapshotRequired`, increment `GpuContextFailureBuildCount` + `GpuFallbackBuildCount`, then resume CPU density extraction. Scheduler metrics expose GPU completed/fallback/unsupported/context/arena/count/write counters, so later cutover work can enforce zero silent eligible fallback.
- [x] **TGPU-003 — Inventory supported semantic surface features.** Claimed GPU near-ring contract is source step 1/2 and implements shared `Smooth`, `Planar`, `Rounded`, `Sharp`, and `Cubic` reconstruction IDs; material-default resolution; generic material-blend marker semantics; presentation-mask water classification including IDs >=32; coating displacement (suppressed for material blends); authored boundary Q3/extrusion-axis behavior; regular Transvoxel cells; faceted faces; negative-shell ownership; transition-face sampling/geometry; and profile/decorative suppression. Count results carry explicit unsupported-mask categories for reconstruction and decoration; later eligibility must use those semantic categories rather than scene names or material IDs.
- [x] **TGPU-004 — Preserve the current minimal density repro.** `GpuDensityDiagnosticOracleTests.FirstDensityDivergenceReportsWorldSampleAndSemanticValues` uses the production GPU extractor and real `TransvoxelDensityJob` oracle. Exact-SHA run `33666261165` reported first divergence at world `(-2,-2,-2)`: density CPU 0.50000/GPU -0.14000, material 1/1, surface `0x04000001`/`0x00000001`, boundary 0/0.
- [x] **TGPU-005 — Discriminate centre-read vs smooth-field failure.** `GpuDensityPathDiscriminationTests` ran the same full mesher/cache binding under Planar and Smooth material defaults. Exact-SHA run `33666796147` showed both identically wrong at world `(-2,-2,-2)`: CPU +0.50 vs GPU -0.14 with correct material/style but missing authoritative-solid bit. Because Planar should early-return on `centreSolid` before `AddTap`, weighted smooth taps are falsified as the sole cause; the defect is in/above centre occupancy. Public `VoxelReadBlockKind` encoding is `Empty=0, Uniform=1, Mixed=2`, matching the shader's wire convention; `PackBrickCacheEntry` writes the kind directly.
- [x] **TGPU-006 — Stop speculative fixes after repeated failure.** Two materially different interventions reproduced the identical Planar/Smooth parity symptom: run `33667605313` synchronized the CPU/GPU water-mask contract and still failed, and run `33668767375` additionally bound `_SolidWaterMaterialMask=0` directly on the production `ComputeShader` instance yet still returned GPU `-0.14` with material/style correct and the authoritative-solid bit absent. A minimal include-only Metal probe (`GpuSolidClassificationProbeTests.MaterialOneIsSolidWhenWaterMaskIsZero`) passed on retry of run `33668009978`, proving `IsSolidSample(1)` and the uniform are correct outside the full mesher. No further production fix is allowed until the full `VoxelBrickMesher` path directly isolates `ReadMaterial -> IsSolidSample -> SampleField` in one dispatch.

## Restore CPU/GPU semantic parity

- [ ] **TGPU-010 — Fix the proven density/sample root cause.** Make GPU centre occupancy, density, dominant material, surface semantics, boundary, coating displacement, and transient authoritative-solid state match `TransvoxelDensityJob` for supported inputs.
- [ ] **TGPU-011 — Prove source-step parity.** Run density/sample parity for every production-supported source step represented by current renderer tiers, including at least step 1 and step 2.
- [ ] **TGPU-012 — Prove material classification parity.** Cover opaque solids, configured water/non-solid materials, material IDs >= 32, material-default styles, and generic material-blend presentation without hard-coded scene material IDs.
- [ ] **TGPU-013 — Prove surface-style parity.** Cover Smooth, Rounded, Planar, Sharp, Cubic, and repository-supported authored styles/join behavior; unsupported reconstruction must be rejected explicitly before extraction.
- [ ] **TGPU-014 — Prove boundary/coating parity.** Cover authored boundary samples, extrusion-axis semantics, coating displacement, and decoration eligibility without changing geometry for presentation-only metadata that should not move it.
- [ ] **TGPU-015 — Restore regular topology parity.** GPU regular-cell case selection, interpolation, winding, counts, and attributed vertices must match CPU oracle geometry for supported continuous surfaces.
- [ ] **TGPU-016 — Restore faceted topology parity.** Planar/Sharp/Cubic exposed-face ownership and attributes must match authoritative occupancy and CPU expectations.
- [ ] **TGPU-017 — Restore negative-shell ownership parity.** Minimum-face cells outside the core chunk must emit exactly the CPU-owned crossing geometry without holes or duplicates.
- [ ] **TGPU-018 — Restore transition-face parity.** LOD transition density/material/surface sampling and transition geometry must stitch supported ring boundaries without cracks or duplicate faces.

## Persistent GPU mirror correctness

- [ ] **TGPU-020 — Verify dense test cache and persistent directory agree.** The same world bricks sampled through explicit dense-cache mode and production persistent lookup must produce identical material/surface/boundary inputs.
- [ ] **TGPU-021 — Verify publication semantics.** Mixed, uniform, and empty brick deltas; slot metadata; payload staging; directory entries; and generation handling must not expose stale or wrong-brick data.
- [ ] **TGPU-022 — Verify negative-coordinate and boundary lookup.** Exercise world bricks around zero, negative coordinates, directory collisions, cache/region boundaries, and padded density taps.
- [ ] **TGPU-023 — Verify edit propagation.** Runtime voxel/material/surface/boundary edits must invalidate/rebuild the affected GPU geometry and visibly converge without stale geometry.
- [ ] **TGPU-024 — Verify eviction/recovery/liveness.** Slot pressure, coverage recovery, generation advancement, and re-admission must converge without permanent holes, deadlock, or silent CPU takeover.
- [ ] **TGPU-025 — Verify no frame-path blocking.** GPU extraction/count/write/copy/publication remains asynchronous on the production frame path except explicitly permitted bounded bookkeeping.

## Production cutover and reuse

- [ ] **TGPU-030 — Make GPU eligibility semantic and explicit.** Eligibility must describe implemented reconstruction requirements, not named scenes, magic material IDs, or incidental data layout.
- [ ] **TGPU-031 — Eliminate silent eligible CPU fallback.** Once a solid chunk is classified GPU-eligible, failure/retry/backpressure is observable and bounded; it must not quietly render via CPU and make acceptance look green.
- [ ] **TGPU-032 — Preserve explicit fallback only for unsupported work.** CPU rendering may remain for declared unsupported geometry/features/devices, with metrics/tests proving why it was ineligible.
- [ ] **TGPU-033 — Prove VoxelShowcase production cutover.** `Assets/Scenes/VoxelShowcase.unity` must complete representative streaming/traversal with GPU builds, visible coverage, zero eligible fallback, and no frame-path blocking violations.
- [ ] **TGPU-034 — Prove an independent production consumer.** Exercise the same GPU renderer in at least one non-VoxelShowcase production scene/fixture through normal composition; no duplicate renderer or scene-specific enabling code.
- [ ] **TGPU-035 — Verify renderer restart/lifecycle.** Recreate/disable/enable the production rendering context without leaked buffers, stale static mirror state, duplicate ownership, or permanently disabled cutover.

## Built-player visual and performance acceptance

- [ ] **TGPU-040 — Add/maintain production player validation.** Use repository-convention validation/scenario coverage that invokes the real renderer, storage, materials, terrain, lighting, and production camera path; do not build a parallel visual fixture.
- [ ] **TGPU-041 — Capture exact-SHA VoxelShowcase traversal evidence.** Inspect built-player captures during/after movement and streaming for holes, cracks, stale chunks, popping caused by missing coverage, wrong materials, malformed faceted surfaces, and LOD seams.
- [ ] **TGPU-042 — Capture edit evidence.** In built player, perform representative voxel/surface edits and verify old geometry is replaced correctly and GPU-rendered output converges without stale remnants.
- [ ] **TGPU-043 — Verify visual success is actually GPU-rendered.** Correlate captures with renderer metrics proving visible GPU-eligible chunks completed on GPU and were not hidden by CPU fallback.
- [ ] **TGPU-044 — Check moving-frame performance.** Preserve the repository's VoxelShowcase moving p95/p99 gates or stricter current budgets; do not relax them to pass.
- [ ] **TGPU-045 — Check settled performance.** Preserve the repository's stationary frame-time gate and demonstrate no continuing pathological rebuild/readback churn once settled.
- [ ] **TGPU-046 — Check memory/upload cost.** Measure mirror, density/sample buffers, geometry arena/pages, directory, and upload traffic against authoritative device/repository budgets; no unbounded growth or per-chunk duplicate world mirrors.

## Regression, cleanup, and close

- [ ] **TGPU-050 — Run rendering module EditMode regressions.** CPU/GPU density, semantic, topology, negative-shell, transition, mirror, lifecycle, and resource tests pass on the exact feature SHA.
- [ ] **TGPU-051 — Run rendering module PlayMode regressions.** Production cutover/recovery/streaming tests pass on the exact feature SHA.
- [ ] **TGPU-052 — Run repository-selected automatic module validation.** Do not replace or weaken automatically discovered module/player gates.
- [ ] **TGPU-053 — Run required top-level built-player integration.** Required canonical player validation passes with durable artifacts on the exact feature SHA.
- [ ] **TGPU-054 — Audit diagnostic/test-only code.** Remove temporary probes that are no longer useful; retain only focused regressions that protect proven invariants. No test-only production behavior switches.
- [ ] **TGPU-055 — Audit fallback and duplicated renderer paths.** Search production code for obsolete GPU-disable switches, stale experimental branches, duplicate surface realization, and fallback paths that violate the explicit eligibility contract; remove only demonstrated obsolete paths.
- [ ] **TGPU-056 — Review final diff and blast radius.** Confirm CPU authoritative storage/collision/world truth is unchanged except where a proven shared semantic defect requires correction.
- [ ] **TGPU-057 — Close with exact evidence.** Populate `resolutionSummary`, `regressionTest`, and `fixCommit`; record exact-SHA CI/player artifacts, GPU no-fallback proof, visual classification, and performance/memory results before moving the SceneIssue to `closed/`.
