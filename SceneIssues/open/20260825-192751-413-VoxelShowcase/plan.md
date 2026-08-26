# Plan — SceneIssue 20260825-192751-413 VoxelShowcase performance/coverage

## Defect / acceptance
The capture reports sub-100 FPS walking, slow fill, and missing geometry until meshing completes. The existing GPU Transvoxel path had been globally disabled. Acceptance requires the saved pose and moving traversal to retain coverage, focused regression CI to be green, and measured performance to remain materially improved without bypassing renderer architecture.

## Proven results
- Restored existing GPU exact-ring extraction for steps 1/2 only; step 4/HLOD and hardware/content fallbacks remain CPU/coarse.
- Exact cutover regression green: request `8bb029535005fbb9fde1365e39c7b41461ecc407`, run `32991459621`.
- Saved-pose replay green: `c7bc806567c007f3cbc0310942a8a799ad88627a`, run `32991641843`; no missing geometry after convergence, late-window average about 168 FPS versus captured ~105 FPS.
- Production moving traversal `c59ca85fc9e81b09577b5a6f6c3d143d42438446`, run `32993732467`, reached a valid visible gate but lost all voxel draws on movement frame 5.
- Steady-throughput test `e540607dacc49c840fe230878c5cab87172c1de6`, run `33006475923`, failed before timing because background work had not converged (`dirty=1407`, `jobs=2`).
- Experiment 010 diagnostic request `02bf75c3dc7a48cc331196ed91d4af3aecdf4c82`, run `33016648361`, was inconclusive: setup ended at `candidates=123/31/0` and never reached a visible frame.

## Competing hypotheses
1. Moving clipmap/visibility routing drops candidates or ownership.
2. Candidates remain routed, but independent LOD publication/readiness temporarily leaves no drawable representation.
Existing clipmap-motion tests already cover basic resident re-admission, so no blanket rediscovery change is justified.

## Current discriminator
Experiment 011 pins the exact saved SceneIssue camera pose before warmup, eliminating arbitrary initial-view state while preserving the first 20 movement frames. It reports camera/far-hole, known→in-band→frustum, and step-4 readiness at the first zero draw. Production is unchanged for this experiment.

## Remaining gates
- [ ] Run experiment 011 exact-SHA targeted CI and classify the first movement failure.
- [ ] Implement only the smallest causal production fix plus focused behavioral regression.
- [ ] Re-run moving traversal, convergence/throughput, and every original saved pose; record actual timings.
- [ ] Commit `verification-final.png` and worker-side terminal bookkeeping: move this capture `open/`→`pending/`, set `status: pending`, `resolutionSummary`, `regressionTest`, `fixCommit`; leave `resolvedUtc` empty.
- [ ] Push verified branch and wait for the coordinator. Do not push master, create review branch, or start another capture.

Current branch is based on master `025e88ef6e2d097143607c3018184ddc99cb747c`; production fix lineage remains rooted at `0fcaf3b98b92f4906c2027dd0b9104d664e01f90`.