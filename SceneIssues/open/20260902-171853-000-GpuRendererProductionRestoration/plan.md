# GPU renderer production restoration

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through the GPU backend, physically delete the retired CPU-only renderer, and pursue **1,000 FPS / 1.00ms whole frame**, or the closest repeatable result under [tasks.md](tasks.md). Preserve authoritative CPU storage/generation/collision/simulation and necessary GPU host orchestration. No hidden content, weaker budgets, reduced distance or permanent CPU fallback. All task gates remain mandatory.

## Execution and material results

User directs local harness/tests and screenshot review; **no further origin pushes**. Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`.

Proven repairs: bounded watchdog accounting, shared44-byte GPU descriptor, common allocator status store, explicit indirect bucket prefix, compatible batch layouts. Malformed step2 caches invalidated earlier geometry-demand estimates. Corrected tracing proved full stationary coverage but real traversal exhaustion (290/1635 requests through175s). Async status recovery now reports rejection and triggers bounded offscreen GPU eviction.

Render-control contract: only asynchronous16-byte status/handle/generation feedback per chunk; no generated geometry/count readback, blocking wait or authoritative-state derivation. Lane scratch remains owned through callback completion.

Automatic publication pump and its compute kernel are deleted locally. Renderer attempts now have unique generations independent of storage versions. Host approval checks slot ownership, source/material/surface/coating versions; cancelled candidates abort by exact identity. Approval-focused tests passed25/25. The48s production module passed initial/traversal/edit/settled/restart with zero fallback or rejection. The180s approval showcase exited0 with11 captures; reviewed60s/165s show terrain bands, grey far geometry and cyan water: **unacceptable**, no performance acceptance.

Next discovered invariant: finalization ignored write counters. Five real-GPU bookkeeping cases (missing/short/overflow writes) all failed before the repair. Finalization now compares written vertex/index totals against the allocated candidate entirely on-device, retires mismatches and returns failed status, preserving old live geometry. All31 focused tests pass, plus4 PlayMode arena regressions (fixtures now acquire handles, explicitly commit, and publish production lookup input). A48s module and180s showcase completed; showcase remains unacceptable. The first strict run shared a generic retry status, so quiet logs did not prove zero write mismatches. The distinct `WriteFailed` diagnostic then passed48s module and180s showcase: zero transaction rejections/exceptions,8/11 captures. Reviewed60s/150s/165s showcase remains unacceptable, including a large featureless foreground surface during traversal. This verifies completion totals, not arbitrary payload correctness.

## Hypotheses and discriminating experiments

1. Incomplete writes explained missing geometry: not observed in the strict180s run (zero reported rejections); count equality cannot prove payload correctness.
2. Writes complete correctly, while traversal admission/eviction churn and coarse presentation cause remaining defects.

Next prove cancellation retains submitted mirror leases until GPU consumption; Release currently drops coverage/readers immediately. Replace four-CPU-frame page retirement with last-consumer completion, then correlate live coverage with pressure. Do not infer visual acceptance from host counters.

## Remaining gates

Finish permanent-error policy, explicit publication and GPU-completion-based retirement/lifetime; migrate step4/8 and water to GPU; replace CPU-dependent test oracles and physically delete CPU-only rendering. Preserve module-local production scenes. Prove edits, pressure and lifecycle, then run locked repeated full-frame/memory workloads and final visual review. G01–G27 remain authoritative; no completion claim.
