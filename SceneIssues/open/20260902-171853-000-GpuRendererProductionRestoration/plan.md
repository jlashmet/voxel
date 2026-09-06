# GPU renderer production restoration — GPU-first plan

## Objective and boundaries

Deliver the complete production-quality `Assets/Scenes/VoxelShowcase.unity` through the GPU voxel backend, physically delete the retired CPU-only renderer, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest honestly measured result under the locked benchmark in [tasks.md](tasks.md). CPU visual perfection is not a prerequisite. Preserve authoritative integer CPU storage/generation/collision/simulation and necessary GPU host orchestration. No content hiding, weaker budgets or permanent CPU fallback.

## Current reconciliation

Starting feature `a551c225d4797abd8b74aaf3889d5736d99e7fed`; remote master `ef475182b866eabfe8e1d1a39c82bf7810a03f49`. The feature's only cache divergence from current master is `GpuCutoverDisabled = true`. Restore the master's environment-controlled cache instead of overwriting other current systems with historical code. The extractor and draw dispatcher already match retained GPU source `a0ac0f5e...`; coordinator/page/mirror differences are not blindly restored.

The Rendering-owned `SolidGpu` validation scenes were absent. Restore their existing storage -> scheduler -> GPU -> URP consumers: minimal canonical fixture and multi-chunk traversal/edit/restart. Restore declarative `gpuCutover: required` handling so the module launcher's inherited CPU override cannot defeat GPU-required scenarios. Existing non-GPU diagnostic launches remain unchanged temporarily; their controls are removed during G18. No new parallel renderer.

Five filesystem/subprocess policy tests now pass locally; four fail against the exact pre-repair runner blob. A runtime cache-policy regression is added but has not run in Unity. See [gpu-resumption-evidence.md](gpu-resumption-evidence.md).

Prior request `560b0c08...`, run `34005604349`, completed success without replacement. Downloaded artifact `9981080134` now preserves separate FarWorld/Water outputs. Its CPU/owner-toggle captures are diagnostic only, not GPU or performance acceptance.

## Next discriminating run

Submit the restored source on `ci-test/fixes/agent-1` with the cache-policy regression, repository-derived module players, and a complete VoxelShowcase replay. Inspect real GPU requests, candidate publications, fallback, visible output and startup/settled frames. Current publication counters alone cannot establish successful GPU output: the feedback's outcome/commit concerns remain open. Record unavailable GPU timings as unavailable; existing FPSLOG is an initial diagnostic, not the locked benchmark result.

**H1:** GPU input/layout or pending/publication/lifetime contracts lose valid surfaces. Compare identical canonical inputs and stop at the first divergence; prioritize [external-agent-feedback.md](external-agent-feedback.md) transaction/lifetime findings.

**H2:** shared far geometry/material ownership also causes malformed masses. Diagnose with GPU active; disabled-owner frames never pass acceptance.

## Ownership and remaining gates

Rendering owns restored `Rendering/Validation/SolidGpu/` and local EditMode tests; Python tooling owns launch-policy tests. Headless value adapters retain local tests. G01–G04 remain open until exact-source runtime evidence. Continue G05–G18 semantics, all LODs, two-phase publication, lifetime, edits and deletion, alongside G19–G23 profiling. G24–G27 require final CPU-backend-free tests, inspected visuals, full-application/independent reuse proof, current-master integration and PR + auto-merge. Only verified complete acceptance permits closure.
