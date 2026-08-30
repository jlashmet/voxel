# Experiment 002 — exact block demand recovery

## Question
Why did the first demand-driven mirror fix remove the 700 ms admission stall yet leave every GPU worker pending and the built scene full of holes?

## Competing explanations
1. GPU dispatch/readback remained broken.
2. Snapshot/history rejection kept all requests stale.
3. Region-granular recovery was still too coarse: a demanded chunk had to wait for its entire containing Storage region to be mirrored.

## Runtime discriminator
Exact request `f7258c53…`, run `33231204833`, failed the production 210 m traversal at frame 134 with `gpuCompleted=0`, `gpuFallback=0`, `gpuWaitSlices=2118`, and zero visible voxel draws. The exact built-player replay stayed near `5 visible / 768 missing` chunks for 45 s, while solid admission was only ~`0.13–0.33 ms` and the player otherwise ran around 300 FPS.

Storage constants make hypothesis 3 predictive: one region is 512 voxels/axis, or 64 logical 8³ blocks/axis = 262,144 blocks. The 64-block/frame recovery cap therefore needs 4,096 frames before a region becomes ready (~13.7 s at 300 FPS) even if a waiting chunk needs only a small block footprint.

## Change
Keep the 64-block/frame budget, but queue/readiness-track exact world block coordinates from each GPU brick-cache footprint. Borrow `RegionReadView` for empty/uniform classification and pin only mixed payload blocks. Keep region-level last-solid-change generations for conservative snapshot rejection. On region changes, invalidate/requeue only demanded ready blocks rather than scanning 262k coordinates.

## Result
Exact request `87671c08…` (feature `c3d06ab0…`), run `33232803150`, proved the block-granular change removed the coarse-recovery stall but exposed a second liveness defect. Editor traversal advanced to `gpuCompleted=3` before failing at frame 119 with zero visible draws, `gpuFallback=0`, and `gpuWaitSlices=1611`. The built player improved from ~1.4 FPS to ~194–200 FPS with solid admission settling near ~2–4 ms, yet exact-scene coverage plateaued at `27 drawn / 743 missing` from roughly t28 through t44. All three replay captures plus `verification-final.png` show the same missing near/mid voxel surfaces while distant terrain/vegetation remain visible.

The result rejects raw block-throughput as the sole remaining cause: a finite exact footprint should continue converging, but both telemetry and captures become permanently flat. Experiment 003 therefore targets stale live-Storage admission generations retained by pending GPU stages.

## Cost
CPU recovery remains bounded at 64 publications/frame; shared GPU mirror remains >=96 MiB. Added CPU memory is only queue/hash bookkeeping for demanded blocks, with static scratch storage and no per-frame managed allocation.

Acceptance remains the existing production behavioral regression and exact built-player scene replay; no thresholds or fallback rules are weakened.
