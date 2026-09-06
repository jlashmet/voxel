# GPU renderer production restoration

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through GPU rendering, physically delete retired CPU-only rendering, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable result under [tasks.md](tasks.md). Preserve authoritative CPU storage/generation/collision/simulation and GPU host orchestration. No hidden content, weaker budgets or reduced distance. Latest user steering: accept imperfect water appearance for now and prioritize performance.

Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`, base `73989d7ac`. Local harness/tests/screenshots authorized. Last requested push: `origin/fixes/agent-1` at `64b2921a3`; current work stays local.

## Retained results

All solid LODs have GPU implementations. Step8 streaming handles 4,096 mixed sources through a 1,024-slot mirror and rejects edited candidates. Full coverage remains incomplete. Latest Showcase completed 180s/11 captures/exit 0, with 642 missing-visible chunks; reviewed 75.1s/150.1s remains unacceptable. Approximate stationary/walking diagnostics: 138/136 FPS, not accepted benchmarks.

Production water now uses GPU snapshot/count/write/allocation/publication/draw. The installed water mask fixes omitted river material 22. WaterDemo 42s/seven captures passed; river is restored, but planar cascade bands, incomplete surroundings and floating vegetation remain unacceptable.

## Current performance experiment

Water retirement committed locally as `d66763890`: deleted CPU water cache/job and contiguous shader input; migrated extraction/publication/raster regressions. Final checks: 21 EditMode +14 PlayMode +3 architecture passed. WaterDemo completed 42s/seven captures/exit 0 and was reviewed. Requested WorldBuilder boundary test was not discovered; not claimed passed. Fixed digests preserve independently checked retired oracle output without retaining CPU meshing.

User accepts imperfect water and prioritizes performance. H1: repeated coordinate traversal dominates visibility. H2: GPU submission owns the aggregate. `gpu-visibility-perf-breakdown` completed 180s/12 captures/exit 0: stationary traversal 1.842ms, selection 0.405ms, water 0.001ms, dispatch 0.013ms. H2 falsified for draw preparation; H1 supported.

Selected fix: cache only coordinate ring/frustum classification under an exactly unchanged camera query, including planes, position, voxel scale, ring bounds and suspension. Moving queries bypass insertion. Dynamic readiness, demand, edits, publication, LOD selection and last-used updates still execute every frame; new coordinates classify on demand. Cache storage is bounded; capacity misses perform normal classification.

17 focused EditMode cases passed (21s): exact query invalidation, newly streamed coordinates, publication/edits during stationary reuse, and LOD fallback/handoff behavior. `gpu-visibility-geometry-cache` completed 180s/11 captures/exit 0. Stationary traversal 1.342ms (-27%); CPU 6.905ms, approximate FPS 145 versus baseline 133. Walking traversal 1.732ms (baseline 1.722), CPU 7.10ms, approximately 141 FPS. These remain incomplete-coverage diagnostics, not repeatable final benchmarks. Reviewed 75.0s/150.1s: castle retained; existing terrain gaps/procedural hills/sparse surroundings remain unacceptable. Focused production module player passed 48s/seven captures/exit 0, including traversal, edits, restart and far handoff; zero missing/fallback. Reviewed 18s/42s: intact production-rendered diagnostic fixture, prototype composition, not final art acceptance.

Do not remove the whole-set missing-visible guard: GPU publication lacks ReadySetVersion invalidation and cached GPU handles need lifetime refresh. Next performance investigation: dynamic per-coordinate bookkeeping/LOD selection; retain full publication and eviction correctness.

## Remaining gates

Solid CPU renderer removal, GPU coverage/visual fidelity, G11 retirement/error policy, lifecycle/pressure/edit validation, module/integration players and repeated frame/memory workloads. G01–G27 remain incomplete; no completion claim.
