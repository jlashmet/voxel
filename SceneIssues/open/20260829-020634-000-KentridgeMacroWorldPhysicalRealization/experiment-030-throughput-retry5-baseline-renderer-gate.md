# Experiment 030 — throughput retry 5 / baseline renderer gate

## Hypothesis
With runner memory healthy, the perimeter-foundation regression and 180-second built-player replay can distinguish the Kentridge throughput fix from the earlier runner-memory blocker.

## Test
Targeted-CI transport `ci-test/fixes/agent-6`, run `33641059051`, source SHA `7e6d30858677f2504763e891289293c9507cfd9f`, requested PlayMode test `VoxelEngine.Tests.PlayMode.TopDownWorldPhysicalVoxelCatalogueBlockoutTests.GenericBuildingBlockoutUsesBoundedFoundationAndWallShellsInsteadOfSolidVolumes`, canonical SceneIssue replay 180 s.

## Result
- Runner admission was healthy: Unity started with 38,665 MB free. The previous 8,192 MB free-memory-floor blocker did not recur.
- Repository-derived persistent validation failed before the requested process-isolated PlayMode test could execute. `persistent-summary.txt` reports 15 failures in `VoxelEngine.Tests.EditMode`, all renderer/GPU tests (including `GeometryPipelineArchitectureTests.SolidArenaPressureIsBackpressureNotBufferGrowth`, GPU geometry arena/oracle/negative-shell parity, and GPU vertex-attribute parity). The requested test therefore has no result from this run.
- This is branch/master baseline drift rather than a Kentridge assertion: current master has advanced hundreds of commits and its `SurfaceGeometryArena.TryAcquireAligned` pressure implementation no longer matches the stale feature-side architecture assertion. Do not modify renderer code from this assignment.
- The standalone player replay still built and ran successfully for the same exact source. It reported `runtime-catalogue definitions=480` with all macro settlements/routes/geography present and no harness assertion failures.
- Moordell content became ready at about 85 s, versus about 175 s in pre-plinth run `33563288872`, a roughly 90 s convergence improvement. At readiness, residency telemetry reported load radius 3, 29 horizontal columns, 31 total resident snapshot, 29 residents in radius, and `featureVerticalExtra=0`.
- After content readiness, capture did not advance because renderer publication coverage stayed false (`FAR ... coverage=False`) through the end of the 180 s replay. This is consistent with the same stale renderer baseline blocking repository validation; no evidence-driver exception was logged.
- Post-30 s interval FPS median was approximately 103.9 across 143 one-second samples, while burst stalls remained. This run is useful throughput/cost evidence but is not closure evidence because the requested regression never ran and readable Fairy/Orc captures were not produced.

## Decision
Keep the Kentridge perimeter-foundation implementation unchanged. Record pre-merge exact-SHA validation as blocked by unrelated renderer baseline drift, retain the successful player throughput/residency evidence, and continue only independent Kentridge acceptance work. Do not weaken the evidence driver's published-coverage requirement and do not repair renderer tests/code in agent-6 scope.
