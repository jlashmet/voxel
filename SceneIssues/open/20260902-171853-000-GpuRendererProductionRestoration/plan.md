# GPU renderer production restoration — GPU-first plan

## Objective and boundaries

Deliver the complete production-quality `Assets/Scenes/VoxelShowcase.unity` through the GPU backend, physically delete the retired CPU-only renderer, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable measured result under [tasks.md](tasks.md). CPU visual perfection is not a prerequisite. Preserve authoritative CPU storage/generation/collision/simulation and necessary GPU host orchestration. No hidden content, weaker budgets or permanent CPU fallback.

## Exact source and active request

Starting feature **`568a8b8f1251760eb92b6cfd1ef547e2cee4c569`**; fetched master **`356b2e0e4d2818901c73bbc6b1788f8d6850356d`** through the GitHub connector; direct git transport is unavailable. GPU launch restoration is source **`9684ff509d65ab7a1caca6245d0f0093f28e249d`**. Request **`fb4a7a92de3420c0affa2a5463287d0252f67797`**, run **`34007154618`**, job **`101416373122`**, remains **queued** at inspection. Preserve it unchanged. It includes the cache-policy regression, restored GPU module consumers and 65-second full VoxelShowcase replay. It excludes the later frame-timing repair and handle-command work.

Earlier run `34005604349` retained separate FarWorld/Water evidence; CPU images remain diagnostic only. Launch/timing tooling has ten recorded local passes; no new Unity/GPU pass is inferred. See [gpu-resumption-evidence.md](gpu-resumption-evidence.md).

## Hypotheses and next discriminators

**H1:** GPU completion/publication/ownership failures lose valid geometry. The coordinator signals completion at submission; actual outcomes and live geometry require separate proof. The arena also returns duplicate-released handles twice and submits unordered same-handle generation writers.

**H2:** input/layout or shared far geometry/material defects explain additional malformed regions. Inspect the exact GPU captures and canonical inputs instead of CPU-only polish or blindly importing historical GPU files.

The bounded G10/G12 repair reconciles historical command coalescing and adds host `Free/Acquired/ReleaseQueued` ownership: one command per handle per flush, idempotent release until reacquisition, and no generation update erasing release cleanup. Before-fix tests are source **`03498b9bf7bf2bf0bdeee341ee8d08a0ef347dce`**; the repair is its direct descendant. Seven real-arena GPU regressions await exact Unity validation. Do not mark full publication/lifetime work complete. See [gpu-handle-command-evidence.md](gpu-handle-command-evidence.md) and [external-agent-feedback.md](external-agent-feedback.md).

## Ownership, cost and remaining gates

Rendering owns the arena, module EditMode regressions and existing `Rendering/Validation/SolidGpu/` scenes. Tooling owns launch/timing tests. New host bookkeeping is bounded by handle/transport capacity; no production readback, GPU allocation or wait is added.

After the active request terminates, inspect tests, output status, actual GPU draws, fallback, images and timings; collect fail-before/pass-after for the staged handle repair. No local Unity compilation is claimed. G01–G04/G19, semantic/LOD, transaction/lifetime, edits/streaming, CPU deletion and profiling gates remain open. Final CPU-backend-free proof precedes closure, current-master integration and PR + auto-merge.
