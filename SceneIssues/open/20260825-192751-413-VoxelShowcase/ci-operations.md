# CI operations — SceneIssue 20260825-192751-413

## 2026-08-28 — prior final request inspection
- CI ref: `ci-test/fixes/agent-2`
- Request SHA: `618b57e85c8a3be462469c7108cc24bb917c019f`
- Feature parent: `db1230ba572b729dc64c7ae627f2caefc7afc957`
- Run: `33226493129`
- Official result: `ci/single-test=failure` due runner infrastructure. Job `99031154261` stopped before the requested PlayMode test because an interactive Unity editor for `/Users/jlashmet/code/voxel` was already open (pid 46412 plus import worker).
- No no-op/replacement CI request was created from that failure.
- The always-run real-player build/capture succeeded and supplied product evidence used by `experiment-001-demand-mirror-recovery.md`: near-ring geometry remained missing and solid admission consumed ~0.65–0.77 s/frame.

## 2026-08-28 — exact request `f7258c53…`
- CI ref: `ci-test/fixes/agent-2`
- Request SHA: `f7258c53e97bc40c30e1db4f4de64f4b4769e130`
- Exact feature parent: `f6754abfffd249737cd2bb07977a6f92cc529720`
- Run/job: `33231204833` / `99044253782`
- Product result: failed. `ShowcaseGpuMigrationTests.MovingShowcaseCompletesGpuSurfaceBuildsAndPreservesCoverage` reached traversal frame 134 with zero visible voxel draws, `gpuCompleted=0`, `gpuFallback=0`, and `gpuWaitSlices=2118`.
- Exact built-player replay independently reproduced the liveness defect: after startup it remained around `visible=5 / missing=768` through the 45 s capture. Solid admission was only ~`0.13–0.33 ms`, proving the prior whole-world recovery stall was removed and isolating block-recovery liveness as the next product defect.
- Run artifact id: `9708773306`.

## 2026-08-28 — exact request `87671c08…`
- CI ref: `ci-test/fixes/agent-2`
- Request SHA: `87671c08698b67c3c86c523748a3ffbd8200f789`
- Exact feature parent: `c3d06ab0421535e4418f12e7f7ebb5ee09467d64`
- Run/job: `33232803150` / `99048449957`
- The workflow eventually concluded `cancelled` at its timeout envelope, but this is a **product-failed iteration**, not an infrastructure retry: the requested PlayMode regression itself failed before cancellation.
- Traversal failure: frame 119, camera `(71.07,24.55,-15.20)`, zero visible voxel draws, `gpuCompleted=3`, `gpuFallback=0`, `gpuWaitSlices=1611`, `dirty=2050`.
- Exact built-player capture completed successfully. Runtime improved to ~194–200 FPS with solid admission settling around ~2–4 ms, but coverage plateaued at `27 drawn / 743 missing` from roughly t28 through t44.
- Artifact id: `9709278530`. All three replay screenshots and `verification-final.png` were inspected; the final state still has large missing near/mid voxel surfaces and disconnected fragments while distant terrain/vegetation render.
- This request is completed and is not being replaced while queued. The same CI mailbox may be reused after the next feature fix is final.

## 2026-08-29 — recovery-fairness request `70b1e683…`
- CI ref: `ci-test/fixes/agent-2`
- Request SHA: `70b1e6832393a6276412af3add2b9f46f772e12c`
- Exact feature parent: `3605acec8cccaa1394cde27bb47d1397f48b7f9e`
- Run: `33239649762`
- Official result: `ci/single-test=failure` before product execution. The new liveness regression did not compile because `VoxelSurfaceMetrics` was referenced without importing `VoxelEngine.Rendering.Runtime.SurfaceExtraction`; the same compiler error prevented the real-player build.
- This is a product/test-source failure, not runner infrastructure. Feature commit `7015d7f2b16434a6d1115665553b1c03473e431f` adds the missing namespace import without changing production behavior or test thresholds.

## 2026-08-29 — exact recovery-fairness request `32deb42f…`
- CI ref: `ci-test/fixes/agent-2`
- Request SHA: `32deb42f84b421f0c7a88adb8530eb3f3c49dfba`
- Exact feature parent: `f86d1eb46c4bd1fb3561af175ea1e18c4c5ee65d`
- Run/job: `33240075886` / `99067783307`
- Official result: `ci/single-test=failure`; this is a product-failed iteration, not an infrastructure retry.
- Focused test result: `GpuSurfaceMirrorRecoveryLivenessTests.DemandRecoveryCannotBeStarvedByCoveredGpuWork` failed before its liveness assertions because direct `Camera.Render()` produced render-pass validation error `Attachment resolutions must match: (640x480) vs (64x64)`. The next regression revision gives this test its own 320x180 target, matching the established traversal harness.
- Production traversal result: `ShowcaseGpuMigrationTests.MovingShowcaseCompletesGpuSurfaceBuildsAndPreservesCoverage` failed at frame 146 / camera `(70.94,24.55,-15.22)` after losing every visible voxel draw. Runtime at failure: `known=5966`, `resident=47`, `dirty=2068`, `missing=718`, `jobs=12`, `gpuBackends=12`, `gpuCompleted=8`, `gpuFallback=0`, `gpuWaitSlices=1898`.
- The fairness change therefore improved forward progress from the prior three GPU completions to eight, but it was insufficient to restore liveness.
- Exact built-player capture completed successfully. After initial startup, the replay plateaued at `23 drawn / 747 missing` from roughly 20 s through 45 s while FPS stayed around `205–244`, solid admission remained around `1.6–1.9 ms`, and the geometry arena reported `leaseFail=0` with substantial unused capacity.
- Artifact id: `9711149960`. All four timed screenshots (`t14.3`, `t24.3`, `t34.3`, `t44.3`) and `verification-final.png` were inspected. The first shows an almost empty near world with sparse isolated structures; from t24.3 onward the same large green near/mid-field holes, disconnected/floating structures, and sparse voxel geometry remain visually frozen through the final evidence.
- This completed red request is not queued and will not be replaced. The assigned CI ref may be advanced once, only after experiment 005 metadata and the next feature head are final.

## Next final request
Pending one fresh exact-head request after the optional-nonresident-halo fix and behavioral-regression metadata are complete. Reuse only `ci-test/fixes/agent-2`, parent the request commit directly on the final feature SHA, and do not modify `.github/test-request.json` on `fixes/agent-2`.
