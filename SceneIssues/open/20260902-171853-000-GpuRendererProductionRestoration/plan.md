# GPU renderer production restoration — GPU-first plan

## Objective and boundaries

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through the GPU backend, physically delete the retired CPU-only renderer, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable measured result under [tasks.md](tasks.md). Preserve authoritative CPU storage/generation/collision/simulation and necessary GPU host orchestration. No hidden content, weaker budgets, reduced draw distance, or permanent CPU renderer fallback.

## Observed state and acceptance

Known-good GPU launch source `9684ff509d65...`, request `fb4a7a92...`, run `34007154618` proved genuine GPU extraction/publication with zero fallback in Rendering validation, but full VoxelShowcase remained visually fragmented and only ~194–199 FPS at 1600x900 diagnostic settings. This is retained evidence, not final acceptance. CPU-removal inventory remains [cpu-render-backend-removal-ledger.md](cpu-render-backend-removal-ledger.md); steps 4/8 and water still require GPU migration before deletion.

G10 requires two-phase pending -> explicit Commit publication, distinct failure statuses, prior-live preservation, generation correctness, and release/reacquire safety. G01–G26 and the locked visual/performance/removal gates remain mandatory before closure.

## Current discriminator

Exact request `99f0c7b93606f918c0432016c87e60849f6d69ea`, run `34013990758`, first proved `CSAllocateBatchPages` exceeded Metal's 8-UAV limit. Source `2a59d1fbed16fa967a5e060716041a4332ff7b7b` removed retired-page UAV writes from allocation while preserving delayed retirement for live geometry and direct recycle only for superseded pending candidates.

Exact request `400119258266df29add711d04857a49019e2d5a5`, run `34019975638`, then proved three real product failures: `Exhausted` and `TooLarge` both remained `Stale`, and production still used `AsyncGPUReadback`. Source `72634a3ca8e1dc1288469037ac930ee283aff129` replaced production batch completion with per-lane graphics fences; exact request `a16497220a861976e0a95a2cd9a1eee1d93baac7`, run `34024202854`, confirmed the no-readback architecture gate is fixed but the same two allocator status assertions still fail.

The generation-binding discriminator in `e7a5038b2008f7835ce629dcd9c30e2b15ef3047` was then exercised by exact request `1b76695dcb2e8941f46d82826a2e39cf5e5f4fae`, run `34029387153`. The run again failed only the two capacity-status regressions: `Exhausted` expected `1` but observed `2`, and `TooLarge` expected `3` but observed `2`; the explicit stale-allocation test passed. The retained player build/replay and screenshot steps also completed, so this is a product failure rather than CI infrastructure.

That repeated result narrows the production-faithful repro further. `CSAllocateBatchPages` binds the batch counter buffer simultaneously as `_BatchCounters` (RW/UAV) and `_BatchCountersRead` (read-only/SRV). The successful path cannot disprove the hazard because `AllocationReady` is zero and the test buffer starts at zero; the stale path proves the initial nonzero write to `2`; both later capacity branches attempt to replace that value with another nonzero status and remain observably stuck at the earlier `2`. This is the same class of Metal SRV/UAV alias hazard already demonstrated for the desired-generation resource, now isolated to the one buffer common to the two persistent failures.

Bounded fix `959f7b4e648119062a3cc4a0bbf7d350deffc452` keeps the allocation kernel on a single RW view of the batch counters for its semantic-support read and status/count writes. Publication still retains its existing read alias because this exact failure is allocator-local. The same commit also corrects the stale shader comment to describe graphics-fence ordering rather than a production status readback.

## Next gate

Submit one exact-SHA targeted-CI request for the current feature source (including `959f7b4e648119062a3cc4a0bbf7d350deffc452` and this plan update) through `ci-test/fixes/agent-1`, changing only `.github/test-request.json` in the request commit. Preserve that request while queued/running. Require the two capacity-status regressions and the no-`AsyncGPUReadback` architecture gate to pass before marking any corresponding G10/G11 work complete.

A green allocator run is not feature closure. Continue the next unchecked non-blocked acceptance work: full GPU VoxelShowcase correctness, GPU migration of step-4/step-8/water responsibilities, physical deletion of the CPU rendering backend and fallback controls, independent-consumer proof, locked M4 Max/Metal performance workloads, final regressions/artifacts, current-master reconciliation, then PR + auto-merge.