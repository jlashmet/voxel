# Experiment 009 — fragmented persistent-mirror payload flush

## Exact runtime evidence
- Baseline exact request `5d78ad005c0873f6155b0171f124959c1d8d7454` targeted feature `4722b74771ab2a265157d800bdf9500f7ffcb9fe` in run/job `33275543571` / `99161482259`.
- Recovery liveness passed. Migration failed after 78.6 s with `visible=43`, `missing=579`, `dirty=1927`, `jobs=12`, `gpu=154/0` after its 20 s settle allowance.
- `gpu=154/0` proves experiment 008 removed eligible CPU fallback, but falsifies it as the complete scene fix.
- The exact built player exited normally after 45 s. All four captures were inspected: t15.4 is almost empty; t25.4/t35.4 recover the castle/town; final remains incomplete. FPS later collapses to roughly 5–18 while solid admission / individual worker `Prepare` reaches ~190–195 ms. Arena `leaseFail=0`.

## Competing hypotheses
1. **Eligible CPU fallback causes the late stalls** — falsified by `gpu=154/0` while coverage/performance still fail.
2. **Surface arena exhaustion/relief causes the late stalls** — falsified by `leaseFail=0`, unused capacity, and negligible relief.
3. **Instrumented Storage/residency/selection sections dominate `Prepare`** — disfavored because those sections stay small during ~195 ms whole-worker spikes.
4. **Fragmented persistent-mirror payload flush causes synchronous Metal upload overhead** — supported by source shape: a 64-block recovery can occupy arbitrary LIFO-reused slots, and the old path issued four `ComputeBuffer.SetData` calls per contiguous dirty-slot run, allowing up to 256 uploads in one worker `Prepare`.

## Fix / discriminator
- `GpuVoxelBrickMirror` compacts dirty payload slots into fixed batches of 64 records and uploads each batch contiguously.
- `VoxelBrickDirectoryUpdater.compute` scatters the compact records into the unchanged live material/surface/boundary/metadata slot buffers before directory deltas and extraction consume them.
- Slot allocation, persistent directory encoding, Storage versions, recovery admission/fairness, extraction shaders, geometry publication and existing performance thresholds are unchanged.

## Final targeted-CI result
- Request `0f7c958fab59ebf497bd3a80edd041970dd9cdd4`, direct parent feature `33ae17d7f5df8a572ebd7edc9bee8e689adc3876`; run/job `33277135240` / `99165718210`; artifact `9721973168`.
- Product red before the discriminator could run: Metal rejected `VoxelBrickDirectoryUpdater.compute` at line 62 because the new local identifier `linear` is an HLSL interpolation keyword. Liveness failed at 45.3 s and migration at 4.4 s on that shader compile error.
- The built-player harness still launched and exited normally, but it cannot satisfy the failed regression gate. Its telemetry continued to show late ~80–195 ms admission stalls and ~6–17 FPS, so there is no evidence that the compaction fixed the marked symptom.
- Correction commit `18d72133342daa56ecfaa3c6d1f09e4a194cf205` renames only `linear` to `linearIndex`. This is a syntax correction to the same experiment, not a new performance hypothesis.

## Blast radius / cost
- Changed path is only persistent GPU mixed-brick publication. CPU topology, HLOD, water, world truth, surface catalogues, chunk scheduling and arena sizing are untouched.
- Fixed staging: 64 × 517 uints = 132,352 GPU bytes plus same-size persistent CPU staging. A full batch is one ~132 KB upload plus one ~32.8k-thread scatter dispatch, replacing up to 256 fragmented `SetData` calls.
- The existing migration test remains the behavioral regression. Because the sole final request is red and the assignment forbids extra CI transports, this experiment is not promotable; the SceneIssue remains open.
