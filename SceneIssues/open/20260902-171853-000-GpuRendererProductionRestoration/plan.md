# GPU renderer production restoration — GPU-first plan

## Objective and boundaries

Deliver the complete production-quality `Assets/Scenes/VoxelShowcase.unity` through the GPU voxel backend, physically delete the retired CPU-only renderer, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest honestly measured result under [tasks.md](tasks.md). CPU visual perfection is not a prerequisite. Preserve authoritative integer CPU storage/generation/collision/simulation and necessary GPU host orchestration. No hidden content, weaker budgets or permanent CPU fallback.

## Current reconciliation

Source **`9684ff509d65ab7a1caca6245d0f0093f28e249d`** restores the actual GPU route. Starting feature `a551c225...`; fetched master `ef475182b866eabfe8e1d1a39c82bf7810a03f49`. The cache's only divergence from master was `GpuCutoverDisabled = true`; restore the environment-controlled value. Extractor/draw-dispatcher already match retained GPU source `a0ac0f5e...`; do not blindly restore old coordinator/page/mirror files.

Restore existing Rendering-owned `SolidGpu` minimal and multi-chunk traversal/edit/restart consumers. Restore `gpuCutover: required` scenario handling to clear the child CPU override. Non-GPU diagnostic launches remain temporarily unchanged; G18 removes obsolete controls. Five local launcher tests pass; four fail before. The new cache-policy Unity regression awaits CI. No CPU backend deletion or runtime GPU success is claimed.

Previous request `560b0c08...`, run `34005604349`, completed success without replacement; artifact `9981080134` retains independent FarWorld/Water outputs. Its CPU captures remain diagnostic only.

## Active exact CI and next discriminator

Direct-child request **`fb4a7a92de3420c0affa2a5463287d0252f67797`**, run **`34007154618`**, job **`101416373122`** targets source **`9684ff509d65ab7a1caca6245d0f0093f28e249d`**. It is **queued** at the latest observation. Leave it untouched. It includes the cache-policy regression, repository-derived module players and a 65-second full VoxelShowcase replay. Inspect GPU requests, allocations, live geometry, fallback and actual images; current candidate-publication counters alone cannot prove successful output. This first diagnostic remains 1600x900, not the locked 1080p performance benchmark.

**H1:** GPU input/layout or pending/publication/lifetime contracts lose valid surfaces. Compare canonical inputs and stop at the first divergence; prioritize transaction/lifetime findings in [external-agent-feedback.md](external-agent-feedback.md).

**H2:** shared far geometry/material ownership also produces malformed masses. Diagnose with GPU active; disabled-owner frames never pass acceptance.

## Timing repair staged after the active source

Ordinary captures omitted the build's existing frame-timing flag despite invoking FRAMEPIPE collection. Add that flag for all diagnostic captures; the existing builder restores project settings in finally. Five shell-launch tests: three intended failures before, all five pass after; both new tooling suites total ten passes locally. No Unity was launched. This patch is NOT part of the queued request. After it terminates, include the timing fix with the next justified exact-source validation. Sampling availability/overhead still need measurement; zero samples remain unavailable, not zero-cost success.

## Ownership and remaining gates

Rendering owns restored `Rendering/Validation/SolidGpu/` and local tests; tooling owns launcher/timing regressions. G01–G04/G19 remain open pending exact runtime proof. Continue semantics/all LODs, two-phase publication/lifetime, streaming/edits, CPU deletion and measured optimization. Final CPU-backend-free tests, production visuals, full-application/independent reuse, current-master integration and PR + auto-merge precede closure. See [gpu-resumption-evidence.md](gpu-resumption-evidence.md).
