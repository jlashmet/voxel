# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Observed behavior / acceptance
- Exact `db1230b...` built-player evidence starts near 160–236 FPS, then solid admission grows to 636–770 ms/frame with 757 visible chunks missing and only four drawn. The exact targeted test did not run because another Unity editor occupied the runner; the earlier `5338252...` product run completed zero GPU chunks.
- Preserve deterministic CPU world truth, collision, replication, and residency. Presentation may lag behind one immutable GPU generation while old/far geometry covers it.
- A passing result needs sustained step-1/step-2 GPU completion, zero silent eligible fallback, no visible holes, no frame-path completion, moving p95 <18 ms/p99 <25 ms, stationary p95 <8 ms, and visual inspection of the built-player capture.

## Hypotheses / next discriminator
- H1: item-count recovery is not a time bound; 2,048 synchronous CPU block pins/stages plus upload flushes cause the measured 700+ ms admission frames. Discriminator: restructure recovery so normal extraction never scans resident voxel payloads on the frame path.
- H2: full CPU exact-snapshot pinning/classification remains the dominant steady-state cost even after mirror recovery. Discriminator: admit GPU candidates from a versioned mirror and classify empty/unsupported semantics in compute without building per-chunk CPU payload arrays.

## Selected design
- Storage remains authoritative and publishes compact versioned brick/residency changes to a world-scoped GPU mirror outside chunk admission. Mirror slots are immutable for an epoch and reclaimed only after GPU fences/readbacks make the epoch safe; no global “zero active extractions” gate.
- GPU classification consumes the same mirror generation as density/Transvoxel extraction. It returns empty/supported/unsupported before allocation. Supported geometry remains GPU-resident through count/reserve/write/indirect draw; unsupported semantics use the existing CPU path.
- Eliminate dense per-chunk CPU brick walks and retained voxel pins for GPU-candidate chunks. Keep renderer-local dirty versions separate from authoritative storage generations and reject stale publication in both domains.
- Bound all mirror maintenance by elapsed time and upload bytes from the device matrix, not block count. Account for GPU buffers and host staging separately.

## Remaining gates
- Focused fake-storage tests for generation races, recovery/update interleaving, eviction, lifecycle, and exhaustion; compute parity for classification and every supported semantic.
- Exact-SHA targeted CI and built-player replay at the recorded pose; inspect screenshots and logs before promotion.
