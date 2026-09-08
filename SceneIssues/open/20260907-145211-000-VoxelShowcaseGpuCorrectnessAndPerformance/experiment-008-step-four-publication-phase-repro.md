# Experiment 008 — isolate step-four publication phase

## Hypothesis

The earlier coverage-lifetime defect is repaired, but the step-4 real mixed-source path still has a distinct downstream publication stall. Because the same bounded publication symptom survived both the source-admission repair and the later coordinator-coverage release repair, do not make another speculative renderer change. First discriminate whether the remaining stall is in source-summary preparation/recovery, count/write/page submission, compact outcome readback, graphics-fence completion, or context publication.

## Exact input

- Feature/source SHA: `cacb7b0b1858bc00f7b04e19e5a4e61227b8808a`
- Exact request SHA: `9f1f4ee42f7389066bced55f30aa34d998673a81`
- Run/job: `34183296361` / `101926567951`
- Artifact: `single-test-34183296361`, id `10040373586`
- Artifact SHA-256: `4b84fd42277f5aea3294de7ba7fe235dcb7d943a18dd60e4d06f7f1e4ebc8ce5`
- Requested focus: `VoxelEngine.Rendering.Tests.EditMode.GpuPersistentSourceDemandOwnershipTests`
- Automatic owning module: `Assets/VoxelEngine/Rendering`
- Hardware/runtime: Unity 6000.5.6f1, Apple M4 Max, Metal

## Evidence

The persistent Rendering EditMode assembly executed 574 tests: 573 passed, 1 failed, 0 quarantined/skipped/inconclusive. The only failure was:

`VoxelEngine.Rendering.Tests.EditMode.GpuQueuedBatchCancellationTests.FineStepFourPublishesRealMixedStorageThroughGpuReadiness`

Failure: `The bounded lane must reach publication within the test deadline. Expected: True But was: False.` The case ran about 16.2 seconds despite retaining the existing five-second logical deadline, consistent with a frame/update slice blocking while the asynchronous GPU path was active.

Closely related cases passed in the same persistent editor: fine steps 1 and 2 full mixed publication, step-4 completed-source edit rejection, and step-4 submitted-resource retirement. The standalone 180-second VoxelShowcase capture also completed its workflow phase. This falsifies the earlier theory that the remaining red gate is simply leaked coordinator demand from `GpuSurfaceSourceAdmission.Release`; that leak no longer reproduces across the suite.

The failing fixture reaches real asynchronous summary submission (`sawSubmission=true`) but never observes `GpuSurfaceExtractionContext.TryTakePagedBatch` ready before its bound. That places the unresolved transition after queue admission and before paged completion.

## Diagnostic change

Add `GpuStepFourPublicationPhaseTests.StepFourMixedPublicationReportsItsTerminalPhaseWithinBound`, a focused module-local EditMode repro using the same production mirror, source-resolution shader, count batch, page arena and mixed storage. It preserves the five-second bound and reports only bounded control-plane state on timeout: summary record/cursor/recovery state, submitted/outcome state, fence validity/passed state, count readbacks/arena waits, pending/ready/mixed source counts, demand/readers and active extractions. It does not read authoritative world state from the GPU, alter capacity, relax the deadline, or change renderer behavior.

## Verdict / next step

The coverage-lifetime repair is supported; a separate step-4 publication-phase defect remains. Run the new focused repro on the exact diagnostic SHA. Use the reported terminal phase to choose one causal product repair; do not extend the deadline or broaden the patch first.
