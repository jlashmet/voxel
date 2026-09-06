# GPU renderer production restoration — GPU-first plan

## Objective and boundaries

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through the GPU backend, physically delete the retired CPU-only renderer, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable measured result under [tasks.md](tasks.md). Preserve authoritative CPU storage/generation/collision/simulation and necessary GPU host orchestration. No hidden content, weaker budgets, reduced draw distance, or permanent CPU renderer fallback.

## Observed state and acceptance

Known-good GPU launch source `9684ff509d65...`, request `fb4a7a92...`, run `34007154618` proved genuine GPU extraction/publication with zero fallback in Rendering validation, but full VoxelShowcase remained visually fragmented and only ~194–199 FPS at 1600x900 diagnostic settings. This is retained evidence, not final acceptance. CPU-removal inventory remains [cpu-render-backend-removal-ledger.md](cpu-render-backend-removal-ledger.md); steps 4/8 and water still require GPU migration before deletion.

G10 requires two-phase pending -> explicit Commit publication, distinct failure statuses, prior-live preservation, generation correctness, and release/reacquire safety. G01–G26 and the locked visual/performance/removal gates remain mandatory before closure.

## Current discriminator

Exact request `99f0c7b93606f918c0432016c87e60849f6d69ea`, run `34013990758`, first proved `CSAllocateBatchPages` exceeded Metal's 8-UAV limit. Source `2a59d1fbed16fa967a5e060716041a4332ff7b7b` removed retired-page UAV writes from allocation while preserving delayed retirement for live geometry and direct recycle only for superseded pending candidates.

Exact request `400119258266df29add711d04857a49019e2d5a5`, run `34019975638`, then proved three real product failures: `Exhausted` and `TooLarge` both remained `Stale`, and production still used `AsyncGPUReadback`. Source `72634a3ca8e1dc1288469037ac930ee283aff129` replaced production batch completion with per-lane graphics fences; exact request `a16497220a861976e0a95a2cd9a1eee1d93baac7`, run `34024202854`, confirmed the no-readback architecture gate is fixed but the same two allocator status assertions still fail.

Because the same allocator symptom survived two materially different fixes, the repository rule requires a minimal root-cause discriminator before another product change. The isolated cause is Metal resource binding, not the capacity arithmetic or descriptor layout:

1. The original shader exposed the same `DesiredGenerations` buffer simultaneously as RW and read-only aliases and `BindAllKernels` bound both aliases to every kernel, which is an unsafe SRV/UAV alias on Metal.
2. Commit `056193f6df9c043bca0f0628069458e244fd3f73` removed the read alias, but that made `CSAllocateBatchPages` consume the desired-generation buffer as an RW resource again, undoing the prior eight-UAV budget repair.
3. `CSAllocateBatchPages` initializes status to `Stale` before generation validation, so a missing/invalid desired-generation read exits before the later `TooLarge`/`Exhausted` assignments. This exactly matches both persistent failures.

Bounded fix `e7a5038b2008f7835ce629dcd9c30e2b15ef3047` separates the bindings by kernel: `CSApplyHandleCommands` receives only the RW desired-generation view; allocation/publication/commit kernels receive only the read-only view of the same physical buffer. This preserves GPU ownership while avoiding both same-kernel SRV/UAV aliasing and the allocator's ninth UAV.

## Next gate

Submit one exact-SHA targeted-CI request for source `e7a5038b2008f7835ce629dcd9c30e2b15ef3047` through `ci-test/fixes/agent-1`, changing only `.github/test-request.json` in the request commit. Preserve that request while queued/running. Require the two capacity-status regressions and the no-`AsyncGPUReadback` architecture gate to pass before marking any corresponding G10/G11 work complete.

A green allocator run is not feature closure. Continue the next unchecked non-blocked acceptance work: full GPU VoxelShowcase correctness, GPU migration of step-4/step-8/water responsibilities, physical deletion of the CPU rendering backend and fallback controls, independent-consumer proof, locked M4 Max/Metal performance workloads, final regressions/artifacts, current-master reconciliation, then PR + auto-merge. Correct the inherited stale shader comment that still mentions a tiny status readback before final acceptance.