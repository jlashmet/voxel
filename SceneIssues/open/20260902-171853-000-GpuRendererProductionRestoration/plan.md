# GPU renderer production restoration — GPU-first plan

## Objective and boundaries

Deliver the complete production-quality `Assets/Scenes/VoxelShowcase.unity` through the GPU backend, physically delete the retired CPU-only renderer, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable measured result under [tasks.md](tasks.md). CPU visual perfection is not a prerequisite. Preserve authoritative CPU storage/generation/collision/simulation and necessary GPU host orchestration. No hidden content, weaker budgets or permanent CPU fallback.

## Exact source and active request

Starting feature **`568a8b8f1251760eb92b6cfd1ef547e2cee4c569`**; fetched master **`356b2e0e4d2818901c73bbc6b1788f8d6850356d`** via the GitHub connector; direct git transport is unavailable. GPU launch restoration is source **`9684ff509d65ab7a1caca6245d0f0093f28e249d`**. Request **`fb4a7a92de3420c0affa2a5463287d0252f67797`**, run **`34007154618`**, job **`101416373122`**, remains **queued** at inspection. Preserve it unchanged. It includes the cache-policy regression, restored GPU module consumers and 65-second full VoxelShowcase replay. It excludes the later frame-timing repair and new handle-command regression.

The earlier owner-probe run `34005604349` retained separate FarWorld/Water evidence; CPU images remain diagnostic only. Launch/timing tooling regressions have ten recorded local passes; no new Unity/GPU success is inferred. See [gpu-resumption-evidence.md](gpu-resumption-evidence.md).

## Hypotheses and next discriminators

**H1:** GPU completion, publication or ownership failures lose valid geometry. Current code immediately signals completion after submission; pending/output status and actual visible geometry must be inspected separately. The current arena also appends duplicate release records and returns their handle twice; repeated generation commands have unordered same-handle GPU writers.

**H2:** input/layout or shared far geometry/material defects account for additional malformed regions. Inspect the queued exact GPU captures and canonical inputs rather than restarting CPU-only polish or blindly importing the historical GPU branch.

While CI is queued, add a bounded before-fix regression for duplicate releases and same-handle command transport under existing G10/G12. Reconcile historical coalescing, add terminal-release ownership checks, and require actual fail-before/pass-after evidence. Do not treat that narrow fix as the full transaction/lifetime solution. See [gpu-handle-command-evidence.md](gpu-handle-command-evidence.md) and [external-agent-feedback.md](external-agent-feedback.md).

## Ownership, cost and remaining gates

Rendering owns the real arena, module EditMode tests and existing `Rendering/Validation/SolidGpu/` minimal/multi-chunk scenes. Tooling owns launch/timing tests. Host command bookkeeping stays bounded by handle/transport capacity; no production readback or geometry changes are needed for this discriminator. Tests may use blocking readback only outside production.

After the active request terminates, inspect tests, GPU publications/draw ownership, fallback, images and timings; failures require correction, not replacement while active. G01–G04/G19 and remaining semantics/LOD, transaction/lifetime, edits/streaming, CPU deletion, profiling and final integration gates remain open. Final CPU-backend-free proof precedes closure, current-master integration and PR + auto-merge.
