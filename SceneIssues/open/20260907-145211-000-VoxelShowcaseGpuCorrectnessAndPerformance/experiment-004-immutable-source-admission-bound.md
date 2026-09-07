# Experiment 004 — immutable exact-source admission bound

## Hypothesis

Experiment 003 proved that unrestricted whole-request leases starve the shared mirror, but its first repair (`0c65f67b4e1ef1518923fb24a19eb4c8170eb707`) may be unsafe if fine prepared-cache records still reference live mirror slots after a bounded preparation slice releases demand. The correct repair must remove waiting-request residency without allowing a prepared mixed source to be evicted/reused before count/write completes.

## Safety discriminator

Static production trace falsified the first repair as sufficient:

- `GpuBrickCachePreparation`/`VoxelBrickCacheResolver.compute` write packed persistent-directory entries into the prepared dense cache.
- For mixed sources the packed entry contains the live mirror slot used to index `_BrickMaterials`/mixed payload data.
- `GpuBlockHlodSummary.compute` explicitly documents that dense-entry consumption assumes admission still holds an immutable source lease.
- Therefore a fine source portion cannot become evictable after its packed entry is prepared and before the final GPU count/write submission. Dropping all whole-request coverage would trade starvation for stale-slot corruption.

No green CI was requested for that unsafe intermediate repair.

## Capacity-bound admission

Production exact-cache edges are derived from the 64-cell chunk and one-brick halo:

- step 1: `10^3 = 1,000` blocks;
- step 2: `18^3 = 5,832` blocks;
- step 4: `34^3 = 39,304` blocks.

The baseline shared mixed mirror has 40,270 payload slots. Two worst-case step-2 requests fit (11,664); a single step-4 request fits but leaves insufficient capacity for another exact/coarse footprint. Step 8 copies HLOD summaries and therefore does not require a retained whole mixed-slot footprint.

Selected repair:

1. Waiting contexts own no source admission and consume no extraction chain.
2. Admitted steps 1/2 retain their immutable whole-source leases, bounded to two owners of the same step.
3. Step 4 retains one immutable exact owner.
4. Step 8 owns one admission turn and only the existing whole-request edit watch plus bounded HLOD source slices.
5. Different source steps do not overlap. A short preferred-step handoff gives a previously blocked LOD a bounded opportunity after the current mode releases.
6. A newly acquired source lease is immediately released if GPU-chain or handle admission fails; normal release/cancellation owns final cleanup.
7. World-epoch changes clear stale gate ownership without touching the new world's coordinator maps.

This changes admission only. It does not increase mirror/page capacity, shorten distance, hide demand, lower quality or change authoritative voxel data.

## Regression

The final `GpuPersistentSourceDemandOwnershipTests` uses only internal production APIs exposed to the Rendering test assembly (no private reflection): real `GpuSurfaceExtractionContext`, real page arena and real bound voxel world. It asserts the bounded owner/demand counts for steps 1/2/4/8 and a cross-LOD release/handoff after the normal one-frame admission-poll boundary.

The earlier exact red request remains valid historical proof: `41cd5e3b07c7d5a9d8c4af87dbd1c360a7191fe4`, run `34163251595`, failed three of four pre-fix cases exactly because steps 1/2/4 each retained an unrestricted whole-request footprint; step 8 passed.

## Verdict / next step

The original H1 diagnosis remains supported, while the first edit-watch-only implementation is rejected as unsafe. Validate the corrected bounded immutable-admission implementation on one exact feature SHA. If the focused regression is green, replay Rendering-owned SolidGpu production validation and full VoxelShowcase; acceptance requires both zero persistent occupied-visible holes under existing deadlines and no stale-slot/lifetime regression.
