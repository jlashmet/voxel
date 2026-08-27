# Experiment 017 — ramp exact-snapshot admission

**Hypothesis.** Movement spikes are amplified because several idle solid shards begin phase-0 exact metadata setup in one renderer frame. If burst admission is causal, allowing the exposed convergence ceiling to grow by only one from the previous frame's observed running solid jobs should reduce traversal p95/p99 without reducing the configured steady-state ceiling.

**Competing hypothesis.** GPU count/write, draw submission, upload, or unrelated player-loop work dominates; smoothing new build admission will not materially change the unchanged traversal gate.

**Action / source.** Production commit `36eeeace938f801063bd0aa6d57074c3bacaf9b2` changes only `VoxelRenderPass`: before scheduler `Prepare`, `MaxConcurrentBuildsConverging` is clamped to `min(configured, previous RunningSolidJobs + 1)`. Already-active builds continue; the scheduler's existing maximum and all coverage/LOD/frame-time budgets remain unchanged.

**Falsifier.** The deterministic traversal still exceeds p95 18 ms or p99 25 ms, or opens the existing near/far coverage gap. A coverage regression also rejects the change even if frame time improves.

**Cost / blast radius.** Shared solid-render admission only. No allocations, storage/geometry semantic changes, GPU format changes, or lower steady-state concurrency. Cold/ramp convergence can expose one additional build slot per rendered frame; the moving coverage assertion measures whether that deferral is acceptable.

**Result.** Pending exact-SHA PlayMode traversal + saved-pose replay.
