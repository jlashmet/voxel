# Plan

## Acceptance and ownership
`issue.json` is the contract (`captures: []`). Preserve the source-backed Mounting Force macro graph while delivering physical settlements, contiguous terrain-aware hard routes, reusable regional geography, Rossdam Lake, Southern Ridge/pass, real CharacterMotor traversal, durable built-player evidence, and bounded CPU/GPU/memory/streaming cost.

Owned validation surfaces:
- WorldBuilder: `Validation/MacroPhysicalWorld` through production catalogue/rendering.
- Showcase: `Validation/FeatureResidency` through production residency/readiness.
- Kentridge Playable: `Validation/KentridgeMacroWorld` through the real slice/evidence driver.
- Rendering: `Validation/GpuSurfaceMirrorRelocation` through production GPU mirror/extraction/publication.

## Latest evidence and root cause
Exact run `33943012273` validated source `a90fd76933b2269fc3aea660d137c196a9882987` against master `51797c954490425964e602d6bb2252a0d7a7c5aa`. Persistent tests and the requested count-batch fairness regression passed. Both Kentridge players reached real macro content without forbidden exceptions; the 180-second replay reached `content-ready target=moordell columns=4`, but strict capture readiness never completed. Durable evidence still showed large checkerboard terrain holes, so coverage assertions must not be weakened.

The replay ended near `missingVisible=102`, `flight=8`, `phases=0x2`, with phase-2 requests occasionally 6.5–8.5 seconds old. `batchArenaWait=0` falsifies GPU-fence backpressure. A suspected scheduler/Unity frame-clock mismatch is also falsified: production `VoxelRenderPass` passes `Time.frameCount` into `VoxelSurfaceScheduler`, which passes that same frame to mirror preparation/sealing.

The demonstrated liveness defect is worker service ordering. Paged GPU completion is produced by the coordinator, but phase 9 consumes `TryTakePagedBatch` and releases the shared extraction slot only when that owning `CpuTransvoxelChunkCache.Prepare` runs. The renderer-wide convergence budget is about 4 ms while the scheduler round-robins all 22 workers without prioritizing already-active builds. It can therefore spend the frame on inactive shards while eight completed/ready GPU owners keep all extraction slots occupied, throttling publication to worker-poll cadence rather than GPU batch cadence.

Selected correction: preserve all existing budgets and GPU limits, but build a zero-allocation admission order that services already-active workers first, with existing round-robin fairness inside active and inactive groups. Add a behavioral EditMode regression against this production ordering helper.

## Remaining gates
1. Exact-SHA targeted CI: new active-worker liveness regression, existing GPU relocation/fairness coverage, repository-derived module validation, and 180-second SceneIssue replay.
2. Inspect full-resolution built-player evidence; require all settlements, Rossdam water/constrained route, Southern Ridge/pass, network overview, differentiated terrain, and real CharacterMotor traversal.
3. Record per-target convergence plus FPS/CPU/GPU/streaming and process/managed/native/GPU memory against existing budgets.
4. Merge then-current `origin/master`, revalidate the exact merged feature SHA as required, complete every task/acceptance item, move only this issue `open -> closed`, then promote through PR + auto-merge and monitor the required PR gate until merged.
