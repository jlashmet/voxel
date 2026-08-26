# Experiment 01 — trace solid LOD visibility ownership

## Question

Can two resident solid LOD workers publish overlapping world coverage into `_solidVisible` during the intentional ring overlap?

## Exact change

No runtime change. Static trace of `VoxelSurfaceScheduler.BuildVisibleDrawLists` and `CpuTransvoxelChunkCache.CollectVisibleCulled` on `fixes/agent-4`.

## Reproduction

1. Use a camera position near an LOD ring transition.
2. The scheduler configures adjacent solid rings with a one-chunk-cell overlap tolerance.
3. Both workers independently enumerate renderable resident chunks inside their own view-distance bands.
4. Both append accepted chunks into the same `_solidVisible` list without hierarchy ownership filtering.

## Result

Confirmed. Ring overlap can yield both a finer chunk and its coarser covering chunk in `_solidVisible` simultaneously; residency readiness is the only per-worker gate.

## Interpretation

The overlap is needed for robust streaming/convergence, but final solid draw publication lacks exclusive LOD coverage. The fix should preserve overlap for building/residency and make render ownership exclusive at publication/handoff.

## Disposition

Keep the diagnostic conclusion; no production change in this experiment.

## Evidence

- `VoxelSurfaceScheduler.BuildVisibleDrawLists`
- `CpuTransvoxelChunkCache.CollectVisibleCulled`
- plan commit `34be99c1dfc7df23a55b50ce22e5d13901bb159f`
