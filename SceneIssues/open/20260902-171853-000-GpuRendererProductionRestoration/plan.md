# GPU renderer production restoration

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through the GPU backend, physically delete the retired CPU-only renderer, and pursue **1,000 FPS / 1.00ms whole frame**, or the closest repeatable result under [tasks.md](tasks.md). Preserve authoritative CPU storage/generation/collision/simulation and GPU host orchestration. No hidden content, weaker budgets, reduced distance or permanent CPU fallback.

## Execution and results

User authorizes local harness/tests/screenshots and pushed existing work through2421192e6 to `origin/fixes/agent-1`. Subsequent summit residency work is local in `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`.

Prior repairs cover GPU descriptor/layout, allocation, indirect bucket prefix, asynchronous recovery, explicit approval and GPU write-finalization. Geometry/counts never return to CPU authority. Prior31 focused/4 arena tests passed. Retirement/lifetime and CPU-backend deletion remain incomplete.

Startup bake lacked the mountain interior. Existing baker regenerated199 regions; production Storage regression and mountain generation tests pass. Regeneration used the harness-standard12GB process guard, peak10.6GB/zero swap; production budgets unchanged.

Per-object handoff now runs against the same camera's near selection in the voxel render pass. Current drawable/known-empty nodes and completed regional discovery masks prove replacement. Unknown/partial discovery and invalidation retain proxies. Discovery evidence is capped at1024 records,512 surface bits each; overflow remains unknown. Showcase opts into this path; ordinary far consumers retain their existing submission.

All14 final domain/presentation/lifecycle tests pass (`replacement-final-lifecycle-tests.xml`,16s). Final48s production module passed both-instance handoff, edit restoration and restart,8 captures/no GPU fallback/error counters. Reviewed42s shows the generated WorldBuilder landmark. Its voxel and far realizations consume the same production catalogue.

Normal180s Showcase completed12 captures/no transaction errors. Stationary mountain replacement improves; side blocks and150s flat traversal obstructions remain **unacceptable**. CPU window p50 medians6.6/10.2ms are diagnostics, not acceptance. The65s owner probe completed11 captures, restoring visibility and exact issue metadata.

## Hypotheses and next experiments

1. Remaining wall-like mountain forms come from proxy shape loss: `BuildGeometryMesh` maps Ramp and most shapes to boxes. Preserve canonical ramp direction/shape through composition and Rendering, with independent raster expectations and module/player evidence.
2. The summit proxy also lacks a complete detailed replacement. Bake directory inspection proves region(-2,1,0) absent; its only upper regions are(0,1,0)/(0,1,1). Regression failed specifically on the absent summit region, then passed after CPU footprint residency and rebaking200 regions (147s/8.8GB). All5 focused tests pass. Normal95s player completed6 captures/no transaction errors:74.8s shows detailed summit and roofed side buildings, semantic proxies0/near1477. Quality remains unacceptable. Audit separated vertical layers/composed children and add a focused module streaming scene before accepting residency. Loading caps unchanged.

Audit unnecessary discovery invalidation on clipmap rescans, global change-feed gating and query cost during traversal. Unresolved content/coverage must keep a valid far representation. Resume G11 cancellation/last-consumer lifetime afterward.

## Remaining gates

Finish publication/permanent-error policy and GPU-completion retirement; migrate step4/8 and water; replace CPU-dependent oracles and delete CPU-only rendering. Validate all module/integration players, edits, pressure and lifecycle. Then run locked repeated full-frame/memory workloads and production-quality visual review. G01–G27 remain authoritative; no completion claim.
