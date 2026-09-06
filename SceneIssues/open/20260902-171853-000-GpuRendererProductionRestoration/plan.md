# GPU renderer production restoration

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through the GPU backend, physically delete the retired CPU-only renderer, and pursue **1,000 FPS / 1.00ms whole frame**, or the closest repeatable result under [tasks.md](tasks.md). Preserve authoritative CPU storage/generation/collision/simulation and GPU host orchestration. No hidden content, weaker budgets, reduced distance or permanent CPU fallback.

## Execution and results

Local harness/tests/screenshots are authorized. Remote `origin/fixes/agent-1` contains2421192e6; subsequent work is local in `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`.

Prior repairs cover GPU descriptor/layout, allocation, indirect bucket prefix, asynchronous recovery, explicit approval and write-finalization. Geometry/counts never return to CPU authority. CPU step4/8 and water remain; no backend deletion or final performance acceptance.

Current near publication and completed regional discovery now drive per-object far handoff in the same render pass. Unknown/incomplete coverage retains proxies. All14 earlier handoff/lifecycle tests and the48s module passed. Full Showcase remains **unacceptable**, particularly large flat traversal obstructions.

Terrain-only residency omitted summit region(-2,1,0). CPU explicit footprint residency and a200-region bake pass5 focused tests. Normal95s player showed detailed summit and roofed side buildings. Audit composed children/separated vertical spans against the unchanged cap and add a Showcase-owned streaming scene before accepting residency.

Far ramps lost direction and became boxes. Canonical run/direction now feed a bounded10-vertex profile. Simple wedges failed both steep tests; cell-centre interpolation passes all21 geometry/presentation tests. Production WorldBuilder module passed28s/7 captures. Normal180s Showcase passed12 captures/no transaction errors, but149.9s traversal remains obstructed. Coarse support forms, material separation and Composition-owned scene coverage remain open.

## Hypotheses and next experiments

1. **Proven:** a released/disposed context remained in an unsubmitted lane, including its prefix extractor/tables. Both new cancellation regressions failed before the fix. Release/retry now revoke queued records before resource release, compact shared lanes onto a live owner, and reject/prune stale tokens. All8 focused cancellation/lifetime/prepared-GPU tests pass. Module48s edit/handoff/restart passed8 captures. Normal180s Showcase passed12 captures/no transaction errors; settled geometry remains detailed but150s traversal still fails visual acceptance.
2. **Open:** submitted lanes retain neither independent mirror-reader ownership nor all referenced GPU resources. Context disposal releases extractor/tables/readers, reset clears/disposes the mirror, and arena retirement uses four CPU frames. Next reproduce cancellation with a submitted lane, then give batches explicit ownership until actual GPU completion. World epochs must prevent old callbacks from altering new-world ownership; retain resources without frame waits.

## Remaining gates

Finish G11 publication/permanent-error policy, cancellation and last-consumer retirement; migrate step4/8 and water; replace CPU-dependent oracles and delete CPU-only rendering. Resolve all visual defects, validate module/integration players, edits, pressure and lifecycle, then run locked repeated full-frame/memory workloads. G01–G27 remain authoritative; no completion claim.
