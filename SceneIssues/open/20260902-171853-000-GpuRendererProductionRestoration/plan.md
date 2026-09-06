# GPU renderer production restoration — GPU-first plan

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through the GPU backend, physically delete the retired CPU-only renderer, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable measured result under [tasks.md](tasks.md). Preserve authoritative CPU storage/generation/collision/simulation and GPU host orchestration. No hidden content, weaker budgets, reduced draw distance, or permanent CPU fallback. G01–G26 remain mandatory.

## Current execution and evidence

User directs **local harness/tests and screenshot review; no further origin pushes**. Worktree: `/private/tmp/voxel-gpu-restoration`. Local commits `968a06ced` (watchdog) and `dc0ced3e2` (allocator) are not pushed. Remote request `03694d19...` / run `34030901271` was preserved; local work does not wait on it.

Three local 180-second visible, non-development Metal VoxelShowcase runs completed at 1920x1080, URP scale 1, walking at 90 seconds, with 11 screenshots each. Evidence under `Artifacts/LocalGpuShowcase/{753a21241-local-harness,stride-fixed,allocator-fixed}/` includes logs, XML, settings/source delta and diagnostic summaries.

All remain **unacceptable**: missing near terrain/structure surfaces, floating castle/vegetation, flat cyan water and blockout grey far masses. `missingVisible=0` contradicted rendered views: fence completion increments host publication without confirming live GPU geometry. Latest diagnostic windows give ~194 FPS stationary (per-window p95 5.41–5.79ms), ~103 FPS traversal (8.49–21.29ms). Missing content and screenshot/log overhead preclude benchmark acceptance; repeated locked workloads remain required.

## Proven repairs and falsified hypotheses

The local watchdog exhausted descriptors during repeated shell process-tree traversal. One bounded process-table snapshot replaces it, fails closed on accounting errors, and passes five behavioral tests. Subsequent guarded builds/tests completed normally.

Allocator descriptors were 36 bytes while host/mesher descriptors are 44. A two-record production-dispatch test failed on record 1's handle; sharing the full HLSL descriptor fixes it.

Counter-alias and desired-generation-alias changes did not fix capacity statuses. Local diagnostics proved correct desired/decoded identity. Removing the default Stale write merely changed erroneous output to zero. Classifying first and writing status at one common point before ownership mutation fixes both Exhausted/TooLarge cases. Nine focused Metal cases pass; four additional prepared-batch/publication tests pass. XML: `stride-fixed/allocator-classified-status.xml` and `allocator-fixed/production-pipeline.xml`. All guarded test invocations finished in 11–13 seconds; no skipped cases.

## Hypotheses and next discriminator

1. Remaining missing surfaces originate in full-scene density/count/write, despite bounded fixtures passing.
2. Correct geometry is lost during GPU publication/draw selection while host readiness hides it.

Next: correlate a known missing VoxelShowcase chunk with its real count/status, pending/live page record and draw arguments using bounded diagnostic evidence, then fix the earliest divergence and rerun the same local player capture. Do not infer coverage from host counters.

## Remaining gates

Correct full-scene GPU coverage/visuals -> GPU step-4/8/water migration -> physical CPU-backend deletion -> independent-consumer/edit/lifecycle proof -> locked repeated performance/memory workloads -> final local regression/artifact review. Remote promotion is outside current local execution.
