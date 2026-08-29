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

## Final request
Pending one fresh exact-head request after the compile fix and this CI record. Reuse only `ci-test/fixes/agent-2`, parent the request commit directly on the final feature SHA, and do not modify `.github/test-request.json` on `fixes/agent-2`.
