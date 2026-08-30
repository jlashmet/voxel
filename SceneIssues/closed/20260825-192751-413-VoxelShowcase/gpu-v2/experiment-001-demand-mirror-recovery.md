# Experiment 001 — demand-driven GPU mirror recovery

## Question
Does the shared mirror stall because GPU admission performs broad resident-world recovery, or because the compute/readback path itself is slow?

## Prediction / falsifier
If broad recovery is the cause, an exact built-player replay should spend its long frames in solid worker/admission before any GPU completion, with arena upload/readback and water unable to account for the plateau. Replacing the resident-world sweep with bounded recovery for only requested GPU footprints should remove that admission plateau and allow near-ring GPU builds to complete. If long frames remain in GPU count/write/readback after this change, the hypothesis is false.

## Before evidence
Exact feature source `db1230ba572b729dc64c7ae627f2caefc7afc957`, workflow run `33226493129`.

- Official targeted test did not start because the runner detected a user Unity editor already open; this is infrastructure evidence only.
- The workflow's always-run real OSX player did build and replay `Assets/Scenes/VoxelShowcase.unity`.
- After ~20 s: ~`1.3–1.5 FPS`, `visible=4`, `missing=757`, step-1/2 resident chunks `0`.
- Solid admission/worker timing stabilized around `0.65–0.77 s/frame`; water admission was ~`0.05 ms` and geometry arena upload ~zero.
- Coordinator source recovered up to `2048` logical blocks from a queue populated by resident-world scanning on the same admission path.

## Change under test
`GpuSurfaceMirrorCoordinator` now queues recovery only from `Covers(...)` for regions actually required by eligible GPU chunk footprints, processes at most `64` logical blocks per frame, classifies empty/uniform blocks from `RegionReadView`, and pins/copies payload only for mixed blocks. Changes during partial recovery restart that region.

## Result
Pending final exact-SHA targeted CI and built-player replay.
