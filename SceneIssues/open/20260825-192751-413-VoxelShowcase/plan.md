# Plan — SceneIssue 20260825-192751-413 VoxelShowcase performance/coverage

## Defect / acceptance
The capture reports sub-100 FPS while walking, slow surface fill, and missing geometry. Acceptance requires the saved pose and moving traversal to retain voxel coverage, the focused production-path regression to pass on exact-SHA CI, saved-pose replay evidence, and no relaxation of existing frame-time or geometry budgets.

## Evidence / competing hypotheses
1. **GPU cutover was disabled or too narrow.** Partly supported: restoring the existing validated exact-ring GPU extractor for source steps 1/2 improved the saved-pose replay to about 168 FPS with no late missing geometry; step 4/HLOD and fallback paths remain CPU/coarse.
2. **Arena pressure removes visible geometry during motion.** Rejected as the first-failure cause: long real-player traversal remained mostly 150–330 FPS while pressure rose later, but the deterministic production traversal lost all draws at movement frame 5.
3. **Camera/frustum math rejects the captured view.** Rejected by experiment 012: Unity's frustum accepted a forward probe at the exact saved pose while production reported `known/inBand/frustum=170/38/0` and step1 `res=0 known=0`.
4. **Surface discovery starves the initial camera window.** Supported by code and captured telemetry. Camera-near priority discovery was only activated after a clipmap had a previous window and moved, so first-window startup fell back to the global resident sweep and could leave the near ring completely unknown.

## Selected fix / regression
Commit `20a32987b0273e6f8f2718e4bb169648cf7e3dae` treats initial clipmap creation as a priority-discovery event while retaining region-difference admission only for later movement. It changes no build, upload, arena, LOD, or frame-time budget. `ShowcaseCapturePoseFrustumDiagnosticsTests.CapturePoseFrustumContainsForwardProbeAndSurfaceCandidates` is the focused behavioral regression through the production scheduler at the exact captured pose. The existing `ShowcaseTraversalPerformanceTests.ContinuousPlayerTraversalNeverStuttersOrOpensNearFarGap` remains the moving acceptance with its original p95/p99 limits.

## Blast radius / cost
Shared surface scheduler only; camera-near priority checks at clipmap changes inspect at most the existing 3×3×3 resident-region neighborhood. No new persistent allocation, job, mesh path, or budget increase is introduced. Current master `9a633c15...` was merged cleanly as `0660116400...`; its delta was limited to another closed SceneIssue.

## Remaining gates
- [ ] Focused exact-SHA CI `3a06a96f...` passes (currently queued on shared macOS runner).
- [ ] Final exact-head traversal + saved-pose replay passes; inspect artifact and commit `verification-final.png`.
- [ ] Complete `issue.json`, move `open/`→`pending/` in separate bookkeeping commit, then close per task authorization.
- [ ] Refresh/merge current master if it advances, then non-force fast-forward verified branch to `master`.
