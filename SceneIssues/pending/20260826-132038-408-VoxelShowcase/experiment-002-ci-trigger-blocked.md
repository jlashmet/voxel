# Experiment 002 — targeted CI trigger is blocked

## Hypothesis

The focused EditMode regression can provide the required red/green proof through the assigned `ci-test/fixes/agent-5` mailbox, and a normal request-file push or explicit ref move will create a `tests-single.yml` run with `ci/single-test` status.

## Performed

Focused test:
`VoxelEngine.Tests.EditMode.FarTerrainSharedTexturePresentationTests.FarTerrainReusesVoxelSurfaceTextureSamplingContract`

Pre-fix source head: `9a2a4695cb331db2bc486d6583b9ebcde8098e4d`.
Candidate production fix: `506d4b37a42639bb1b9d48f1796e7794446d3c40` (`Unify far terrain grass texture sampling`).

The candidate fix changes only `Assets/VoxelEngine/Rendering/Runtime/Shaders/FarTerrain.shader`: the far clipmap now consumes the renderer-owned `_MaterialAlbedo`, `_MaterialSampling`, `_MaterialSurface`, `_AlbedoTextures`, and `_VoxelSize` globals, resolves its existing authoritative vertex albedo back to the closest semantic material row, and samples with the same `SurfaceUV`, base-voxel world coordinate, triplanar scale, and `hitDistance / 350.0` attenuation used by `SmoothSurface`.

CI trigger attempts on the only assigned mailbox branch:

1. Pre-fix request commit `d9f2899e0e8cae007e895149c6646ee503cbe905`, request id `agent5-20260826-132038-red`.
2. Pre-fix reissue `a1a07c62afc1833651e295903a7e4a81af3cbc0e`, request id `agent5-20260826-132038-red-r2`.
3. Post-fix request commit `ac97240f8a93db916ed601a7b31875d41519605b`, request id `agent5-20260826-132038-green-r1`.
4. Post-fix request built as a Git tree/commit and published by explicit ref move: `aca945d6df49bdff73a398fe415eeb7c5eff4b8c`, request id `agent5-20260826-132038-green-r2-refmove`.

For each request, GitHub reports zero workflow runs for the exact request SHA and no individual commit statuses. The combined status endpoint returns its empty-status sentinel (`total_count=0`), not an active `ci/single-test` run. Listing runs for `ci-test/fixes/agent-5` still returns only the older August 25 runs; the latest retained successful run is `32896511398` for the previous tree-batching task.

The repository workflow itself is present and correct on the request source: `.github/workflows/tests-single.yml` listens to pushes on `ci-test/**` when `.github/test-request.json` changes. The connected GitHub surface can write files, create commits/trees, and move refs, but exposes no workflow-dispatch/run action; both content-write and explicit-ref-move publication failed to generate the push workflow event.

## Result

**Blocked before required CI/replay verification.** No red or green Unity execution is claimed. The candidate shader source and focused regression are pushed on `fixes/agent-5`, but the repository-mandated `ci/single-test` result cannot be obtained from this connected session because CI request publication is not creating Actions runs.

## Consequence

Keep `SceneIssues/open/20260826-132038-408-VoxelShowcase` open. Do not set `status=fixed`, do not populate `resolvedUtc`/`resolutionSummary`/`regressionTest`/`fixCommit`, do not move the capture to `SceneIssues/closed/`, and do not integrate the candidate into `master` until the exact focused test has executed successfully and the saved capture has been replay-verified.

## Next

The local coordinator/terminal path should reissue the same focused test from source commit `506d4b37a42639bb1b9d48f1796e7794446d3c40` on `ci-test/fixes/agent-5`. After a green exactly-one-test result, replay the existing capture at its saved camera pose, preserve the visual evidence, and only then perform the separate terminal bookkeeping/close commit.
