# GPU renderer production restoration — implementation plan

**Target:** `Assets/VoxelEngine/Rendering` GPU surface extraction, persistent mirror, production cutover, and production consumers.  
**Starting SHA:** `b18d470f66221c7cb6091249f4683c2d994bffec`.

## Observed behavior

Exact-SHA run `33665456593` reproduced the production density divergence: 1300/2197 samples disagree for source step 1, worst CPU `+0.50000` vs GPU `-0.14000`. Diagnostics localized the first divergence to world voxel `(-2,-2,-2)`, where material and boundary match but the GPU loses the transient authoritative-solid bit.

Historical run `33677903232` passed both production density-oracle source steps on commit `5716e56a0f72fadedda54c8a5727f5dd61ca60ee`; immediate child `b4de1b576dfb06821ada42b6094bf9cbe7c9c31f` failed both after adding persistent GPU directory resolution to `VoxelBrickDensity.hlsl`. Later discriminators falsified direct UAV assignment, nested `out`, branch flattening, loop attributes, SRV/UAV aliasing, water-mask binding, coordinate/cache addressing, and giant-mesher compilation-unit size as sole causes.

Run `33682727099` proved the exact standalone `CSSampleDensity` + full `SampleField` path is correct when `VOXEL_FORCE_DENSE_LOOKUP` compiles persistent lookup out. Run `33684089590` proved `VOXEL_FORCE_PERSISTENT_LOOKUP` returns the same Planar density/material/surface/boundary as dense lookup for the same synthetic 4x4x4 world. The earlier minimal helper-reachability probe also passed. Therefore persistent directory semantics themselves are correct; the Metal defect requires the full density program and persistent-directory code to coexist in the same compilation unit.

Run `33692310999` falsified a second combined-lookup design before production activation. Merely adding a bounded prepared-table alternate path plus two extra SRVs to the shared density include made both production density oracles fail `2197/2197` with GPU density `0.0`, collapsed downstream topology/transition output, and even perturbed the forced-persistent standalone control. The failed include experiment was reverted exactly. Because two materially different alternate-lookup designs reproduced the same class of compiler corruption, the repair keeps persistent hash/probe resolution in a separate compute compilation unit.

Subsequent exact-SHA compiler probes separated that boundary: declaring the prepared SRVs but never using them passes, and a reachable runtime-disabled prepared branch also passes. Corruption therefore requires active alternate lookup semantics in the shared density path, not merely extra resources or branch reachability.

## Repair direction

Do not attempt more syntax reshaping. Preserve the world-scoped persistent GPU mirror, but isolate its coordinate hash/probe logic in a dedicated GPU resolver. That resolver produces the same packed empty/uniform/mixed brick entries as the legacy dense cache. All density, faceted, decoration, profile, and transition sampling then consumes only the resolved dense window, so `VoxelBrickDensity.hlsl` compiles without persistent-directory resolution. Do not reintroduce CPU per-chunk brick staging or readback.

Production integration is implemented in feature commit `4609079f1109cb43b5dc747926020e0e28b08222`. `GpuSurfaceExtractor.CountBatchResources` owns one reusable `GpuBrickCachePreparation`; `DispatchCountBatch` resolves all lane requests in one resolver dispatch, removes the production `_brickCache.SetData(_brickCacheStaging)` CPU upload, and binds the resulting dense entries plus request views through both count and write batch kernels. Batch kernels compile with `VOXEL_BATCH_DENSE_LOOKUP`, while standalone/editor kernels retain the legacy one-window dense path. Unused reusable request slots are invalidated so smaller later batches cannot observe stale views. `GpuProductionBrickCacheArchitectureTests` guards the no-readback/no-CPU-reconstruction/reuse/binding contract.

Exact-SHA Metal evidence now validates the production repair itself. Run `33699482967` passed `GpuSurfaceExtractorOracleTests.GpuDensityMatchesTheCpuJobSampleForSample(1)`. Run `33699824226` passed the corresponding source-step-2 oracle. Run `33700484569`, sourced from feature SHA `d9c371c78453f160fe7914c9cbfc842e132f8d93`, passed `GpuProductionPreparedBatchRuntimeTests.TwoSeparatedRequestsUseTheirOwnPreparedDenseSlicesForCountAndWrite`: two far-separated persistent-mirror requests use independent GPU-resolved dense slices through both count and write. Its persistent module summary contained only the recurring architecture failures `GeometryPipelineArchitectureTests.SolidArenaPressureIsBackpressureNotBufferGrowth` and `GpuLod2CutoverPolicyTests.ProductionGpuCutoverDefaultsOnWithExplicitDisableFallback`. TGPU-010 and TGPU-011 are therefore validated.

Feature SHA `2430e224a3dce40b0596f3d82c7ee77ef6b11ae3` added semantic regression coverage for configured water, opaque material IDs above the 32-bit water-mask range, material-default style resolution, generic material-blend presentation, and explicit rejection of unsupported reconstruction before extraction. Run `33701207515` exercised that rendering module set on Metal and failed only the same two architecture assertions; the semantic, topology, negative-shell, transition, and eligibility tests under that source passed. Current feature work additionally contains a real built-in Snow coating-displacement parity regression because the older boundary/coating oracle used a default no-op coating catalogue and therefore did not actually exercise displacement.

Run `33706289961` exposed a distinct validation blocker in the prepared-batch path: `GpuProductionPreparedBatchRuntimeTests.TwoSeparatedRequestsUseTheirOwnPreparedDenseSlicesForCountAndWrite` spent about 600.5 seconds in Unity shader compilation and then observed zero output. The artifact log identified the actual cause: Metal rejects `_BatchBrickCacheViews.GetDimensions(...)` because Metal shading language does not support runtime buffer-size queries. This was a compiler failure, not evidence of incorrect dense-slice semantics. The repair removes the unsupported query and makes prepared request views self-terminating: `GpuBrickCachePreparationBuffers` reserves one extra view, `GpuBrickCachePreparation` writes `OutputBase = -1` from the first inactive view through that reserved terminator, and `VoxelBrickDensity.hlsl` walks only until the terminator. The contract remains GPU-prepared/reused with no CPU voxel reconstruction or readback, and it is safe even when a batch uses its full logical capacity.

Exact-SHA run `33801344286`, sourced from feature SHA `f9c07f60d635f23fc4c7a97bd99d10223da417a6`, passed the automatically required Rendering module validation and the standalone SceneIssue replay. This validates TGPU-012 material classification and TGPU-013 surface-style/unsupported-reconstruction coverage. The subsequent feature commits modify only TGPU-014 regressions: real Snow displacement, presentation-only Wet coating invariance, unsupported decoration rejection, and authored boundary extrusion-axis parity across X/Y/Z.

TGPU-014 is now exact-SHA validated: CI request commit `0c4f7d1ddeb16967a8cf92fc15d1870340b820be` targets feature SHA `2521e787d2dde8624b71e55a9ede7398b0a46d5c`, and run `33803816245` completed successfully. The feature branch was then synchronized with current `master` `81ffa4bbc76c3feb6e0bde2376065b4144f3f10a` through merge commit `d277431677ce8e7f2f1bf58553be12b4d25668a1` before continuing persistent-mirror work. The existing `GpuPersistentMirrorExtractionTests` request also passed as run `33807347113` for feature SHA `78677ab8b5c24bd01c51da3ce8fe6cf468d7cb8a`, proving one repeated mixed-brick publication path; it does not yet satisfy the full TGPU-021 mixed/uniform/empty and generation contract.

## Required build-green rule

Repository-selected required validation must be green before this assignment can close. A failure in an automatically required gate is a blocking defect for this assignment while it prevents TGPU-052/closure, even when the failing assertion predates the GPU density repair. Do not waive, relabel as baseline, or route around such failures. Fix the demonstrated production contract or merge an authoritative upstream fix, then rerun the same required gate.

## Acceptance

1. GPU density/sample semantics match the real CPU jobs for every supported reconstruction path and supported source step exercised by production.
2. Regular topology, attributed geometry, negative-shell ownership, transition faces, faceted surfaces, coatings/decorations, and material semantics match CPU expectations where GPU support is claimed.
3. Persistent mirror publication, edits, eviction/recovery, generation handling, and world-coordinate lookup remain correct with no stale/wrong-brick rendering.
4. VoxelShowcase and at least one independent production consumer render GPU-eligible solid chunks through GPU extraction with zero silent eligible CPU fallbacks.
5. Built-player traversal/edit evidence is visually production-correct: no holes, cracks, stale geometry, missing surfaces, wrong materials, or fallback-hidden success.
6. Frame-path blocking, frame latency, upload/memory, and committed GPU resource cost remain within repository budgets.
7. All repository-selected required build/CI gates for the exact feature SHA are green; recurring automatic failures are fixed rather than waived.

## Architecture / blast radius

Keep CPU voxel/storage truth authoritative. GPU code is a derived presentation backend. Fix shared GPU rendering semantics rather than VoxelShowcase policy. Unsupported inputs must be explicit eligibility results, not wrong geometry or incidental fallback. Preserve the existing world-scoped mirror and production composition unless evidence proves a boundary defect.

## Remaining gates

TGPU-020 dense-vs-persistent sample equivalence -> complete TGPU-021 publication semantics -> negative-coordinate/boundary lookup -> edit/eviction/liveness/frame-path correctness -> explicit no-silent-fallback/recovery contract -> production GPU cutover in VoxelShowcase plus independent consumer -> automatic module/player validation -> exact-SHA built-player visual/edit evidence -> performance/memory review -> close.
