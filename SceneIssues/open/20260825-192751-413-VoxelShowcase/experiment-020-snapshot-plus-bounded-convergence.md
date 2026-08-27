# Experiment 020 — bounded convergence after snapshot fix

## Question
Does reducing visible-convergence build concurrency from 12 to 8 close the remaining traversal tail without reopening visible holes?

## Change
`VoxelRenderBridge.SurfaceMaxConcurrentBuildsConverging` changed 12→8. The near-ring metadata helper remained enabled; all acceptance thresholds were unchanged.

## Exact result — rejected
Targeted request `agent-2-192751-final-snapshot-bounded-20260827-1535`, transport `13530d725a008217a29f5e69f6fefd593fb74467`, run `33123159073`, job `98694823144`, failed the behavioral regression because **visible voxel draw dropped to zero on traversal frame 5**. The required 45 s replay later converged to roughly 400–452 FPS with zero missing geometry, but late recovery does not satisfy moving coverage.

The 8-build cap is therefore falsified and restored to 12 in `0c3d0f01faa60cf47d5e85e87589d6956182cd5c`.

## Attribution correction
Audit after the run found the long-lived feature branch still differed from production in `CpuTransvoxelChunkCache.GpuCutoverDisabled`: production hard-disables GPU extraction, while the tested branch re-enabled it unless `VOXEL_DISABLE_GPU_CUTOVER=1`. Therefore this run cannot attribute timing to the snapshot helper alone. `d1b458b2d796a84a9ba7cd91e7586c0040fff229` restores `CpuTransvoxelChunkCache.cs` byte-for-byte from current master while retaining the separate near-ring metadata scheduling helper.

## Blast radius / cost
The rejected cap affected extraction admission only and is gone. The next candidate changes only scheduling of the bounded step-1/2 exact metadata jobs; Storage authority, gameplay/collision, geometry/material/topology semantics, LOD layout, visibility, upload/discovery budgets, arena capacity, and regression thresholds remain unchanged.
