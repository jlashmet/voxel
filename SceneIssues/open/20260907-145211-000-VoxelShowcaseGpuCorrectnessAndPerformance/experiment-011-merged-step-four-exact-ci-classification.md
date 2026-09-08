# Experiment 011 — merged step-four exact CI classification

## Hypothesis

The master-integrated narrow cold Editor/Metal timing boundary is sufficient for the real mixed-storage step-four publication regression without the rejected module-global warm-up, while the full exact-SHA run may still expose independent integration failures.

## Exact SHA and inputs

- Feature source: `909c9893bc0d491fd3676e87e2616da32e5d2933`
- Targeted-CI request: `00e166f7073b5c8871a45cd100bb49e5731ca1bd`
- Workflow run/job: `34202852688` / `101985422051`
- Requested test: `VoxelEngine.Rendering.Tests.EditMode.GpuQueuedBatchCancellationTests.FineStepFourPublishesRealMixedStorageThroughGpuReadiness`
- SceneIssue replay: production `VoxelShowcase`, 180 seconds
- Canonical integration: production `KentridgePlayableSlice` / System24 validation

## Evidence

The persistent repository-derived test phase passed (`exit_code=0`, `status=passed`) and the exact requested step-four test was the requested child. `persistent-failures.txt` is empty. All repository-selected Rendering release players completed with zero harness assertion failures: BlockHlodPropagation, GpuBoundaryContinuity, GpuCutoverContention, GpuDataDrivenEmission, GpuPhysicsQueryIndependence, GpuTransactionalPublication and GpuTransitionOwnership.

The standalone SceneIssue `VoxelShowcase` replay also completed its full 180-second window with zero harness assertion failures. This directly rejects the prior module-global warm-up as necessary: the merged narrow timing boundary is sufficient for the focused Renderer regression on this exact source.

The overall workflow nevertheless concluded `failure` because the mandatory canonical Kentridge integration failed independently. Its System24 route reached `WaitGameplayControl` with gameplay ready and the session running, but remained on `destinationRoute=NetworkApproach` with `exitedPub=False`; it timed out at position approximately `(111.7, 22.1, 84.2)` while targeting `(90.3, 22.2, 55.9)`. The agent-3 feature-vs-master diff contains no `Assets/Game` / Kentridge production change that owns this gameplay-control transition.

## Verdict

**Focused Renderer gate: PASS. Overall exact-SHA acceptance: BLOCKED by an independent canonical Kentridge/System24 product/integration failure.** This is not an infrastructure failure, so the request must not be rerun unchanged. It also is not evidence to modify System24 from agent-3.

No SceneIssue closure is justified: the independent castle stationary/traversal/return reproduction, bounded cell-to-draw causal trace, full-scene correctness, performance campaign/Metal trace, Water GPU presentation migration, device budgets/lifetime work and final exact-SHA integration remain open.

## Next step

Continue the next non-blocked agent-3 acceptance work: independently reproduce the annotated castle stationary/traversal/return route and obtain a bounded cell/candidate-to-selected-payload-to-draw causal trace under the existing settle/convergence budgets. Preserve the Kentridge failure as an external integration blocker for final validation; do not change its owning game domain from this branch.
