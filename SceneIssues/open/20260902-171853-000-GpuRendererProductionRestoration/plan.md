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

1. **Proven:** released contexts remained queued with disposed resources. Release/retry now revoke unsubmitted records and compact onto live owners. Submitted batches independently retain source/arena/scratch resources and region readers through ordered completion. All19 earlier focused tests and48s/180s players passed. Native Burst compiler crashes in one test/build attempt remain infrastructure failures, not passes.
2. **Proven:** world replacement/history invalidation cleared the actual GPU directory despite submitted owners. The before-fix directory regression failed. Clear now coalesces behind submission completion, blocks admission/mutation during reset, and resumes recovery afterward. Context coverage/reader cleanup uses captured world epochs; invalid queued/completed requests signal retry. All24 focused checks pass, including actual world replacement during a submitted batch, history invalidation and old-context cleanup. The48s module passed edit/handoff/restart with8 captures; reviewed42s has intact geometry but blockout composition. Full180s Showcase passed12 captures/no exceptions or transaction rejections; reviewed75s/150s. Walking remains obscured by flat surfaces: **unacceptable**.

Next G11 experiments: bound retired worlds under GPU lag and replace four-CPU-frame page retirement with actual last-draw completion. Audit other mutable source resources. No full lifetime claim yet.

## Remaining gates

Finish G11 publication/permanent-error policy, cancellation and last-consumer retirement; migrate step4/8 and water; replace CPU-dependent oracles and delete CPU-only rendering. Resolve all visual defects, validate module/integration players, edits, pressure and lifecycle, then run locked repeated full-frame/memory workloads. G01–G27 remain authoritative; no completion claim.
