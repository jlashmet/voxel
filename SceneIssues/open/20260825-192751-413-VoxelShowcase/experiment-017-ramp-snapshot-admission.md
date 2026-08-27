# Experiment 017 — ramp exact-snapshot admission

**Hypothesis.** Movement spikes are amplified because several idle solid shards begin phase-0 exact metadata setup in one renderer frame. If burst admission is causal, exposing new convergence work gradually should reduce traversal p95/p99 without changing the configured steady-state ceiling.

**Competing hypothesis.** GPU count/write, draw submission, upload, or unrelated player-loop work dominates; smoothing new build admission will not materially change the unchanged traversal gate.

**First action.** Production commit `36eeeace938f801063bd0aa6d57074c3bacaf9b2` clamped the exposed convergence ceiling to `min(configured, previous RunningSolidJobs + 1)` before scheduler `Prepare`.

**Exact-SHA result.** Targeted request on source `a15d241c32f4bbbf8809178df516c1582c5fc2dd`, workflow run `33094983575`, job `98597415661`, failed the unchanged behavioral regression at movement frame 5: `VisibleSolidChunks == 0`. The run was terminally failed, not queued. Its saved-pose replay later settled around roughly 238–400 FPS, so the performance work did not justify the presentation regression.

**Discriminator.** `RunningSolidJobs` is not stable ramp state: short jobs can finish before the next rendered frame and collapse the next ceiling back toward one. That explains how snapshot fanout can be reduced while near-field publication is starved.

**Revised action.** Commit `203788c90ee0ab82d9fed5a1d9dfb317c0d039d8` keeps an explicit per-render-pass convergence ceiling. It starts at two slots for presentation continuity and grows by one per rendered frame while the previous scheduler metrics report missing visible solids. It resets across world teardown. Already-active builds and the configured steady-state maximum remain unchanged.

**Falsifier.** The deterministic traversal still exceeds p95 18 ms or p99 25 ms, loses all visible solids, reports frame-path blocking completion, or opens the existing near/far coverage gap.

**Cost / blast radius.** Shared solid-render admission only. No storage/geometry semantics, arena formats, upload budgets, LOD bands, allocations per frame, or acceptance thresholds change. Cold/ramp convergence may expose work more slowly than the configured maximum; the production traversal regression directly measures whether that deferral is acceptable.

**Result.** Pending exact-SHA PlayMode traversal + saved-pose replay of the revised ramp.
