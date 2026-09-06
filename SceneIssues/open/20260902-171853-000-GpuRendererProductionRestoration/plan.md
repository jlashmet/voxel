# GPU renderer production restoration — GPU-first plan

## Objective and boundaries

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through the GPU backend, physically delete the retired CPU-only renderer, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable measured result under [tasks.md](tasks.md). Preserve authoritative CPU storage/generation/collision/simulation and necessary GPU host orchestration. No hidden content, weaker budgets, reduced draw distance, or permanent CPU renderer fallback.

## Observed state and acceptance

Known-good GPU launch source `9684ff509d65...`, request `fb4a7a92...`, run `34007154618` proved genuine GPU extraction/publication with zero fallback in Rendering validation, but full VoxelShowcase remained visually fragmented and only ~194–199 FPS at 1600x900 diagnostic settings. This is retained evidence, not final acceptance. CPU-removal inventory remains [cpu-render-backend-removal-ledger.md](cpu-render-backend-removal-ledger.md); steps 4/8 and water still require GPU migration before deletion.

G10 requires two-phase pending -> explicit Commit publication, distinct failure statuses, prior-live preservation, generation correctness, and release/reacquire safety. G01–G26 and the locked visual/performance/removal gates remain mandatory before closure.

## Current discriminator

Exact request `99f0c7b93606f918c0432016c87e60849f6d69ea`, run `34013990758`, tested source `12a00b8f...` and failed as a product result: `CSAllocateBatchPages` exceeded Metal's 8-UAV limit with a ninth writable resource. The same run returned default `Ready` where `Exhausted`/`TooLarge` were expected, consistent with the allocator dispatch not executing correctly.

Two hypotheses:
1. **Selected:** allocator resource pressure prevents the transaction kernel from executing; removing the ninth writable dependency restores both dispatch and status semantics.
2. Status publication is independently wrong even after a valid dispatch; if the same tests still fail after the UAV repair, isolate status ordering/aliasing before another production change.

Current source restored the known-good publication baseline at `690a2ae...`. Fix `2a59d1fbed16fa967a5e060716041a4332ff7b7b` keeps live geometry on delayed retirement but directly recycles superseded *pending* pages only after the replacement is known to fit. This removes retired-page UAV writes from allocation and preserves an existing pending candidate on `Exhausted`/`TooLarge`.

## Next gate

Exact CI request `400119258266df29add711d04857a49019e2d5a5` (run `34019975638`) is the sole active validation for source `2a59d1fb...`; preserve it while queued/running. If green, reconcile G10's remaining cache-level explicit commit identity contract, then continue the next unchecked non-blocked GPU correctness/removal/performance work. If product-red, diagnose that exact failure; do not replace it as infrastructure. Final CPU-backend-free exact-SHA validation, current-master reconciliation, PR + auto-merge remain mandatory.
