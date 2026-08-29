# Reproduction — SceneIssue 20260825-192751-413 VoxelShowcase

## Capture and marked region
- Canonical capture: `screenshot-001.png`, `1364x836`, frame `17168`, `87.756 s` after scene load.
- Replay pose: `Showcase Camera` at `(77.953941, 24.550051, -3.345814)`, FOV `70`.
- The single mark is a circle centered at normalized `(0.02825, 0.02643)` with radius `0.0360`, i.e. the top-left performance/FPS telemetry. The issue note explicitly calls out sub-100 FPS while walking, slow scene fill, and temporarily missing geometry; the mark is therefore a performance indicator, not a terrain-material defect.
- The repository connector exposes the PNG as a binary blob but cannot decode repository image blobs into the inspection surface. Capture metadata, pose, mark geometry and note were inspected from `issue.json`; exact visual validation is performed through CI-built-player screenshots.

## Current exact runtime reproduction
Exact request run `33275543571` targeted feature SHA `4722b74771ab2a265157d800bdf9500f7ffcb9fe` with the focused liveness regression, 210 m migration regression and 45 s built-player replay.
- `GpuSurfaceMirrorRecoveryLivenessTests.DemandRecoveryCannotBeStarvedByCoveredGpuWork` passed.
- `ShowcaseGpuMigrationTests.MovingShowcaseCompletesGpuSurfaceBuildsAndPreservesCoverage` failed after 78.6 s because the view did not settle within 20 s: `visible=43`, `missing=579`, `dirty=1927`, `jobs=12`, `uploads=0`, `gpu=154/0`.
- `gpu=154/0` is important: supported work is now staying on GPU, so eligible CPU fallback is no longer the explanation for slow fill.
- All four exact built-player captures were inspected. t15.4 is almost empty, t25.4/t35.4 recover the main castle/town, and `verification-final.png` still shows incomplete world/right-side coverage.
- The player initially runs around 245–264 FPS, then degrades to roughly 5–18 FPS. Late telemetry repeatedly attributes ~190–195 ms to solid admission / individual solid-worker `Prepare`; arena `leaseFail=0` and relief remain negligible.

## Hypotheses discriminated
1. **Global resident-world mirror recovery cost** — confirmed earlier, then fixed by demand-scoped recovery; the original ~0.65–0.77 s global mirror stall disappeared.
2. **Recovery admission starvation / exact-block demand** — confirmed earlier and addressed by exact demand, fairness and bounded recovery; focused liveness now passes and GPU completions continue.
3. **Transient/empty GPU completion routes supported work through CPU** — partially confirmed, then fixed. The latest exact migration reports `gpu=154/0`, proving zero eligible fallback, but the scene still fails performance/coverage, so fallback is not the remaining root cause.
4. **Geometry arena pressure** — falsified for the current failure: `leaseFail=0`, substantial unused capacity and negligible relief coexist with ~195 ms worker stalls.
5. **Storage/residency scans or build selection** — disfavored by scheduler section timings; those measured sections stay small while an entire individual `worker.Prepare` spikes.
6. **Fragmented persistent-mirror payload flush** — current supported hypothesis. Recovery stages up to 64 mixed bricks. LIFO slot reuse means their GPU slot indices can be fragmented. The old `GpuVoxelBrickMirror.FlushPayloadSlots` made four synchronous `ComputeBuffer.SetData` calls per contiguous dirty-slot run, so one maximally fragmented bounded recovery slice can cause 256 driver uploads inside the uninstrumented GPU path of one `worker.Prepare`.

## Behavioral discriminator / expected contract
- `GpuSurfaceMirrorRecoveryLivenessTests.DemandRecoveryCannotBeStarvedByCoveredGpuWork` must remain green, proving the recovery-liveness fixes are preserved.
- `ShowcaseGpuMigrationTests.MovingShowcaseCompletesGpuSurfaceBuildsAndPreservesCoverage` is the primary behavioral regression for the current defect: exact traversal must continue GPU completions, keep eligible fallback/blocking completion at zero, settle coverage, and meet its existing moving/stationary frame budgets.
- Exact built-player replay must independently show the original marked FPS/slow-fill symptom is gone at the captured scene/camera workflow.
- Payload publication may change transfer mechanics only. Storage truth, slot/directory mapping, recovery budgets, live payload offsets, GPU extraction semantics and CPU/HLOD/water paths must remain unchanged.
