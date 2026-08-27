# Experiment 020 — bounded convergence after snapshot fix

## Question
Does reducing visible-convergence build concurrency from 12 to 8 close the remaining traversal tail *after* the proven near-ring snapshot scheduling reduction, without reopening visible holes or slowing convergence enough to violate the same behavioral regression?

## Evidence selecting this discriminator
Per-frame profiling before the snapshot fix showed worker admission/prepare and exact snapshot dominating spikes; a diagnostic 12→8 cap reduced snapshot/worker pressure by roughly 27%, establishing concurrency as an amplifier. That older cap-only configuration still failed, so 8 is not accepted on its own.

Experiment 018 subsequently reduced exact near-ring snapshot p95 from roughly 4.5 ms to ~1.76 ms while keeping coarse snapshot work asynchronous, but snapshot-only exact traversal remained ~20.16 ms p95 / ~28.33 ms p99. The untried combination is therefore: keep that measured snapshot reduction and reduce simultaneous visible-convergence build pressure. Admission ramps are explicitly excluded because exact runs either lost all visible geometry by frame 5 or still failed at 20.73/25.10 with worse replay FPS.

## Change
`VoxelRenderBridge.SurfaceMaxConcurrentBuildsConverging` changes from 12 to 8. `SurfaceMaxConcurrentBuildsConverged` remains 1; build/upload/discovery budgets, LOD layout, geometry semantics, scheduler admission order, publication, visibility, and regression thresholds are unchanged.

The failed bounded moving-visibility reuse and the unsupported same-frame guard are removed; `VoxelSurfaceScheduler.cs` and `VoxelRenderPass.cs` match current `origin/master`. The only retained extraction source change is the bounded near-ring exact snapshot helper from experiment 018.

## Falsifier
Reject if exact-SHA `ContinuousPlayerTraversalNeverStuttersOrOpensNearFarGap` exceeds p95 18 ms or p99 25 ms, loses visible solids, opens the near/far fallback gap beyond 5 cm, reports a frame-path blocking completion, fails to stream across the traversal, or the 45 s saved-pose player replay shows missing/stale geometry or materially worse frame rate.

## Blast radius / cost
Rendering/extraction scheduling only. At most eight CPU surface builds may be in flight while visible geometry is missing, reducing job-pool pressure at the cost of up to one-third less cold-view extraction parallelism. The behavioral regression directly measures both sides of that trade: sustained movement must stay within frame budget *and* preserve visible/fallback coverage while streaming. No Storage, gameplay, collision, authoritative voxel state, GPU format, world generation, material/topology rules, or arena capacity changes.

## Result
Pending one final exact-SHA targeted CI request and 45 s saved-pose replay.
