# Reproduction — SceneIssue 20260825-192751-413 VoxelShowcase

## Capture and marked region
- Canonical capture: `screenshot-001.png`, `1364x836`, frame `17168`, `87.756 s` after scene load.
- Replay pose: `Showcase Camera` at `(77.953941, 24.550051, -3.345814)`, FOV `70`.
- The single mark is a circle centered at normalized `(0.02825, 0.02643)` with radius `0.0360`, i.e. the top-left performance/FPS telemetry. The issue note explicitly calls out sub-100 FPS while walking, slow scene fill, and temporarily missing geometry; the mark is therefore a performance indicator, not a terrain-material defect.
- The repository connector exposes the PNG as a binary blob but cannot decode repository image blobs into the inspection surface. Capture metadata, pose, mark geometry and note were inspected from `issue.json`; exact visual validation is additionally performed through the CI-built-player screenshots below.

## Exact runtime reproduction
Rejected exact-head targeted run `33234469456` on feature SHA `8524c5a44832b97335232627dc0bf6aea42b1c39` reproduces a permanent GPU cutover liveness failure:
- focused traversal: `gpuBackends=12`, `gpuCompleted=3`, `gpuFallback=0`, `gpuWaitSlices=21138`, `chunks=770`, `visible=188`, `missing=582` at failure;
- real player: coverage grows only to `26 visible / 744 missing`, then remains pixel-stable from the ~25.8 s through ~35.8 s captures while the player continues around `195–218 FPS`;
- player telemetry stays at `jobs=12`, `missing=744`, no arena lease failures, and solid admission remains roughly `~2 ms`, so this is not the original 0.7 s CPU recovery cost and not general frame saturation.

## Hypotheses discriminated
1. **Global resident-world mirror recovery cost** — confirmed earlier, then fixed by demand-scoped recovery. Runtime cost fell from ~0.65–0.77 s/frame to a few milliseconds.
2. **Whole-region recovery granularity** — confirmed earlier, then fixed by exact demanded 8³ blocks. Early GPU completions and high FPS returned, but liveness still plateaued.
3. **Pending stage keeps an obsolete Storage generation forever** — implemented and falsified by run `33234469456`; exact traversal still stops after three GPU completions.
4. **Storage version and change-journal cursor are different counter domains** — falsified by source inspection: `RegionReadSource.Version` is exactly `_changes.CurrentVersion`.
5. **Completed GPU writes leak the shared extraction lease** — falsified by caller inspection: `CpuTransvoxelChunkCache` calls `_gpuExtraction.Release()` immediately after phase-10 write polling leaves `Pending`.
6. **Supported current hypothesis: recovery admission starvation** — `PrepareFromBridge` processes at most 64 queued blocks, but reports ready solely from mirrored version even when `RecoveryComplete` remains false. An already-covered worker can therefore begin another extraction while other workers still have demanded blocks queued. Since mirror mutation is globally forbidden whenever any extraction is active, repeated covered admissions can prevent the backlog from ever obtaining a drain point.

## Minimal behavioral discriminator
`GpuSurfaceMirrorRecoveryLivenessTests.DemandRecoveryCannotBeStarvedByCoveredGpuWork` is committed before the next production edit, per the three-failed-fix stop rule. It runs a focused 96 m `VoxelShowcase` traversal and observes the coordinator directly. A recovery backlog may overlap an already-dispatched extraction briefly, but `ReadyBlockCount` or completed GPU builds must resume within 180 rendered frames. The failure message reports active extraction count, recovery state, ready blocks, GPU completions/waits, and visible/missing coverage so a red result distinguishes recovery starvation from a raw GPU/readback stall.

## Expected fix contract
- Once demand recovery is queued, do not report mirror admission-ready until the queued recovery backlog drains.
- Queueing new demanded blocks must invalidate any same-frame cached successful `PrepareFromBridge` result so later workers cannot bypass the newly-created backlog in that frame.
- Preserve the existing safety rule that no mirror mutation occurs while an extraction may still read it.
- Preserve bounded work: at most 64 demanded blocks and 128 journal records per preparation slice; no CPU fallback for GPU-eligible work.
