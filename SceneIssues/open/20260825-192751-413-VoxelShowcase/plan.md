# Plan — SceneIssue 20260825-192751-413 VoxelShowcase performance/coverage

## Defect / acceptance
The capture reports sub-100 FPS while walking, slow surface fill, and missing geometry. Acceptance requires saved-pose and moving traversal coverage, green exact-SHA production-path regression, saved-pose replay evidence, no relaxed budgets, and measured evidence identifying the dominant remaining frame-time bottleneck.

## Evidence / selected fix
GPU cutover was partly missing: restoring the validated exact-ring GPU extractor for source steps 1/2 improved saved-pose replay to ~168 FPS with no late missing geometry. Arena pressure was not the first moving failure. Exact-pose experiment 012 proved Unity camera/frustum math valid while production had no usable surface candidates. The causal startup defect was initial camera-window discovery starvation.

Commit `20a32987b0273e6f8f2718e4bb169648cf7e3dae` makes initial clipmap creation trigger camera-near priority discovery while preserving region-difference admission for later movement. It changes no build, upload, arena, LOD, or frame-time budget. Focused exact-pose regression `ShowcaseCapturePoseFrustumDiagnosticsTests.CapturePoseFrustumContainsForwardProbeAndSurfaceCandidates` passed exact-SHA CI `3a06a96f...`. Moving acceptance remains `ShowcaseTraversalPerformanceTests.ContinuousPlayerTraversalNeverStuttersOrOpensNearFarGap` with unchanged p95/p99 limits.

## Executable bottleneck investigation
After exact request `6da72281f0f741c0b254681b337fe1f807c47b29` completes, measure the same saved pose and deterministic traversal with temporary test-scoped counters/timers. Record p50/p95 frame time plus work/queue counts for: camera discovery/scheduler, GPU extraction dispatch/completion, CPU fallback/step-4 meshing, mesh upload, culling/draw submission, resident/visible chunks, and pending build/upload work.

Run one-variable A/B discriminators against that baseline:
1. **Freeze discovery/generation after warm-up** while rendering the settled resident set. A large frame-time drop implicates ongoing discovery/build work; little change points downstream.
2. **Suppress draw submission while continuing discovery/meshing/upload.** A large drop implicates culling/render/GPU draw cost; little change points to world/surface work.
3. **Disable CPU fallback/step-4 surface work** while retaining validated GPU exact-ring extraction. Improvement quantifies fallback cost and reveals whether it is stealing frame budget.
4. **Disable coarse/HLOD fallback only.** Improvement isolates far/coarse rendering versus near-ring work.
5. **Simplified content runs:** terrain-only, castle-only, then the full showcase at identical camera/resident bounds. Normalize against visible/resident chunk counts to distinguish per-chunk pipeline overhead from content/triangle complexity.

Each experiment gets a concise `experiment-NNN-*.md` with source SHA, exact toggles, counters, frame-time deltas, and falsification result. Temporary instrumentation/toggles must be reverted before promotion. Optimize only the stage whose controlled removal produces the dominant reproducible delta, then rerun the exact-pose regression, moving traversal, and final replay.

## Blast radius / remaining gates
Shared surface scheduler only; priority checks inspect the existing 3×3×3 resident-region neighborhood with no persistent allocation/job/budget increase. Current master has advanced with unrelated compiled WorldBuilder changes and must be merged before final promotion; product-changing overlap requires re-verification.

- [ ] Exact traversal/replay request `6da72281...` completes and artifact is inspected.
- [ ] Execute bottleneck measurement + at least one discriminating A/B pair; record result.
- [ ] Commit `verification-final.png`; complete `issue.json`; close per task authorization.
- [ ] Merge current master, reverify as required, then non-force advance `master`.
