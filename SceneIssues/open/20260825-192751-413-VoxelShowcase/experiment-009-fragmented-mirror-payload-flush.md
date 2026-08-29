# Experiment 009 — fragmented persistent-mirror payload flush

## Exact runtime evidence
- Exact feature request `5d78ad005c0873f6155b0171f124959c1d8d7454` targeted feature SHA `4722b74771ab2a265157d800bdf9500f7ffcb9fe` in run `33275543571` / job `99161482259`.
- `GpuSurfaceMirrorRecoveryLivenessTests.DemandRecoveryCannotBeStarvedByCoveredGpuWork` passed.
- `ShowcaseGpuMigrationTests.MovingShowcaseCompletesGpuSurfaceBuildsAndPreservesCoverage` failed after 78.6 s: `visible=43`, `missing=579`, `dirty=1927`, `jobs=12`, `uploads=0`, `gpu=154/0` after its 20 s settle allowance.
- `gpu=154/0` proves experiment 008's empty-stage completion change removed eligible CPU fallback. It is therefore retained, but falsified as the complete scene fix.
- The exact built player exited normally after 45 s. The four captured frames were inspected: t15.4 is almost empty; t25.4/t35.4 recover the castle/town; the final capture still has incomplete right-side/world coverage.
- Player telemetry starts near 245–264 FPS, then collapses late to roughly 5–18 FPS. Individual solid-worker `Prepare` and `admissionFrame solid` repeatedly reach about 190–195 ms while arena `leaseFail=0` and relief remain zero.

## Competing hypotheses
1. **Eligible CPU fallback causes the late stalls** — falsified by the exact migration result `gpu=154/0` while the scene still fails coverage/performance.
2. **Surface geometry arena exhaustion/relief causes the late stalls** — falsified by `leaseFail=0`, substantial unused arena capacity, and negligible relief timing.
3. **Storage/residency scans or worker selection dominate `Prepare`** — disfavored by scheduler section telemetry: rule sync, residency, capacity, selection, snapshot, compact/upload remain small while whole individual `worker.Prepare` spikes to ~195 ms.
4. **Fragmented persistent-mirror payload flush causes synchronous Metal driver overhead** — supported by source plus timing. Recovery stages up to 64 mixed blocks into arbitrary slots. `GpuBrickSlotTable` reuses a LIFO free-slot stack, so those slots need not be contiguous. The first extraction after recovery calls `GpuVoxelBrickMirror.FlushPendingUploads`; the old `FlushPayloadSlots` emitted four `ComputeBuffer.SetData` calls for every contiguous dirty-slot run. A maximally fragmented 64-brick slice therefore permits 256 synchronous buffer uploads inside one otherwise uninstrumented worker `Prepare`, matching the observed timing shape.

## Fix / discriminator
- `GpuVoxelBrickMirror` now compacts dirty slot payloads into fixed batches of 64 records. Each record carries the destination slot plus the exact material, surface, boundary and 16-byte metadata payload.
- `VoxelBrickDirectoryUpdater.compute` adds `CSApplyPayloadDeltas`, which scatters that one contiguous CPU upload into the unchanged live slot buffers before directory deltas and extraction dispatches consume them.
- This mirrors the already-existing compact directory-delta design. Slot allocation/reuse, persistent directory encoding, Storage versions, recovery admission/fairness, extraction shaders and geometry publication remain unchanged.

## Blast radius / cost
- Changed path is only persistent GPU mixed-brick publication. CPU topology, HLOD, water, world truth, surface catalogues, chunk scheduling and arena sizing are untouched.
- Fixed staging: 64 × 517 uints = 132,352 GPU bytes plus the same-size persistent CPU staging array. This is independent of mirror slot capacity.
- Each full batch performs one ~132 KB `SetData` plus one ~32.8k-thread scatter dispatch, replacing up to 256 fragmented `SetData` calls. Live payload size and destination offsets are unchanged.
- The existing exact migration test is the behavioral regression: it requires scene traversal to preserve coverage, settle, keep eligible CPU fallback/blocking completion at zero, and meet the existing moving/stationary frame-time gates. The built-player replay independently checks the original marked FPS/slow-fill symptom.
