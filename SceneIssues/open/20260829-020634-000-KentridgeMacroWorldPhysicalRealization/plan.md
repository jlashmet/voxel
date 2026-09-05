# Plan

## Acceptance and ownership
`issue.json` is the contract (`captures: []`). Preserve the source-backed Mounting Force macro graph while delivering physical settlements, contiguous terrain-aware hard routes, reusable regional geography, Rossdam Lake, Southern Ridge/pass, real CharacterMotor traversal, durable built-player evidence, and bounded CPU/GPU/memory/streaming cost.

Owned validation surfaces:
- WorldBuilder: `Validation/MacroPhysicalWorld` through production catalogue/rendering.
- Showcase: `Validation/FeatureResidency` through production residency/readiness.
- Kentridge Playable: `Validation/KentridgeMacroWorld` through the real slice/evidence driver.
- Rendering: `Validation/GpuSurfaceMirrorRelocation` through production GPU mirror/extraction/publication.

## Latest evidence and root cause
Exact run `33949455376` validated source `75da81caa3be77b45ecad5adb9050391d5b866a0` against master `51797c954490425964e602d6bb2252a0d7a7c5aa`. The fence-owned extraction-lifetime correction and production GPU relocation/liveness regression exercised successfully, and the standalone SceneIssue player exited cleanly, but repository-derived required module validation remained product-red. The Kentridge module did not converge through Moordell within its player contract; the 180-second replay reached `content-ready target=moordell columns=4` but still did not reach strict `capture-ready` or advance to the next macro target. Durable screenshots continue to show real checkerboard terrain gaps, so readiness/coverage assertions and time budgets must not be weakened.

The previous ownership-lifetime defect is now demonstrated corrected. At the end of the latest replay the coordinator reports `activeExtractions=0` even while renderer worker diagnostics can still report eight phase-2 GPU contexts. Mirror-ready work also reconciles to published plus stale results. Therefore the graphics-fence retirement ledger is releasing active-count and mirror-reader protection independently of worker polling as intended; the remaining failure is downstream of that transfer.

The demonstrated remaining liveness defect is CPU worker completion consumption. A paged GPU batch is marked ready by `GpuSurfaceExtractionContext.CompletePagedBatch`, but `CpuTransvoxelChunkCache` consumes that result only when its phase-9 worker is polled. `VoxelSurfaceScheduler` currently visits all 22 workers in cursor order under one shared solid deadline. Earlier workers can consume that deadline doing ordinary admission/build work, leaving already-completed phase-2 GPU results unpolled for multiple frames or seconds. The latest artifact's `activeExtractions=0` together with `flight=8 phases=0x2` is the minimal production signature of that gap.

Selected correction: preserve the existing solid deadline, convergence scaling, build ceilings, streaming radius, and cursor fairness, but poll workers that currently hold phase-2 GPU results before ordinary workers. Only those bounded GPU completion consumers receive first service; this does not restore the previously reverted broad "all active workers first" scheduler experiment and does not expand any budget. A pure ordering regression will require phase-2 priority while proving ordinary cursor order remains unchanged when no priority worker exists.

## Remaining gates
1. Implement the narrow phase-2 completion-consumption ordering policy and focused behavioral regression.
2. Exact-SHA targeted CI: run that regression plus existing GPU relocation/liveness coverage, repository-derived module validation, and the 180-second SceneIssue replay.
3. Inspect full-resolution built-player evidence; require all settlements, Rossdam water/constrained route, Southern Ridge/pass, network overview, differentiated terrain, and real CharacterMotor traversal.
4. Record per-target convergence plus FPS/CPU/GPU/streaming and process/managed/native/GPU memory against existing budgets.
5. Merge then-current `origin/master`, revalidate the exact merged feature SHA as required, complete every task/acceptance item, move only this issue `open -> closed`, then promote through PR + auto-merge and monitor the required PR gate until merged.
