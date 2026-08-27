# Experiment 019 — same-frame identical-view preparation

## Hypothesis
The unchanged traversal gate includes an automatic player-frame render followed by explicit `camera.Render()` in the same Unity frame. `VoxelSurfaceScheduler` advances change/discovery/build/publication state only on the first `Prepare`; its same-frame path reruns only visibility. While coverage is converging, that second identical-view call pays another full ~4–6 ms slot/LOD visibility sweep and is a material part of the measured p95 tail.

## Action / source
At the render-pass boundary, reuse the scheduler result only when `LastAdvancedFrame == Time.frameCount`, the camera instance is identical, and its position and rotation exactly match the view already prepared that frame. Different cameras or a same-frame pose change still call `Prepare` and therefore recollect visibility. Draw staging and submission still run for every render.

Production change began at `cd0f9b216734e7047fb8bc52ffd93aec37f3c396`; plan/evidence bookkeeping follows on the same feature branch.

## Competing evidence
Cross-frame converging visibility reuse is not sufficient: exact request `agent-2-192751-final-bounded-visibility-reuse-v2-20260827-1219` preserved coverage but failed at p95 23.40 ms. Historical profiling also measured no gain from replacing the frustum test and found cadence-throttled 360-degree demand reduced drawn coverage, so this experiment does not alter either behavior.

## Falsifier
Reject if exact-SHA `ContinuousPlayerTraversalNeverStuttersOrOpensNearFarGap` still exceeds p95 18 ms / p99 25 ms, loses visible solids or fallback coverage, reports a blocking completion, or the 45 s saved-pose replay regresses.

## Blast radius / cost
Render-only idempotence within one Unity frame and one exact camera pose. No cross-frame staleness, gameplay/collision authority, Storage state, clipmap demand, worker admission, geometry publication, or acceptance thresholds change. Cost is three cached camera fields and comparisons per render.

## Result
Pending exact-SHA targeted CI and 45 s saved-pose replay.
