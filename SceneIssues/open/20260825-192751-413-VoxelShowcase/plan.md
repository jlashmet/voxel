# Plan — SceneIssue 20260825-192751-413 VoxelShowcase performance/coverage

## Defect / acceptance
The capture reports sub-100 FPS walking, slow fill, and missing geometry until meshing completes. Acceptance requires every saved pose plus moving traversal to retain coverage, focused behavioral CI to be green, and measured performance to improve without bypassing renderer architecture.

## Proven results
- Restored the existing validated GPU exact-ring extractor for steps 1/2; step 4/HLOD and hardware/content fallbacks remain CPU/coarse.
- Exact cutover regression green: request `8bb029535005fbb9fde1365e39c7b41461ecc407`, run `32991459621`.
- Saved-pose replay green: `c7bc806567c007f3cbc0310942a8a799ad88627a`, run `32991641843`; converged with no missing geometry, late-window average about 168 FPS versus captured ~105 FPS.
- Production moving traversal `c59ca85fc9e81b09577b5a6f6c3d143d42438446`, run `32993732467`, reached visible coverage then lost all voxel draws on movement frame 5.
- Throughput test `e540607dacc49c840fe230878c5cab87172c1de6`, run `33006475923`, failed before timing because background work had not converged.
- Experiment 010 was inconclusive: setup ended at `123/31/0` known/in-band/frustum candidates.
- Experiment 011 exact saved pose also failed before movement: `144/36/0`; therefore no movement/scheduler fix is yet proven. Scene YAML confirms `VoxelShowcase`, `Camera`, and captured `Showcase Camera` are the same GameObject.

## Competing hypotheses
1. The diagnostic render path constructs an invalid/stale camera frustum at the saved pose.
2. Camera frustum math is correct, but discovery/ring ownership supplies in-band chunks outside the captured view.
3. Only after initial visibility is proven: movement can expose an independent LOD publication/readiness gap.

## Current discriminator
Experiment 012 leaves production unchanged. At the exact saved pose it checks whether Unity's calculated frustum accepts a small AABB directly in front of the camera while production visibility remains zero; failure also reports `DescribeRings()`.

## Remaining gates
- [ ] Run experiment 012 exact-SHA CI and classify zero-frustum state.
- [ ] Implement only the smallest causal production fix plus focused behavioral regression.
- [ ] Re-run moving traversal, throughput/convergence, and every original pose; record actual timings.
- [ ] Commit `verification-final.png`; move capture `open/`→`pending/`, set pending bookkeeping with `resolvedUtc` empty.
- [ ] Push verified branch and wait for coordinator; do not push master, create review branch, or start another capture.

Current master ancestor: `025e88ef6e2d097143607c3018184ddc99cb747c`. Production fix lineage remains rooted at `0fcaf3b98b92f4906c2027dd0b9104d664e01f90`.
