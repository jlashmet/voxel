# Experiment 009 — discriminate shared-context state from cold publication cost

## Exact result

- Feature/source SHA: `dbe6bd00bddf0666083cff2765362c2fad5e43d1`
- Exact request SHA: `42a49b66bbee3bcf4771029db927b42867d33551`
- Run/job: `34188737907` / `101942237983`
- Artifact: `single-test-34188737907`, id `10042481300`
- Artifact digest: `sha256:9f4c59da1472d9faa24e1501cbedc429fa83a0bebdc33b7c8d45ad64fff16480`
- Unity 6000.5.6f1 / Apple M4 Max / Metal

## Evidence

Automatic Rendering EditMode ran 575 tests: 574 passed, 1 failed, 0 quarantined/skipped/inconclusive. The only failure remained `GpuQueuedBatchCancellationTests.FineStepFourPublishesRealMixedStorageThroughGpuReadiness`; it took 15.386 seconds and failed the unchanged five-second publication bound. Fine steps 1/2 and the step-4 edit/retirement cases passed.

The new isolated production-path diagnostic `GpuStepFourPublicationPhaseTests.StepFourMixedPublicationReportsItsTerminalPhaseWithinBound` passed in 0.03 seconds in the same persistent editor. The standalone 180-second VoxelShowcase replay also completed. Therefore the step-4 count/write/page chain is not intrinsically a >5-second transaction once the surrounding state is equivalent enough to run it; the old failure is conditional.

The passing diagnostic differed materially from the failing fixture in one lifetime detail: the legacy fixture creates two shared-mirror contexts, disposes/replaces only the first inside `ValidateSummaryLane`, and keeps the second otherwise-idle context alive. The diagnostic created only one context. `GpuSurfaceMirrorCoordinator.ResetWorld` does reset queue lanes, dispatch/fairness state, coverage, recovery, readers and demand on world attachment, so the next discriminant is whether the still-live second consumer changes the result or whether the old case is paying a first-full-publication cold cost.

## Next discriminating experiment

Commit `9a49ace1266633a7d9aef5e6feab935d6c991d72` changes only the diagnostic test. It now matches the two-context lifetime exactly, including creating both consumers before page-arena configuration and replacing only the first. It also records iteration count, maximum synchronous `PrepareFrame` duration, summary/full-submission state and the existing bounded lane/fence/source diagnostics. Renderer behavior and the five-second deadline are unchanged.

If the matched two-context diagnostic fails, use its terminal phase to repair the shared-context lifetime defect. If it passes, shared-context lifetime is falsified and the consistent 15–16 second first full step-4 case points to cold synchronous publication initialization; isolate that cost before altering product behavior or the deadline.
