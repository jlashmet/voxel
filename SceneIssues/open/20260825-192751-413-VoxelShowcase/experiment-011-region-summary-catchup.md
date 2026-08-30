# Experiment 011 — word-oriented region summary catch-up

## Trigger
Exact-SHA run `33282801017` on `5443cd73f5991d37dffbe5a2f1023ea162d35013` left one failure: `ShowcaseGpuMigrationTests.MovingShowcaseCompletesGpuSurfaceBuildsAndPreservesCoverage` reported moving p99 `79.164 ms` against the `<25 ms` gate. The mirror-recovery liveness regression passed.

The same run restored coverage, completed GPU work, reported zero frame-path blocking completion violations, and the built-player capture converged to stable geometry. During movement, however, Showcase streaming advanced from 199 generated regions to 243 with as many as 30 pending.

## Competing hypotheses

### H1 — GPU mirror/readback recovery is still blocking the player frame: falsified
The exact traversal had zero `FramePathBlockingCompletionViolations`; demand-recovery liveness passed. Built-player `PREPARESECTIONS` telemetry kept solid scheduler/admission/worker/upload slices far below the 79 ms tail once startup had converged.

### H2 — water discovery owns the remaining tail: falsified as dominant
Experiment 010 shared the scheduler deadline with water discovery and guaranteed only four bricks of progress after the deadline. The exact rerun still produced a 79.164 ms moving p99, so bounding that secondary slice did not move the failure.

### H3 — ordinary terrain generation or feature generation is unsliced: falsified
`ShowcaseWorld.StepStreaming` gives terrain a realtime deadline and `StepRegion` advances only 48 brick columns per slice. Queued feature generation also stops at its own share of the same frame budget. The observed tail occurs while regions finish, not because those loops lost their deadline.

### H4 — region finalization pays an unbudgeted whole-region occupancy-summary rebuild: selected
`FinishRegion` runs under profiler marker `Voxel.Streaming.RegionCommit` after the last time-sliced terrain slice. It calls `RegionMutationStore.RefreshRegionSummary` synchronously before commit.

A region contains `64^3 = 262,144` bricks. The previous bulk rebuild called the per-block updater for every brick. Each call read and then read/modified/wrote the occupied and fully-solid summary NativeArrays; mixed bricks additionally scanned their eight occupancy words. That makes the final frame pay hundreds of thousands of NativeArray summary updates even though generation itself was sliced to 3 ms.

This matches the runtime shape: stationary rendering is fast after convergence, renderer admission is bounded, but traversal creates new regions and the long tail survives every renderer-side optimization.

## Change
`RegionMutationStore.RefreshRegionSummary` now builds one 64-block summary word at a time. It still reads every authoritative `BrickRef`, and it still scans mixed-brick occupancy exactly as before, but it writes each occupied/fully-solid summary word once instead of performing per-brick read/modify/write updates.

The ordinary per-block mutation path is deliberately unchanged.

A new exact-scene PlayMode behavioral regression, `ShowcaseRegionCommitBudgetTests.RegionCompletionFramesStayInsideTraversalBudget`, drives the real VoxelShowcase player 210 m across at least four region boundaries. Whenever `ShowcaseWorld.RegionsGenerated` advances, it gates that production `StepStreaming` completion frame below 25 ms and simultaneously verifies visible solid coverage, zero renderer blocking completions, and far-field fallback safety.

## Blast radius
- Authoritative voxel contents, region commit ordering, persistence/replication truth, collision, and mutation semantics are unchanged.
- Only the bulk occupancy-summary rebuild implementation changes; single-block edit/update behavior remains on the existing path.
- All 262,144 summary bits retain the same definition. Empty/uniform bricks use the same material rule; mixed bricks use the same eight occupancy words.
- Showcase remains the motivating bulk caller, but any other legitimate bulk caller receives the same summary with fewer NativeArray writes.

## Cost
- Time complexity remains O(bricks + mixed occupancy words), so there is no hidden unbounded work or memory growth.
- Summary NativeArray writes fall from two read/modify/write operations per brick to two assignments per 64 bricks: 262,144 block updates become 4,096 word-pair writes.
- No allocations are added.
- Expected tradeoff is strictly lower CPU/cache traffic on bulk finalization; no per-frame renderer or gameplay work is added.

## Validation required
- A focused EditMode behavioral gate exercised the production method over a complete 262,144-brick
  region, checked all 4,096 occupied/fully-solid word pairs across empty, uniform, partial-mixed,
  full-mixed, and word-boundary cases, and passed at 0.794 ms against the unchanged 25 ms budget.
- Local invocation compiled and entered the real-scene test, then the repository wrapper killed
  Unity at 6,234 MB against the mandated 6,144 MB ceiling before assertions. This is an
  infrastructure result, not a product verdict.
- Exact targeted CI must include the new region-completion regression, the existing mirror-recovery liveness regression, and the unchanged moving GPU migration gate.
- The built-player capture must again load `Assets/Scenes/VoxelShowcase.unity`; inspect every captured frame and the marked telemetry region.
- Do not close the issue unless moving p99 is `<25 ms`, region completion stays within its gate, coverage remains intact, and the exact-SHA run is green.
