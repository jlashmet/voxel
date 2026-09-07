# GPU renderer production restoration

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through GPU rendering, physically delete retired CPU-only rendering, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable result under [tasks.md](tasks.md). Preserve authoritative CPU storage/generation/collision/simulation and GPU host orchestration. No hidden content, weaker budgets or reduced distance. Latest user steering: accept imperfect water appearance for now and prioritize performance.

Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`, base `73989d7ac`. Local harness/tests/screenshots authorized. Last requested push: `origin/fixes/agent-1` at `64b2921a3`; current work stays local.

## Retained results

All solid source steps1/2/4/8 have GPU implementations. GPU water is active and its CPU cache/job/shader path is deleted. Full solid retirement, production coverage and visual acceptance remain open.

## Current GPU faceted merge port

`c161ac401` moved final solid frustum/LOD selection and draw compaction onto GPU with persistent candidate inputs.29 focused tests and the48s module passed.180s Showcase measured198/142 FPS stationary/walking, CPU4.65/6.855ms;631 missing-visible chunks and880 allocation failures/evictions remain. Screenshots retain castle but terrain/proxy/vegetation finish remains unacceptable. These are single-run diagnostics, not benchmark acceptance. Far handoff is conservative and may retain proxies longer. CPU source demand, moving-camera readiness scans, water visibility, API submission and retired solid backend removal remain.

H1: excess unmerged regular faceted geometry drives page pressure and raster cost. H2: source-mirror recovery and streaming, rather than geometry volume, dominate. CPU `FacetedMergeJob` merges equal packed face attributes; regular GPU `VoxelBrickMesher` deliberately emitted one quad per exposed cell. Uniform64×64 face:1 versus4,096 quads, not a whole-scene measurement. Step8 already has bounded4×4 merging.

Selected port: one GPU workgroup classifies each64×64 plane tile into16KiB group scratch, then the group leader greedily merges equal packed attributes. Count and write use identical rectangle traversal, retaining occupied boundary, material/style/coating identity, normals and winding. Larger extraction grids tile without increasing scratch or rejecting supported inputs. All immediate/recorded/batched dispatch sites use plane-group counts. CPU authoritative data and geometry budgets remain unchanged.

Validation:57 focused tests passed (20s, no skips), covering flat/material/hole/negative-coordinate geometry, prepared batches, coating/HLOD/arena behavior. Module48s/seven captures passed all lifecycle/edit/far markers, zero missing/fallback; fort intact, prototype diagnostic composition. Fixed Metal indexing/barrier issues and two fixture mistakes; one Burst startup native crash was infrastructure.

Showcase180s/12 captures passed:236/140 FPS stationary/walking versus198/142; CPU4.095/7.00ms. Allocation failures/evictions fell880/880→0/0; missing-visible631→340; publications1174→2446. H1 supported for geometry pressure; walking speed did not improve, so pressure alone was insufficient. Exact sources/hashes/screenshots retained in `gpu-faceted-merge-showcase`. Castle retained; walking terrain holes, sparse vegetation and far finish remain unacceptable. Single-run diagnostics, not repeatable acceptance.

Next discriminating work: audit/split GPU host ownership from CPU meshing/workspace/upload, delete the retired solid renderer and unused arena, and validate the same source/version/publication invariants. Source recovery and moving-camera traversal remain CPU costs; final step8 publication waits19.5s despite no geometry allocation failures. Test whether CPU source orchestration or GPU submission latency is limiting that progress before changing scheduling.

## Remaining gates

Solid CPU renderer removal, GPU coverage/visual fidelity, G11 retirement/error policy, lifecycle/pressure/edit validation, module/integration players and repeated frame/memory workloads. G01–G27 remain incomplete; no completion claim.
