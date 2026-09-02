# GPU renderer production restoration — implementation plan

**Target:** `Assets/VoxelEngine/Rendering` GPU surface extraction, persistent mirror, production cutover, and production consumers.  
**Starting SHA:** `b18d470f66221c7cb6091249f4683c2d994bffec`.

## Observed behavior

Exact-SHA run `33665456593` reproduced the production density divergence: 1300/2197 samples disagree for source step 1, worst CPU `+0.50000` vs GPU `-0.14000`. Diagnostics localized the first divergence to world voxel `(-2,-2,-2)`, where material and boundary match but the GPU loses the transient authoritative-solid bit.

Historical run `33677903232` passed both production density-oracle source steps on commit `5716e56a0f72fadedda54c8a5727f5dd61ca60ee`; immediate child `b4de1b576dfb06821ada42b6094bf9cbe7c9c31f` failed both after adding persistent GPU directory resolution to `VoxelBrickDensity.hlsl`. Later discriminators falsified direct UAV assignment, nested `out`, branch flattening, loop attributes, SRV/UAV aliasing, water-mask binding, coordinate/cache addressing, and giant-mesher compilation-unit size as sole causes.

Run `33682727099` proved the exact standalone `CSSampleDensity` + full `SampleField` path is correct when `VOXEL_FORCE_DENSE_LOOKUP` compiles persistent lookup out. Run `33684089590` proved `VOXEL_FORCE_PERSISTENT_LOOKUP` returns the same Planar density/material/surface/boundary as dense lookup for the same synthetic 4x4x4 world. The earlier minimal helper-reachability probe also passed. Therefore persistent directory semantics themselves are correct; the Metal defect requires the full density program and persistent-directory code to coexist in the same compilation unit.

Run `33692310999` falsified a second combined-lookup design before production activation. Merely adding a bounded prepared-table alternate path plus two extra SRVs to the shared density include made both production density oracles fail `2197/2197` with GPU density `0.0`, collapsed downstream topology/transition output, and even perturbed the forced-persistent standalone control. The failed include experiment was reverted exactly. Because two materially different alternate-lookup designs now reproduce the same class of compiler corruption, production integration is blocked until the test-only `GpuPreparedLookupCompilerProbeTests` separates extra-resource declaration/layout from reachable alternate-resource branching on real Metal.

## Repair direction

Do not attempt more syntax reshaping. Preserve the world-scoped persistent GPU mirror, but isolate its coordinate hash/probe logic in a dedicated GPU resolver. That resolver produces the same packed empty/uniform/mixed brick entries as the legacy dense cache. All density, faceted, decoration, profile, and transition sampling then consumes only the resolved dense window, so `VoxelBrickDensity.hlsl` can compile without persistent-directory resolution. This must not reintroduce CPU per-chunk brick staging or readback.

The first production slice is `VoxelBrickCacheResolver.compute`, an isolated resolver containing only persistent directory lookup and dense-entry publication. Its regression covers negative coordinates plus empty/uniform/mixed packed entries. After that kernel is proven, integrate it into the live batched extraction path while preserving batching/backpressure and then rerun the real production density oracle for source steps 1 and 2.

## Acceptance

1. GPU density/sample semantics match the real CPU jobs for every supported reconstruction path and supported source step exercised by production.
2. Regular topology, attributed geometry, negative-shell ownership, transition faces, faceted surfaces, coatings/decorations, and material semantics match CPU expectations where GPU support is claimed.
3. Persistent mirror publication, edits, eviction/recovery, generation handling, and world-coordinate lookup remain correct with no stale/wrong-brick rendering.
4. VoxelShowcase and at least one independent production consumer render GPU-eligible solid chunks through GPU extraction with zero silent eligible CPU fallbacks.
5. Built-player traversal/edit evidence is visually production-correct: no holes, cracks, stale geometry, missing surfaces, wrong materials, or fallback-hidden success.
6. Frame-path blocking, frame latency, upload/memory, and committed GPU resource cost remain within repository budgets.

## Architecture / blast radius

Keep CPU voxel/storage truth authoritative. GPU code is a derived presentation backend. Fix shared GPU rendering semantics rather than VoxelShowcase policy. Unsupported inputs must be explicit eligibility results, not wrong geometry or incidental fallback. Preserve the existing world-scoped mirror and production composition unless evidence proves a boundary defect.

## Remaining gates

Prepared-resource compiler boundary probes -> production dense-window integration -> production density parity step 1/2 -> CPU/GPU semantic/topology suite -> persistent mirror correctness -> explicit no-silent-fallback/recovery contract -> automatic module validation -> exact-SHA built-player VoxelShowcase plus independent-consumer evidence -> performance/memory review -> close.
