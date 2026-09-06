# GPU renderer production restoration

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through GPU rendering, physically delete retired CPU-only rendering, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable result under [tasks.md](tasks.md). Preserve authoritative CPU storage/generation/collision/simulation and GPU host orchestration. No hidden content, weaker budgets or reduced distance. Latest user steering: accept imperfect water appearance for now and prioritize performance.

Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`, base `73989d7ac`. Local harness/tests/screenshots authorized. Last requested push: `origin/fixes/agent-1` at `64b2921a3`; current work stays local.

## Retained results

All solid LODs have GPU implementations. Step8 streaming handles 4,096 mixed sources through a 1,024-slot mirror and rejects edited candidates. Full coverage remains incomplete. Latest Showcase completed 180s/11 captures/exit 0, with 642 missing-visible chunks; reviewed 75.1s/150.1s remains unacceptable. Approximate stationary/walking diagnostics: 138/136 FPS, not accepted benchmarks.

Production water now uses GPU snapshot/count/write/allocation/publication/draw. The installed water mask fixes omitted river material 22. WaterDemo 42s/seven captures passed; river is restored, but planar cascade bands, incomplete surroundings and floating vegetation remain unacceptable.

## Current performance experiment

Water retirement `d66763890` removed CPU cache/job and contiguous shader input. Migrated tests passed (21 EditMode,14 PlayMode,3 architecture), and WaterDemo passed. WorldBuilder boundary test was not discovered; not claimed passed. CPU water oracle algorithms are gone; validated fixed digests remain.

User accepts imperfect water and prioritizes performance. Measurements disproved GPU submission as the dominant visibility cost. Geometry-only query caching (`5f1d6c666`) reduced stationary traversal 1.842→1.342ms. Exact LOD input reuse (`6516517c6`) reduced selection 0.428→0.064ms. Focused tests and module players passed; screenshots retain existing coverage/art defects. Approximate stationary FPS rose 133→154; diagnostics only.

Current fix returns drawable/current-complete facts directly from worker collection, replacing repeated diagnostic-counter reads in the scheduler. Counters, build demand, stale drawable fallback, empty-generation handling, off-frustum prefetch and last-used updates remain intact. 28 focused tests passed (17s), including seven direct-result states plus admission, geometry cache and LOD regressions.

`gpu-visibility-direct-result` completed 180s/12 captures/exit 0, build 33s. Traversal medians: stationary 1.256ms versus 1.317ms; walking 1.712ms versus 1.770ms. CPU 6.395/7.11ms, approximately 155/140 FPS. H1 (handoff bookkeeping matters) shows only modest savings; H2 (other chunk-state work dominates) remains supported. These are single-run, incomplete-coverage diagnostics, not repeatable final benchmarks. Reviewed exact 74.9s/150.0s: castle retained; existing terrain gaps/procedural hills/sparse surroundings remain unacceptable. Focused module player passed 48s/seven captures/exit 0, including edits/restart/far handoff, zero missing/fallback. Reviewed 18s/42s intact production-rendered diagnostic fixture; prototype composition, not final art acceptance.

Next discriminating experiment: trace GPU allocation failures, eviction count, publication, residency and arena-relief CPU time together. Scheduler GPU-failure relief evicts offscreen entries independently of the older CPU convergence guard. H1: prefetch allocation/eviction churn sustains CPU cost and delays coverage. H2: normal moving-window retirement plus insufficient allocation throughput explains residency changes. Do not change eviction policy without distinguishing these causes; preserve visible geometry and source generations.

## Remaining gates

Solid CPU renderer removal, GPU coverage/visual fidelity, G11 retirement/error policy, lifecycle/pressure/edit validation, module/integration players and repeated frame/memory workloads. G01–G27 remain incomplete; no completion claim.
