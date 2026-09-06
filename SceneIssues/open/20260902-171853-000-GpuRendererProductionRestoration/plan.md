# GPU renderer production restoration — GPU-first plan

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through the GPU backend, physically delete the retired CPU-only renderer, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable measured result under [tasks.md](tasks.md). Preserve authoritative CPU storage/generation/collision/simulation and necessary GPU host orchestration. No hidden content, weaker budgets, reduced draw distance, or permanent CPU fallback. G01–G26 remain mandatory before closure and promotion.

## Observed state

GPU launch source `9684ff509d65...`, request `fb4a7a92...`, run `34007154618` proved GPU extraction/publication with zero fallback in Rendering validation. Full VoxelShowcase remained fragmented at ~194–199 FPS with 1600x900 diagnostic settings: neither final visuals nor locked performance acceptance. Steps 4/8 and water still require migration; see [removal ledger](cpu-render-backend-removal-ledger.md).

G10 allocation must distinguish Exhausted/Stale/TooLarge and preserve live geometry until Commit. Run `34013990758` exposed Metal's eight-UAV limit, addressed by `2a59d1fb`. Runs `34019975638`, `34024202854`, and `34029387153` repeatedly observed Stale instead of Exhausted/TooLarge. `72634a3c` replaced production readback completion with graphics fences; the architecture gate passed. `e7a5038b` separated desired-generation bindings but did not repair the two status failures.

## Hypotheses and discriminating experiment

1. Allocator-local SRV/UAV aliasing of the batch counter buffer prevents later nonzero status writes. `959f7b4e` removes that alias without altering capacity semantics.
2. A separate allocator control-flow/resource defect causes the wrong status; generation-binding separation alone was falsified by `34029387153`.

Exact request `03694d19e7f78c38cc0cc9587043461423cf4b42` on source `6e80dc966`, run `34030901271`, tests hypothesis 1. GitHub currently confirms the run/job is queued. Preserve it; do not replace it or treat the wait as failure. Inspect terminal status, executed capacity/stale tests, no-readback gate, and retained player artifacts before selecting the next allocator change.

## Independent progress while queued

G05/G06: strengthen the existing real-kernel Planar/Sharp/Cubic half-brick fixture with an analytic geometry oracle independent of CPU meshing. Occupancy y=[0,4) and repeated neighbours imply two 8x8 boundary planes. Check indexed lattice positions, outward winding, each unit face's area, complementary triangles, duplicates, and material/style/normal attributes. Counts alone could miss misplaced or overlapping faces. New assertions await Unity/Metal execution; no pass is claimed and no CPU oracle is deleted yet.

## Remaining gates

Allocator result -> exact-source independent geometry regression -> GPU step-4/8/water migration and visual repairs -> physical CPU-backend deletion -> independent-consumer/edit/lifecycle proof -> locked repeated 1080p M4 Max/Metal workloads and memory budgets -> all repository-derived module/player gates -> current-master reconciliation, final PR and auto-merge. Existing Rendering validation scenes remain production consumers; this test-only change introduces no replacement scene/rendering path.
