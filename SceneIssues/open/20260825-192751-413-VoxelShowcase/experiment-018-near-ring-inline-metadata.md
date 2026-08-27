# Experiment 018 — inline near-ring exact metadata

## Hypothesis
The measured `Voxel.Surface.Snapshot` spikes are amplified by Unity job-scheduling/dependency fan-out for the two small exact near-ring metadata grids (step 1 = 10^3 entries; step 2 = 18^3), not by the metadata mapping work itself. Running those same clear/map/compact job bodies inline should remove the scheduler burst while preserving the authoritative snapshot and existing GPU/CPU routing.

## Competing hypothesis
Mixed-payload pinning/classification, coarse-ring snapshot work, GPU count/write, or unrelated player-loop work dominates; eliminating only near-ring metadata scheduling will not materially improve the unchanged traversal gate.

## Change / correctness boundary
`NearRingExactSnapshotScheduling` provides specific scheduling overloads for the existing exact metadata jobs. Metadata grids at or below 6000 entries execute the unchanged job `Execute` bodies directly; larger grids retain Unity's existing asynchronous Burst scheduling. Therefore step 4 (34^3) and step 8 (66^3) cannot regress into the historical large main-thread snapshot scan.

The change does not alter Storage pin/version checks, mixed-payload COW ownership, classification, surface/coating/profile rules, GPU eligibility, GPU count/write behavior, geometry publication, LOD coverage, build concurrency, or acceptance thresholds.

## Falsifier
Reject the change if the exact production traversal still exceeds p95 18 ms or p99 25 ms, loses all visible solids, opens the near/far fallback gap, reports a frame-path blocking completion, or the saved-pose RealPlayer replay materially regresses.

## Blast radius / cost
Render extraction only, limited by metadata-grid size to the two GPU-capable near rings in production. Worst-case synchronous metadata work is 5832 clear/map entries plus one 5832-entry compact scan for a step-2 snapshot; coarse grids remain jobs. No gameplay/collision authority, storage format, GPU format, geometry semantics, or allocations are changed.

## Result
Pending exact-SHA traversal and 45-second saved-pose RealPlayer replay.
