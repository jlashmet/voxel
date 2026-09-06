# GPU resumption — launch regression and retained evidence

## Scope

G01–G04 / G19 continuation. This records restoration and first full-scene GPU evidence, not completion of GPU correctness, CPU-backend deletion, visual acceptance, or the 1,000 FPS objective.

## Proven launch divergence and repair

The previous feature forced `CpuTransvoxelChunkCache.GpuCutoverDisabled = true`; restoring the environment-based production policy removed that diagnostic CPU gate. The scenario runner also ignored `gpuCutover: required` while module validation inherited `VOXEL_DISABLE_GPU_CUTOVER=1`; the restored declarative child policy clears that override only for GPU-required scenarios.

Local tooling regressions: the GPU player-policy suite has five passes after four intended before-fix failures; the frame-timing capture suite has five passes after three intended missing-build-flag failures. These are launch/tooling proofs only, not Unity rendering evidence.

## First exact GPU-enabled production baseline

Feature source **`9684ff509d65ab7a1caca6245d0f0093f28e249d`**; request **`fb4a7a92de3420c0affa2a5463287d0252f67797`**; run **`34007154618`**; job `101416373122`: **completed success** without replacement. Artifact **`9982119472`**, SHA-256 **`f4fd1e71e4cd76676f4cb88ef39801f55c73c42006543e4b086a4ace4f9eb78c`**. Repository-derived module validation and the 65-second standalone VoxelShowcase replay both passed.

Rendering-owned GPU fixtures are real successes:
- `SolidGpuMinimalValidation`: `gpuAvailable=True backends=1 pub=1 fallback=0 unsupported=0 contextFail=0 arenaFull=0 countFail=0 writeFail=0 blocking=0 visible=1 missing=0`, with the expected 41 solids / 114 exposed faces / 456 vertices / 684 indices visibly rendered.
- `SolidGpuProductionValidation`: initial/traversal/edit/restart all converge with `fallback=0`, no unsupported/context/count/write/blocking failures. Settled evidence reported `frameP95Ms=5.983`, `frameP99Ms=7.023`, prepare p95 `0.083 ms`, submission p95 `0.008 ms`. Its exact scene is a bounded correctness fixture, not the 1,000 FPS target workload.

The **full VoxelShowcase is genuinely exercising GPU extraction but fails visual acceptance badly**. At the end of the replay it reports approximately `gpu[req=731 ... pub=730 ... unsupported=0 stale=0 retry=0]`, `missingVisible=0`, `visible=600`, no fallback/error counters, yet the rendered image contains large missing/flat regions and fragmented floating structural surfaces. Because the defect remains after visible coverage converges to zero missing chunks, it is not acceptable as a startup-only hole. This falsifies “GPU is universally broken” while demonstrating that simple-fixture success does not predict full-scene correctness.

The late 1600x900 windows reached roughly **194–199 rendered FPS**, p50 about **5.0–5.1 ms**, p95 about **5.3–5.5 ms**, with full-scene coverage converged. This is an initial diagnostic baseline only: the locked benchmark is 1920x1080 and repeated workloads, and this exact source still logs zero `FRAMEPIPE` CPU/main/render/present/GPU samples because it predates the descendant frame-timing build fix. Do not interpret unavailable GPU timing as zero cost.

Steps 1/2 are the GPU-enabled near rings in this source. Step 4 and step 8 still have CPU rendering dependencies, and water still has a CPU surface cache; their migration/deletion remains required.

## P0 publication transaction discriminator

External review findings 1–4 match current production contracts closely enough to require direct proof. Immutable fail-before feature **`51a9f344ec62f583f66a6c1acb3a801efdbf0bae`** adds a real compute page-arena test requiring a successful GPU candidate to stay pending after write/finalization until explicit CPU approval. Companion cases distinguish `Ready`, `Exhausted`, `Stale`, and `TooLarge` without generated-geometry readback.

Exact fail-before request **`c1f72490ea9434a1a9069d4afc23b688a885e5e8`**, run **`34010085590`**, is queued at the latest observation. Preserve it until terminal. The expected defect is the old `PublishBatch` swapping pending geometry live before `FinishPagedGpuBuild` performs its renderer slot/version validation.

Descendant work introduces an allocation-free typed outcome parser, retains submitted lanes until a tiny asynchronous status/identity readback completes, and adds explicit pending `Commit` / `Abort` page-arena kernels. This descendant is not pass-after evidence until the CPU approval hook is wired and exact-SHA CI passes.

## Frame timing follow-up

Descendant `tools/showcase-player-capture.sh` requests the existing `-voxelFrameTimingStats` builder flag for ordinary, traversal, and SceneIssue captures. `ShowcasePlayerBuild` restores `PlayerSettings.enableFrameTimingStats` in `finally`; no persistent project setting is changed. Exact runtime proof of available Metal frame timing is still required on a post-baseline source.
