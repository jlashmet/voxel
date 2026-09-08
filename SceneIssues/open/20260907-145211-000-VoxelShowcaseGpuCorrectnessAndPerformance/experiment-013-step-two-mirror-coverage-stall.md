# Experiment 013 — step-two mirror coverage stall

## Scope

This is evidence only. It does not change renderer policy, budgets, world state, System24, or the targeted-CI transport. The exact source under observation remains `909c9893bc0d491fd3676e87e2616da32e5d2933`, validated by request `00e166f7073b5c8871a45cd100bb49e5731ca1bd` / run `34202852688` / artifact `10050933912` (`sha256:b3553904f585429255f4b3077e35076b8731118cc0bdc4258810d7b63248df68`).

The goal is to identify the first demonstrably broken transition before making another product change.

## Stationary evidence window

Late stationary `VoxelShowcase` frames remain visibly incomplete while the renderer continues to report missing frustum demand. Across the late run:

- `missingVisible` falls only from about `522` to `501`; the view does not converge.
- `gpuDemand ... unqueued=0`, so the missing GPU-classified demand has reached a durable worker queue rather than being lost before admission.
- GPU publications continue slowly (`step1` about `124 -> 130`, `step2` about `119 -> 125`, `step4` about `56 -> 59`, `step8` about `10 -> 16`), so the renderer is not globally dead.
- The shared source mirror is continuously full: `mixed=40270/40270` and `ready=40270`.
- `noSlot` rises from about `111359` to `138954` during the same late window, despite no GPU page-arena allocation failures.
- Twelve worker GPU stages remain in flight. The oldest request remains the same source footprint for more than nineteen sampled seconds while its age increases from about `144000 ms` to `163040 ms`:
  `step=2 origin=int3(47, 31, 31) edge=18 source=GPU restarts=0`.

The dominant pressure is therefore upstream of geometry-page allocation: the page arena reports `allocFail=0`, while the source mirror is saturated and source recovery repeatedly reports `NoSlot`.

## Portable world-space reconstruction of the oldest stalled footprint

`ShowcaseWorld.VoxelSize` is exactly `0.1 m`. The castle landmark is centred at voxel `x=256`, `z=376`, i.e. world `x=25.6 m`, `z=37.6 m`.

`GpuSolidChunkCache.TryStageGpuBuild` constructs a step-2 request from a 64-cell chunk as follows:

- `VoxelsPerAxis = 64 * SourceStep = 128` voxels;
- `chunkOriginVoxel = chunkCoordinate * 128`;
- `BrickCacheOrigin = (chunkOriginVoxel >> 3) - 1`.

Inverting the observed `BrickCacheOrigin=int3(47,31,31)` gives:

- `chunkOriginVoxel = (int3(47,31,31) + 1) * 8 = int3(384,256,256)`;
- exact step-2 chunk coordinate `int3(3,2,2)`;
- owned core voxels `[384,512) x [256,384) x [256,384)`;
- owned core world bounds `x=[38.4,51.2) m`, `y=[25.6,38.4) m`, `z=[25.6,38.4) m`;
- renderer draw bounds include the one-sample step halo, approximately `x=[38.2,51.4] m`, `y=[25.4,38.6] m`, `z=[25.4,38.6] m`.

Relative to the landmark centre this footprint covers the castle's right-hand side and reaches the castle depth. Using the stationary camera recorded in experiment 012 (`position=(25.6,30.0,16.6)`, rotation approximately +15 degrees about camera X, vertical FOV 70, 1600x900), the projected box intersects roughly the rightmost `x=1160..1600` pixels and `y=0..594` pixels; several corners are offscreen. This calculated envelope overlaps the visibly broken right-hand castle / adjacent terrain region in the accepted stationary rejection frame. It is a portable coordinate correlation, not a claim that every pixel inside the envelope belongs to this one chunk.

## Causal trace through the existing production path

1. **GPU presentation classification reaches host demand.** `GpuSurfaceDrawDispatcher` reads back the bounded LOD-state image and maps each record to its stable `SurfaceLodNodeKey`. `VoxelSurfaceScheduler.ApplyGpuDemandFeedback` routes that exact `(SourceStep, Coordinate)` to the owning shard. Late evidence reports `unqueued=0`, so this transition is working for the aggregate missing set.
2. **Missing in-frustum demand is retained.** `GpuSolidChunkCache.ApplyGpuDemand` marks a pending in-band/frustum coordinate dirty and promotes it to the visible FIFO when there is no current ready or known-empty generation. The late `missingVisible~500` result therefore means many GPU-classified frustum coordinates still lack a current drawable publication.
3. **The oldest step-2 build has entered the GPU source path but its immutable footprint does not converge.** `GpuSurfaceExtractionContext` requests source coverage for the exact 18-cubed brick-cache footprint. The unchanged oldest coverage identity for >160 seconds proves this request is not advancing to terminal publication.
4. **The first observed broken transition is source-mirror coverage acquisition.** During the stall the mirror is pinned at `40270/40270` mixed slots and recovery's `NoSlot` counter climbs by tens of thousands. `GpuSurfaceSourceAdmission` bounds new step-2 immutable owners (18^3 slots each) and prevents unbounded owner admission, but admitted source footprints still depend on `GpuSurfaceMirrorCoordinator` making the requested blocks resident. Recovery is making some global progress, but not enough to complete the oldest footprint under the existing settle budget.
5. **Downstream geometry cannot close the hole until that transition completes.** Count/write/page publication happens only after the source request can advance. The page arena itself shows no allocation failure in this window, and aggregate publications continue for other chunks. A coordinate whose current generation has not published remains `missingVisible`, so the source-coverage stall is sufficient to explain persistent gaps for affected demanded chunks.

## What this proves and what it does not

Proven:

- the stationary visual defect is persistent, not startup-only;
- GPU demand is not being dropped before the worker queues;
- the shared source mirror is capacity-saturated during the defect;
- the first long-lived stalled production stage observed for a castle-overlapping step-2 footprint is source coverage / mirror residency, before final geometry publication;
- page-arena allocation is not the first failure in this captured window.

Not yet proven:

- that the specific oldest `int3(3,2,2)` chunk carries the `missing-frustum` accounting bit in the same feedback sample; the existing aggregate log does not print that per-node bit;
- which canonical cell/material inside that footprint is the first visibly missing surface sample;
- whether the saturation is caused by active-footprint retention, insufficient bounded inactive-slot eviction, or another mirror-lifetime defect;
- that correcting mirror coverage alone closes every captured castle/terrain/vegetation gap.

## Next discriminator before any repair

Use the existing bounded GPU LOD-demand feedback — do not add an authoritative world readback — to preserve one exact missing-frustum `(step, chunk)` identity and its world bounds. For that one candidate, record only presentation/control state already owned by the renderer: desired/source generation, dirty/visible queue age, owning shard, active-build match, admission/coverage phase, source footprint, page handle/publication generation, and selected-draw state. Then correlate that exact candidate to the screenshot and, only if needed, add a separately bounded GPU diagnostic query for one canonical mirrored cell/material classification. No renderer policy or budget should change until that trace identifies the first failing transition for a confirmed visible gap.