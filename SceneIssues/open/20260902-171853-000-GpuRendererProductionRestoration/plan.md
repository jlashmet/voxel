# GPU renderer production restoration — implementation plan

**Target:** `Assets/VoxelEngine/Rendering` GPU surface extraction, persistent mirror, production cutover, and production consumers.  
**Baseline:** SceneIssue created from master `c20f19dba999503a3214c5e7d4b0f64ffdeb0062`; implementation must fetch current `origin/master` first.

## Observed behavior

The GPU backend exists and production migration tests expect supported near-ring solid chunks to use it without silent CPU fallback. Current diagnostic work on `enable_gpu` shows CPU/GPU density divergence for a uniform-neighbourhood oracle: material identity can remain correct while authoritative centre occupancy diverges. A standalone shader using the same raw `ReadMaterial`/`IsSolidSample` logic passes, so the known symptom is not yet proven to be cache packing or coordinate lookup.

## Acceptance

1. GPU density/sample semantics match the real CPU jobs for every supported reconstruction path and supported source step exercised by production.
2. Regular topology, attributed geometry, negative-shell ownership, transition faces, faceted surfaces, coatings/decorations, and material semantics match CPU expectations where GPU support is claimed.
3. Persistent mirror publication, edits, eviction/recovery, generation handling, and world-coordinate lookup remain correct with no stale/wrong-brick rendering.
4. VoxelShowcase and at least one independent production consumer render GPU-eligible solid chunks through GPU extraction with zero silent eligible CPU fallbacks.
5. Built-player traversal/edit evidence is visually production-correct: no holes, cracks, stale geometry, missing surfaces, wrong materials, or fallback-hidden success.
6. Frame-path blocking, frame latency, upload/memory, and committed GPU resource cost remain within repository budgets.

## Hypotheses / next experiment

- **H1:** full-mesher compilation/execution corrupts `SampleField` centre occupancy or smooth-tap state even though isolated raw material reads are correct.
- **H2:** shared production state/buffer binding or persistent-lookup mode contaminates otherwise-correct sampling.

First reproduce on current master with the smallest CPU-vs-GPU sample oracle, then compare (a) isolated raw read, (b) planar early-return `SampleField`, and (c) smooth full `SampleField` in the same full mesher/bindings. This discriminates lookup/binding from smooth-field logic/codegen before changing production behavior.

## Architecture / blast radius

Keep CPU voxel/storage truth authoritative. GPU code is a derived presentation backend. Fix shared GPU rendering semantics rather than VoxelShowcase policy. Unsupported inputs must be explicit eligibility results, not wrong geometry or incidental fallback. Preserve the existing world-scoped mirror and production composition unless evidence proves a boundary defect.

## Remaining gates

Root cause -> focused regression -> CPU/GPU semantic suite -> production no-fallback/recovery tests -> automatic module validation -> exact-SHA built-player VoxelShowcase plus independent-consumer evidence -> performance/memory review -> close.
