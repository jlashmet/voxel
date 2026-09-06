# GPU renderer production restoration

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through the GPU backend, physically delete the retired CPU-only renderer, and pursue **1,000 FPS / 1.00ms whole frame**, or the closest repeatable result under [tasks.md](tasks.md). Preserve authoritative CPU storage/generation/collision/simulation and necessary GPU host orchestration. No hidden content, weaker budgets, reduced distance or permanent CPU fallback. All task gates remain mandatory.

## Execution and material results

User directs local harness/tests and screenshot review; latest instruction authorizes pushing existing work to `origin/fixes/agent-1`, then continuing. Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`.

Proven repairs: bounded watchdog,44-byte descriptor, allocator status, indirect bucket prefix, compatible batch layouts, async rejection/retry and bounded offscreen eviction.

Render-control contract: only asynchronous16-byte status/handle/generation feedback per chunk; no generated geometry/count readback, blocking wait or authoritative-state derivation. Lane scratch remains owned through callback completion.

Automatic publication is deleted. Unique renderer generations and explicit source/configuration approval gate candidates. GPU finalization rejects mismatched write totals without transferring counts. Five regression cases failed before,31 focused tests plus4 arena PlayMode tests pass after. Production module48s and showcase180s completed with zero transaction rejections; reviewed showcase remains **unacceptable**. These checks do not prove payload correctness, complete coverage or performance.

## Hypotheses and discriminating experiments

Far-world is active:6 terrain rings/1481 semantic sources. Storage restoration proved the startup bake had air at the current mountain interior(-600,358,200). Existing baker regenerated199 regions in144s, peak10.6GB/zero swap; both mountain tests now pass. Harness-standard12GB guard retained free-memory/swap limits after initial6GB attempt failed before writing.

Normal180s player completed12 captures/no transaction rejection. Reviewed15s/60s/150s remains **unacceptable**: coarse side blocks, terrain bands and flat traversal foreground. Stationary missing coverage reached0; traversal reclosed terrain hole. Diagnostic CPU window p50 medians6.35/8.59ms are not acceptance.

Rebaked65s owner probe completed11 captures: suppressing semantic proxies reveals detailed dark mountain and roofed houses; restoration covers them again. Visibility/issue metadata restored. Diagnostic-only.

1. **Confirmed:** stale startup content prevented detailed mountain replacement; regeneration fixes occupancy.
2. **Confirmed overlap:** semantic far proxies obscure existing near geometry. Terrain-cutout collapse remains a separate candidate for terrain defects.

Current work: bounded `SurfaceReplacementCoverage` checks every intersected64-voxel cell against current selected/known-empty LOD proofs, refusing oversized bounds. All7 domain tests pass in12s; no player gate applies to this unconnected calculation. It is not connected to runtime yet.

Integration requires same-camera-frame selection in the render pass, not last-frame `Update` metrics. Unknown chunks cannot imply empty: clipmap eviction removes `_known`. Preserve completed regional discovery masks to distinguish empty cells from missing publication, invalidate on edits/eviction, and bound retention by residency. Then route semantic far submission through the same render boundary. Prove replacement coverage and restoration after invalidation/eviction; never use distance alone or global convergence to hide proxies. Validate a module-local production consumer and normal showcase screenshots. Resume G11 cancellation/last-consumer lifetime afterward.

## Remaining gates

Finish permanent-error policy, explicit publication and GPU-completion-based retirement/lifetime; migrate step4/8 and water to GPU; replace CPU-dependent test oracles and physically delete CPU-only rendering. Preserve module-local production scenes. Prove edits, pressure and lifecycle, then run locked repeated full-frame/memory workloads and final visual review. G01–G27 remain authoritative; no completion claim.
