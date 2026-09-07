# Experiment 004 — immutable exact-source admission bound

## Hypothesis

Experiment 003 proved that unrestricted whole-request leases starve the shared mirror, but the first edit-watch-only repair (`0c65f67b4e1ef1518923fb24a19eb4c8170eb707`) is unsafe because fine prepared-cache entries retain live mixed-slot references until count/write completes. The repair therefore has to bound **admission**, not weaken immutable source lifetime.

## Safety discriminator

Static production trace falsified the first repair before green CI:

- `GpuBrickCachePreparation` / `VoxelBrickCacheResolver.compute` copy packed persistent entries into the GPU dense cache.
- Mixed entries contain live mirror slot indexes used by count/write.
- `GpuBlockHlodSummary.compute` explicitly states dense-entry consumption assumes an immutable source lease.
- Making those prepared mixed sources evictable could reuse a slot before final count/write and corrupt geometry.

## Capacity-aware admission

Production cache edges are step 1 = 10 (`1,000` worst-case mixed slots), step 2 = 18 (`5,832`), step 4 = 34 (`39,304`). The repository's 96 MB minimum shared-mirror layout has `40,270` mixed slots. Admission now reserves those worst-case counts against the **actual** mirror slot capacity plus the existing eight-chain cap. At the minimum layout this permits up to 8 step-1, 6 step-2, or 1 step-4 exact owners. Step 8 uses copied HLOD summaries, keeps one exclusive admission turn, and has no whole-footprint mixed-slot reservation.

Rules:

1. Waiting contexts own no source demand and consume no extraction chain.
2. Exact owners retain `RequestCoverage` through count/write/cancellation.
3. Different LOD modes do not overlap; once another LOD waits, the active mode drains rather than refills.
4. Cancelled/inactive waiters are pruned so they cannot reserve a phantom turn.
5. A newly acquired lease is released immediately if later GPU-chain/handle admission fails.
6. World-epoch changes reset gate state; coordinator world reset remains source-map authority.

No capacity, quality, draw-distance, content or CPU-authority budget changes are made.

## Regression

`GpuPersistentSourceDemandOwnershipTests` uses internal production APIs (no private reflection), a real bound world, real extraction contexts and real page arena. It proves the 8/6/1/exclusive-HLOD bounds, reservation <= mirror capacity, waiting requests add no demand/chain, and cross-LOD handoff after the normal frame boundary.

`GpuSourceAdmissionFairnessTests` proves continuous near arrivals cannot refill ahead of an already-waiting coarse LOD, and that a cancelled waiter does not hold a false preferred turn.

Pre-fix red evidence remains `41cd5e3b07c7d5a9d8c4af87dbd1c360a7191fe4`, run `34163251595`: steps 1/2/4 failed exactly because each retained an unrestricted whole footprint; step 8 passed.

The first corrected request, transport `730a7a344f8ac7162f9891b664734f3e58be3aad` / source `47e08b76e9e3bc331786790178160bbb12188403`, run `34165162761`, reached automatic Rendering validation but failed compile before tests with two `CS0103` errors for `VoxelReadGrid` in the new gate. This was a narrow missing namespace import, not a behavioral or infrastructure result. Current source imports `VoxelEngine.Rendering.Runtime.SurfaceExtraction`, so that exact old failure is superseded and must not be retried as acceptance.

## Verdict / next step

H1 remains supported and the unsafe edit-watch-only implementation remains rejected. Run the current capacity-aware implementation on a new exact feature SHA. Acceptance requires the Rendering assembly and owning production players first, then the full 180-second VoxelShowcase replay. The repair is accepted only if source service remains bounded, persistent occupied-visible holes converge under existing deadlines, and no stale-slot/lifetime regression appears.
