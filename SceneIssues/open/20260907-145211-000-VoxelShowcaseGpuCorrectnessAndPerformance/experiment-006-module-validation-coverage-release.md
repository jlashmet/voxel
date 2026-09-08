# Experiment 006 — exact module validation coverage-release leak

## Hypothesis

The corrected capacity-aware source admission can still pass its focused ownership tests while leaking coordinator demand when a production-faithful queue path already owns `RequestCoverage` before the extraction context releases it. A leaked demand footprint can contaminate later asynchronous summary/water tests and block bounded publication.

## Exact source and inputs

- Feature source under test: `303b2afb931a47b1fdd07c40accaeba6e37620dc`.
- Exact targeted-CI request: `90b77cf525b74b4ba5dd0e8a2e6c38ac4312c4c2`.
- Workflow run/job: `34173008582` / `101896830552`.
- Requested test: `VoxelEngine.Rendering.Tests.EditMode.GpuPersistentSourceDemandOwnershipTests`.
- Automatic affected module validation: `VoxelEngine.Rendering.Tests.EditMode`.
- Standalone SceneIssue replay: 180 seconds.

## Evidence

The exact run reached the real tests and standalone player. The standalone replay passed, but module validation failed with 568 passed and 5 non-quarantined failures:

1. `GpuQueuedBatchCancellationTests.FineCompletedSourceEditRejectsTheCandidate` — final demand footprint expected 0, got 1.
2. `GpuQueuedBatchCancellationTests.FineStepOnePublishesRealMixedStorageThroughGpuReadiness` — final demand footprint expected 0, got 1.
3. `GpuQueuedBatchCancellationTests.FineStepTwoPublishesRealMixedStorageThroughGpuReadiness` — final demand footprint expected 0, got 1.
4. `GpuQueuedBatchCancellationTests.FineStepFourPublishesRealMixedStorageThroughGpuReadiness` — bounded lane did not reach publication by the test deadline.
5. `GpuWaterSurfaceChunkCacheTests.AdditionalRegisteredWaterMaterialIsDiscoveredAndPublished` — GPU water transaction did not finish.

The first three failures directly expose leaked coordinator coverage. `ValidateSummaryLane` registers real coverage before dispatch and marks the context `_coverageRequested`; `GpuSurfaceExtractionContext.ReleasePersistentCoverage` then delegates to `GpuSurfaceSourceAdmission.Release`. The new admission implementation returned immediately when the context was not present in `s_Owners`, before calling `ReleaseCoverage`/`ReleaseEditWatch`. That conflated admission accounting with the independent coordinator-coverage lifetime. The leaked demand explains cross-test contamination consistent with the later step-4 and water timeout failures. This is a product failure, not runner/import infrastructure.

## Repair

Commit `9bbf41d00d7b3976ec378832ec19abf4ec7b5f4c` separates the two lifetimes: owner/reservation accounting remains conditional on admission ownership, but coordinator coverage/edit-watch release always executes for the explicit context lifetime. The coordinator's world-epoch guards make stale-world release safe.

Commit `676709da2c66225d10dd9b5c31b08b712cf0b85f` adds `ReleaseDropsCoordinatorCoverageWithoutAdmissionOwnership`, which establishes coordinator coverage with zero admission owners and requires release to return `DemandFootprintCount` to zero without changing owner/reservation counts.

## Verdict and next step

The exact request is correctly classified as an in-scope product failure. Do not retry the failed source unchanged. Run a new exact-SHA targeted request from the documented repaired feature head, including the focused ownership regression plus automatic Rendering module validation and standalone SceneIssue replay. Keep the broader correctness/performance/migration gates open until that evidence is green and subsequent acceptance work is complete.
