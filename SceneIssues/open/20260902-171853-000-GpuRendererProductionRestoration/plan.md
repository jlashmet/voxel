# GPU renderer production restoration

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through GPU rendering, physically delete retired CPU-only rendering, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable result under [tasks.md](tasks.md). Preserve authoritative CPU storage/generation/collision/simulation and GPU host orchestration. No hidden content, weaker budgets or reduced distance. Latest user steering: accept imperfect water appearance for now and prioritize performance.

Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`, base `73989d7ac`. Local harness/tests/screenshots authorized. Last requested push: `origin/fixes/agent-1` at `64b2921a3`; current work stays local.

## Retained results

All solid source steps1/2/4/8 have GPU implementations. GPU water is active and its CPU cache/job/shader path is deleted. Full solid retirement, production coverage and visual acceptance remain open.

## Current GPU cutover

User requests completing the originally planned CPU-to-GPU presentation migration before judging FPS. Alternative traversal-backend research is deferred; water art remains deferred. Authoritative storage, edits, collision and simulation stay CPU-owned.

Baseline `29703c338`: approximately180/147 FPS stationary/walking, CPU5.485/6.79ms. Sparse dirty uploads cut mirror flush0.57→0.006ms. Still643 missing-visible chunks and allocation/eviction churn; no accepted benchmark.

Implemented GPU frustum classification, bottom-up all-eight-child LOD handoff and physical-fallback selection feeding indirect compaction. CPU supplies bounded source/readiness identities and spatial links, never final selected GPU handles. Off-camera owned children satisfy current-view completion; negative coordinates and padded bounds match production. Far handoff uses conservative current-publication proof instead of querying a removed CPU selection. This can retain proxies longer; Showcase distant proxy overlap/blank buildings remain a visual gate.

Initial nine GPU tests passed after fixing a Metal vector-index compile error. Module initially failed because far handoff still queried CPU selection; corrected publication proof passed48s/seven captures, including edits/restart/far restoration. GPU frustum version passed27 tests and module player. First180s Showcase completed12 captures:166/141 FPS, CPU6.00/7.005ms; CPU traversal1.334/1.859ms remained. Screenshots preserve castle but incomplete terrain/proxies remain unacceptable.

Selected follow-up: persistent candidate snapshots keyed by exact camera/planes/scale, slot membership, demand, publication/retirement and ring settings. GPU publications/empty completion now advance readiness revision; toroidal replacement advances membership revision even at unchanged count. GPU compaction enumerates its own selected-handle mask, eliminating per-frame CPU handle upload.29 focused tests passed23s; module48s/seven captures passed, zero fallback/missing/errors. Reviewed42s fixture intact; prototype diagnostic composition. Final180s Showcase passed12 captures:198/142 FPS, CPU4.65/6.855ms; stationary traversal median0ms versus1.334ms, walking1.791ms. Still631 missing-visible and880 allocation failures/evictions; single-run diagnostics, not acceptance. GPU timing6.31/1.58ms does not establish frame-critical GPU attribution. Castle retained; terrain holes/proxy finish remain unacceptable.

H1: repeated source-readiness traversal dominates CPU presentation cost; persistence should remove it between actual changes. H2: geometry volume/residency and GPU raster dominate afterward. New source finding: CPU `FacetedMergeJob` greedily merges compatible planar faces; regular GPU `VoxelBrickMesher` deliberately emits individual cell faces. A uniform64×64 plane is1 versus4,096 quads. Step8 already has bounded greedy merging. Next port the regular faceted merge onto GPU, preserving material/boundary/coating semantics and independent coverage/winding tests, then measure geometry pressure and full-frame cost. Do not assume the illustrative ratio applies to the whole scene.

## Remaining gates

Solid CPU renderer removal, GPU coverage/visual fidelity, G11 retirement/error policy, lifecycle/pressure/edit validation, module/integration players and repeated frame/memory workloads. G01–G27 remain incomplete; no completion claim.
