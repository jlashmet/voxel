# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Observed behavior / acceptance
- Exact built-player evidence at `db1230b` reached 636–770 ms solid admission, 757 missing visible chunks, and four drawn. Subsequent exact-block/optional-halo iterations removed the 700 ms stall but remained too slow or starved during traversal.
- Preserve deterministic CPU world truth, collision, replication, residency, and stale-publication rejection. Presentation may lag behind one immutable generation while old/far geometry covers it.
- Pass requires sustained step-1/step-2 GPU completion, zero eligible CPU fallback, no visible holes/blocking waits, moving p95 <18 ms/p99 <25 ms, stationary p95 <8 ms, and inspected exact-pose built-player evidence.

## Hypotheses / discriminators
- H1 (**confirmed**): whole-region recovery and per-chunk CPU brick snapshots dominate admission. One region contains 262,144 logical blocks; recovering only demanded blocks and staging GPU candidates before CPU metadata/payload pinning removes both costs.
- H2 (**confirmed contributor**): global “no active extraction” mutation and serialized demand starve recovery. Region-scoped readers plus coalesced block demand allow unrelated recovery to progress safely.
- H3 (**under test**): repeated 1,000–5,832-block admission scans and abandoned async readbacks create the remaining tail. A local 210 m run completed with zero eligible fallback but moving p95 23.156 ms. Epoch-based one-shot demand and retaining stale GPU transactions until readback completion should remove duplicate CPU/GPU work.

## Selected fix / material results
- Use one demand-filled, versioned world GPU mirror shared by all workers. Copy mixed payloads directly from one borrowed `RegionReadView`; do not pin/copy a CPU exact snapshot for GPU candidates.
- Classify raw mirrored semantics in compute. Empty completes without geometry; decorated/faceted/profile content retains the CPU fidelity path. Geometry stays GPU-resident through count/reserve/write/draw.
- Bound mirror maintenance by 0.10 ms and 256 KiB/frame; prioritize exact chunk footprints, accept nonresident optional halo as canonical empty, and keep core residency mandatory.
- Protect only regions read by active count/write. On capacity pressure atomically evict an inactive mirror coordinate/directory entry. Transient Metal counter or count/write failures retry the immutable GPU transaction rather than build a CPU snapshot.
- Local focused results so far: EditMode policy/classification 5/5 passed; real-device arena bridge 4/4 passed with zero geometry readback; 210 m traversal reached zero eligible fallback before failing only the 18 ms p95 gate.

## Remaining gates
- Run merged liveness, arena, traversal, and allocation regressions; inspect fallback/retry/snapshotless telemetry.
- Commit final diff, create the exact feature-parented CI request on `ci-test/fixes/agent-2`, and inspect exact built-player screenshots/logs before closure.
