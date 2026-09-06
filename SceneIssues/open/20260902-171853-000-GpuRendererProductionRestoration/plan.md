# GPU renderer production restoration — GPU-first plan

## Objective and boundaries

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through the GPU backend, physically delete the retired CPU-only renderer, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable measured result under [tasks.md](tasks.md). CPU visual perfection is not a prerequisite. Preserve authoritative CPU storage/generation/collision/simulation and necessary GPU host orchestration. No hidden content, weaker budgets, reduced draw distance, or permanent CPU renderer fallback.

## Exact state

Current feature lineage includes GPU launch restoration `9684ff509d65...`, frame-timing repair `568a8b8f125...`, bounded same-handle ownership repair `6532462cfe0...`, CPU-removal ledger `24ac26bc937...`, and current fail-before transaction source **`51a9f344ec62f583f66a6c1acb3a801efdbf0bae`**. See [gpu-resumption-evidence.md](gpu-resumption-evidence.md), [cpu-render-backend-removal-ledger.md](cpu-render-backend-removal-ledger.md), and [external-agent-feedback.md](external-agent-feedback.md).

Preserved request **`fb4a7a92de3420c0affa2a5463287d0252f67797`**, run **`34007154618`**, job **`101416373122`**, validates exact source `9684ff509d65...` and is **in progress** in repository-derived module validation. It includes restored SolidGpu consumers and a 65-second VoxelShowcase replay; never replace it while running. It excludes later timing/transaction work.

## Hypotheses and discriminators

**H1 — confirmed contract gap:** current `SealCountBatch` dispatches work, inserts a fence, immediately reports every context complete, and resets the lane; current page publication can make pending geometry live before `FinishPagedGpuBuild` performs the renderer slot/version approval. Allocation already emits `Ready/Exhausted/Stale/TooLarge` status, but production does not consume it.

Fail-before `GpuPagedPublicationTransactionTests` at `51a9f344...` uses the real page-arena compute shader. Its primary case requires a successful allocation to remain pending after write/finalization; current immediate publication should fail. Companion cases require stale/exhausted/oversized status to remain distinguishable without geometry readback.

**Selected fix:** keep submitted lanes/resources alive through a bounded asynchronous readback of the existing small counters/status buffer; validate status + handle + immutable generation; never read generated vertices/indices. Successful output remains pending. Existing `FinishPagedGpuBuild` slot/desired-version check is the CPU approval point: queue generation-tagged **Commit** only after it passes, otherwise **Abort** and preserve prior live geometry. Lane reuse/teardown must wait for final GPU consumer completion. This is one transaction state machine, not six unrelated patches.

**H2:** after the P0 transaction is trustworthy, remaining malformed regions may be shared far-presentation/material/LOD defects. Diagnose them with GPU active. Migrate steps 4/8 and water before deleting their CPU renderers.

## Remaining gates

Finish exact fail-before/pass-after P0 evidence; inspect the preserved GPU run’s counters/images; complete semantic/LOD, lifetime/pressure, streaming/edit/restart and independent-consumer proof; migrate/delete CPU-only rendering files per ledger; then run the locked whole-frame benchmark and optimize measured bottlenecks. Final CPU-backend-free exact-SHA validation precedes `open` -> `closed`, current-master integration, PR + auto-merge, and verification on `origin/master`.
