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
- GitHub workflow conclusion was `cancelled`, but uploaded XML contains completed requested tests and therefore supplies product evidence; this is not treated as an infrastructure retry.
- Recovery liveness passed. Migration failed after 78.6 s because coverage did not settle within 20 s: `visible=43`, `missing=579`, `dirty=1927`, `jobs=12`, `uploads=0`, `gpu=154/0`.
- Exact player exited normally after 45 s. All four screenshots were inspected: t15.4 nearly empty, t25.4/t35.4 substantially recovered, final still incomplete. Runtime starts around 245–264 FPS, then late solid admission / individual worker `Prepare` repeatedly reaches ~190–195 ms and FPS falls to roughly 5–18. Arena `leaseFail=0`.
- Source inspection isolated fragmented persistent-mirror payload uploads as the next discriminator; experiment 009 replaces fragmentation-dependent `SetData` calls with one compact payload upload plus GPU scatter per 64-brick batch.

## 2026-08-29 — final fragmented-payload request `0f7c958f…`
- CI ref: `ci-test/fixes/agent-2`.
- Request SHA: `0f7c958fab59ebf497bd3a80edd041970dd9cdd4`.
- Exact feature parent: `33ae17d7f5df8a572ebd7edc9bee8e689adc3876`.
- Run/job: `33277135240` / `99165718210`; artifact `9721973168`; official workflow conclusion `cancelled` with requested-test step `failure`.
- Product/test-source red: uploaded XML shows both requested PlayMode tests failed on Metal shader compilation. `VoxelBrickDirectoryUpdater.compute(62)` reports `syntax error: unexpected token 'linear'` in `CSApplyPayloadDeltas`/shader compilation. Liveness duration 45.325 s; migration duration 4.382 s.
- The exact built-player harness still built/launched `VoxelShowcase` and completed 45.1 s with zero harness assertions, but this cannot satisfy the failed targeted gate. Player telemetry also still exhibits late admission spikes around 80–195 ms and roughly 6–17 FPS, so there is no positive performance verdict for the compaction.
- Feature correction `18d72133342daa56ecfaa3c6d1f09e4a194cf205` renames only the reserved HLSL identifier `linear` to `linearIndex`.
- Per assignment, no extra CI transport was created after the final request. Therefore the corrected head has no green exact-SHA targeted CI and the capture must remain under `open/`; pending/closed metadata and master promotion are prohibited.

## 2026-08-29 — corrected payload-scatter request `562616c0…`
- Exact feature parent `0a1ee326877320dbd236dcdf9e2acf6fcd7d7ceb`; run/job `33279094247` / `99171054270`; artifact `9722639694`.
- Product red. Recovery liveness passed in 52.35 s. Migration failed after 44.55 s because traversal frame 3 lost every visible voxel draw: `visible=0`, `missing=64`, `gpuCompleted=8`, `gpuFallback=0`, `gpuWaitSlices=248`.
- Built player launched and ran to 51.4 s. All four screenshots were inspected: t15.4 is almost empty; the castle largely appears by t25.4; t35.4/t51.4 add distant structures but remain incomplete. Telemetry retains recurring ~191–198 ms solid-admission spikes and ends at 351 visible / 344 missing with `leaseFail=0`.
- The corrected shader compiles, so the prior Metal syntax failure is closed. Compact payload scatter is falsified as the complete performance fix. The capture remains open.

## 2026-08-29 — shared-mirror takeover request `0890971c…`
- Exact feature parent `4754860892cf3ddd81a7d2fc8130d02fc58355f3`; run/job `33281099872` / `99176301088`; artifact `9723212603`.
- Product red. Strengthened recovery liveness passed in 52.21 s. Migration failed only the unchanged moving-tail gate: p99 75.912 ms versus 25 ms; the preceding adoption, visibility, zero-fallback, and snapshotless-stage assertions passed.
- The 45 s real-player harness passed and all four images were inspected. At 15.7 s geometry was nearly absent; the castle was substantially present by 25.7 s. Runtime ultimately reached `missingVisible=0`, no arena allocation failure, and roughly 200–500 FPS.
- Exact telemetry separates the remaining tail from shared-mirror solid admission: one water admission slice was 29.256 ms while solid admission was 0.478 ms; later solid admission remained about 0.5–3 ms. Experiment 010 bounds discovery-only water classification by its existing deadline. The capture remains open.
