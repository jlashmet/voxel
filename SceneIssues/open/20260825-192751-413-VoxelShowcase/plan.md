# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Observed defect / acceptance
- Capture `screenshot-001.png` marks the performance telemetry at `Showcase Camera` `(77.953941,24.550051,-3.345814)`, FOV 70: sub-100 FPS while moving, slow fill, and transient/missing geometry.
- Pass requires sustained step-1/2 GPU completion, zero eligible CPU fallback/blocking waits, no visible holes, moving p95 <18 ms/p99 <25 ms, stationary p95 <8 ms, plus exact built-player pose inspection.

## Runtime evidence / hypotheses
- H1 (**confirmed material fix**): demand-scoped mirror recovery, exact footprints, optional empty halo, obsolete-demand cancellation, snapshotless GPU admission, bounded coverage cursors, and concurrent recovery removed global recovery starvation. The latest exact run keeps liveness green and reports zero frame-path blocking completions.
- H2 (**falsified as remaining tail**): fragmented mirror payload flush and renderer admission changes improved fill/recovery but did not clear traversal. Exact run `33281099872` still failed moving p99 at 75.912 ms.
- H3 (**falsified as remaining tail**): water discovery was sharing neither the presentation deadline nor region-view reuse. Experiment 010 bounded it and preserved immediate invalidation, but exact run `33282801017` still failed moving p99 at 79.164 ms while mirror liveness passed.
- H4 (**selected**): the remaining tail tracks streamed-region completion. The exact run generated regions 199 -> 243 while moving, with up to 30 pending. Renderer prepare/admission slices stayed far below the 79 ms tail. Source inspection shows `StepRegion` is time-sliced but `FinishRegion` synchronously calls `RefreshRegionSummary` across all 262,144 bricks inside `Voxel.Streaming.RegionCommit` after the final slice.

## Selected integration
- Preserve compact GPU payload upload/scatter, snapshotless production admission, demand-scoped mirror recovery, bounded descriptor/coverage work, water discovery deadline sharing, and stale-build/arena correctness already validated on this branch.
- Replace the whole-region summary's per-brick summary read/modify/write loop with a word-oriented rebuild: scan the same authoritative brick/mixed occupancy data, assemble 64 summary bits locally, then write each occupied/fully-solid word once.
- Leave normal per-block authoritative mutation behavior unchanged.
- Add `ShowcaseRegionCommitBudgetTests.RegionCompletionFramesStayInsideTraversalBudget`: real VoxelShowcase traversal must cross four production region boundaries, commit new regions, keep completion-frame `StepStreaming` below 25 ms, preserve visible solids/far fallback, and keep renderer blocking completions at zero.
- CPU world truth, collision, replication, HLOD/water, stale-build rejection, arena budgets, and the existing performance thresholds remain unchanged.

## Blast radius / cost
- Bulk occupancy-summary reconstruction only; no region data, commit order, change journal, collision, or rendering semantics change.
- Complexity remains O(262,144 bricks + mixed occupancy words), with no allocation. Summary writes drop from per-brick read/modify/write traffic to 4,096 word-pair assignments, reducing cache/safety-check overhead on the unbudgeted region-finalization frame.
- Ordinary edits continue through the existing single-block summary updater.

## Remaining gates
- Exact run `33282801017` is red only on moving p99 79.164 ms; its built-player capture converged to coherent final geometry and the mirror liveness regression passed.
- Commit experiment 011 + bulk-summary optimization + exact-scene region-completion regression on `fixes/agent-2` without touching `.github/test-request.json` there.
- Use `ci-test/fixes/agent-2` for one final exact-SHA request containing region-completion budget, mirror liveness, and unchanged GPU migration traversal, plus the exact VoxelShowcase built-player capture.
- Inspect every returned capture and test/runtime log. Only after green exact-SHA validation move open -> pending -> closed, set `status=fixed`/`resolvedUtc`, merge current `origin/master` into `fixes/agent-2`, and push that exact branch head to `origin/master` non-force.
