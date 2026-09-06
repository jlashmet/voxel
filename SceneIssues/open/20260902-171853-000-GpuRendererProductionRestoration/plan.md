# GPU renderer production restoration

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through the GPU backend, physically delete the retired CPU-only renderer, and pursue **1,000 FPS / 1.00ms whole frame**, or the closest repeatable result under [tasks.md](tasks.md). Preserve authoritative CPU storage/generation/collision/simulation and necessary GPU host orchestration. No hidden content, weaker budgets, reduced distance or permanent CPU fallback. All task gates remain mandatory.

## Execution and material results

User directs local harness/tests and screenshot review; latest instruction authorizes pushing existing work to `origin/fixes/agent-1`, then continuing. Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`.

Proven repairs: bounded watchdog,44-byte descriptor, allocator status, indirect bucket prefix, compatible batch layouts, async rejection/retry and bounded offscreen eviction.

Render-control contract: only asynchronous16-byte status/handle/generation feedback per chunk; no generated geometry/count readback, blocking wait or authoritative-state derivation. Lane scratch remains owned through callback completion.

Automatic publication is deleted. Unique renderer generations and explicit source/configuration approval gate candidates. GPU finalization rejects mismatched write totals without transferring counts. Five regression cases failed before,31 focused tests plus4 arena PlayMode tests pass after. Production module48s and showcase180s completed with zero transaction rejections; reviewed showcase remains **unacceptable**. These checks do not prove payload correctness, complete coverage or performance.

## Hypotheses and discriminating experiments

Far-world is active:6 terrain rings and1481 semantic source instances. Reversible owner probes locate the grey mountain and coarse side structures in semantic far presentation, inside the streaming radius. Initial startup-bake decoding found air in the current mountain interior. Production Storage restoration verified the region semantic hash and reproduced missing occupancy at(-600,358,200).

The baker regenerated199 regions in144s, peak10.6GB and zero swap growth with harness-standard12GB guard. Initial6GB attempt was killed before writing; free-memory/swap guards and production budgets are unchanged. Both mountain production tests now pass, including the previously failing occupancy assertion.

Normal180s player completed with12 captures and no transaction rejection/exception. Reviewed15s/60s/150s remains **unacceptable**: coarse side blocks, terrain bands and huge flat foreground surfaces during traversal. Stationary coverage reached zero missing visible chunks; traversal reclosed the global terrain hole. Diagnostic CPU window p50 medians6.35ms stationary/8.59ms walking are not benchmark acceptance.

The rebaked65s owner probe completed with11 captures. At35s, suppressing only semantic proxies reveals a dark detailed mountain and roofed houses; restoring40s covers them again with the grey mountain and coarse blocks. Original visibility and exact issue metadata restored. Diagnostic-only, no visual acceptance.

1. **Confirmed:** stale startup content prevented detailed mountain replacement; regeneration fixes occupancy.
2. **Confirmed overlap:** semantic far proxies obscure existing near geometry. Terrain-cutout collapse remains a separate candidate for terrain defects.

Next implement a bounded, per-object publication-based near/far handoff through existing renderer/composition contracts. Prove replacement coverage and restoration after invalidation/eviction; never use distance alone or global convergence to hide proxies. Validate a module-local production consumer and normal showcase screenshots. Resume G11 cancellation/last-consumer lifetime afterward.

## Remaining gates

Finish permanent-error policy, explicit publication and GPU-completion-based retirement/lifetime; migrate step4/8 and water to GPU; replace CPU-dependent test oracles and physically delete CPU-only rendering. Preserve module-local production scenes. Prove edits, pressure and lifecycle, then run locked repeated full-frame/memory workloads and final visual review. G01–G27 remain authoritative; no completion claim.
