# CI operations — SceneIssue 20260825-192751-413

## 2026-08-28 — prior final request inspection
- CI ref: `ci-test/fixes/agent-2`
- Request SHA: `618b57e85c8a3be462469c7108cc24bb917c019f`
- Feature parent: `db1230ba572b729dc64c7ae627f2caefc7afc957`
- Run: `33226493129`
- Official result: `ci/single-test=failure` due runner infrastructure. Job `99031154261` stopped before the requested PlayMode test because an interactive Unity editor for `/Users/jlashmet/code/voxel` was already open.
- No replacement request was created from that failure. The real-player capture still supplied product evidence: near-ring geometry remained missing and solid admission consumed ~0.65–0.77 s/frame.

## 2026-08-28 — exact request `f7258c53…`
- Feature parent: `f6754abfffd249737cd2bb07977a6f92cc529720`; run/job `33231204833` / `99044253782`; artifact `9708773306`.
- Product red: migration reached zero visible voxel draws with `gpuCompleted=0`, `gpuFallback=0`, `gpuWaitSlices=2118`; player remained around `5 visible / 768 missing` through 45 s.
- Solid admission had fallen to ~0.13–0.33 ms, isolating block-recovery liveness after the whole-world recovery stall was removed.

## 2026-08-28 — exact request `87671c08…`
- Feature parent: `c3d06ab0421535e4418f12e7f7ebb5ee09467d64`; run/job `33232803150` / `99048449957`; artifact `9709278530`.
- Product red despite eventual workflow cancellation: migration failed with `gpuCompleted=3`, `gpuFallback=0`, `gpuWaitSlices=1611`, `dirty=2050`.
- Player held ~194–200 FPS but plateaued near `27 drawn / 743 missing`; all timed/final screenshots were inspected and showed persistent near/mid holes.

## 2026-08-29 — recovery-fairness request `70b1e683…`
- Feature parent: `3605acec8cccaa1394cde27bb47d1397f48b7f9e`; run `33239649762`.
- Product/test-source red before execution: the new liveness regression missed a namespace import. Feature `7015d7f2b16434a6d1115665553b1c03473e431f` corrected only that compile error.

## 2026-08-29 — exact recovery-fairness request `32deb42f…`
- Feature parent: `f86d1eb46c4bd1fb3561af175ea1e18c4c5ee65d`; run/job `33240075886` / `99067783307`; artifact `9711149960`.
- Product red. Focused liveness hit a render-target-size validation error; migration failed with `gpuCompleted=8`, `gpuFallback=0`, `missing=718`.
- Player plateaued at `23 drawn / 747 missing`, ~205–244 FPS, `leaseFail=0`. Screenshots showed frozen large green holes. Test harness target sizing was fixed separately.

## 2026-08-29 — exact optional-halo request `22f5ea96…`
- Feature parent: `7c6beeb95a5d8727716d6f922628dabf2acb8abd`; run/job `33241309873` / `99071018106`; artifact `9711598132`.
- Product red. Focused warmup was measuring variable castle-generation startup; migration failed with `gpuCompleted=8`, `gpuFallback=1`, `missing=724`.
- Player was converging rather than permanently deadlocked: late capture about `348 drawn / 351 missing`, but still far too slow. This led to demand coalescing and bounded descriptor recovery.

## 2026-08-29 — exact empty-stage request `088fb880…`
- CI ref: `ci-test/fixes/agent-2`; feature parent `b650932f7b35323948d75b92bc65a1a34c6ec194`; run `33273977297`; artifact `9720987258`.
- Product red. Recovery liveness passed; migration completed 89 GPU builds but recorded 3 CPU fallbacks among 92 GPU-eligible attempts.
- Built player reached 45 s. Evidence narrowed the remaining fallback path to successful empty GPU counts being sent through unnecessary completion work.

## 2026-08-29 — exact zero-fallback request `5d78ad00…`
- CI ref: `ci-test/fixes/agent-2`.
- Request SHA: `5d78ad005c0873f6155b0171f124959c1d8d7454`.
- Exact feature parent: `4722b74771ab2a265157d800bdf9500f7ffcb9fe`.
- Run/job: `33275543571` / `99161482259`; artifact `9721533195`.
- GitHub's workflow conclusion was `cancelled`, but the uploaded XML contains completed requested tests and therefore supplies product evidence. This is not treated as an infrastructure retry.
- Focused recovery liveness passed.
- Migration failed after 78.6 s because coverage did not settle within 20 s: `visible=43`, `missing=579`, `dirty=1927`, `jobs=12`, `uploads=0`, `gpu=154/0`.
- `gpu=154/0` proves the empty-stage fix eliminated eligible CPU fallback; that narrow fix is retained.
- The exact player exited normally after 45 s. All four screenshots were inspected: t15.4 nearly empty, t25.4/t35.4 substantially recovered, final still incomplete on the right/world extent.
- Runtime starts around 245–264 FPS, then late solid admission / individual solid-worker `Prepare` repeatedly reaches ~190–195 ms and FPS falls to roughly 5–18. Arena `leaseFail=0`, unused capacity remains, and relief is negligible.
- Source inspection identified an uninstrumented persistent-mirror transfer hotspot: mixed recovery may stage 64 arbitrarily fragmented LIFO-reused slots, while the old payload flush performs four synchronous `ComputeBuffer.SetData` calls per contiguous run. Experiment 009 replaces that fragmentation-dependent call count with one compact payload upload plus GPU scatter per 64-brick batch.

## Current final-validation candidate
- Fragmented payload scatter source: `Assets/VoxelEngine/Rendering/Runtime/GpuVoxel/GpuVoxelBrickMirror.cs` and `Assets/VoxelEngine/Rendering/Resources/VoxelBrickDirectoryUpdater.compute`.
- Experiment evidence: `experiment-009-fragmented-mirror-payload-flush.md`.
- Feature synchronized with current `origin/master` (`922565bedd104c1795a9d13c610d4d185b65754e`) through merge `b38e8d68840372243219f17c89a1c0ccfa8398fe`; master changes touched only the separate reopened town-architecture assignment, so no agent-2 path conflict existed.
- Next: update this metadata with the exact final feature head, then issue one targeted request on `ci-test/fixes/agent-2` parented directly on that head. Do not modify feature `.github/test-request.json` and do not replace the request while queued/running.
