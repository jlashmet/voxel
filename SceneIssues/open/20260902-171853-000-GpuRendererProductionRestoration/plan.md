# GPU renderer production restoration

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through GPU rendering, physically delete retired CPU-only rendering, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable result under [tasks.md](tasks.md). Preserve authoritative CPU storage/generation/collision/simulation and GPU host orchestration. No hidden content, weaker budgets or reduced distance. Latest user steering: accept imperfect water appearance for now and prioritize performance.

Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`, base `73989d7ac`. Local harness/tests/screenshots authorized. Last requested push: `origin/fixes/agent-1` at `64b2921a3`; current work stays local.

## Retained results

All solid LODs have GPU implementations. Step8 streaming handles 4,096 mixed sources through a 1,024-slot mirror and rejects edited candidates. Full coverage remains incomplete. Latest Showcase completed 180s/11 captures/exit 0, with 642 missing-visible chunks; reviewed 75.1s/150.1s remains unacceptable. Approximate stationary/walking diagnostics: 138/136 FPS, not accepted benchmarks.

Production water now uses GPU snapshot/count/write/allocation/publication/draw. The installed water mask fixes omitted river material 22. WaterDemo 42s/seven captures passed; river is restored, but planar cascade bands, incomplete surroundings and floating vegetation remain unacceptable.

## Current performance experiment

Water retirement `d66763890` removed CPU cache/job and contiguous shader input. Migrated tests passed (21 EditMode,14 PlayMode,3 architecture), and WaterDemo 42s/seven captures passed. WorldBuilder boundary test was not discovered; not claimed passed. CPU water oracle algorithms are gone; fixed validated digests remain.

User accepts imperfect water and prioritizes performance. Instrumented Showcase disproved GPU draw preparation as the dominant visibility cost: stationary traversal 1.842ms, selection 0.405ms, dispatch 0.013ms. Geometry-only query caching (`5f1d6c666`) reduced traversal to 1.342ms, retaining dynamic publication/demand. 17 tests and the focused production module player passed. Stationary FPS approximately 133→145; incomplete-coverage diagnostics only.

Current fix: own snapshots of the drawable/current-complete LOD input lists and reuse selection only when both match exactly. Mutating or replacing a list entry, including a same-count change, runs the original rebuild. GPU handles and all worker state still update each frame. H1: repeated hash-set reconstruction owns selection cost. H2: exact comparisons cost as much. 18 focused tests passed (14s), including same-list mutation, restored child coverage, physical fallback and removal.

`gpu-lod-selection-reuse` completed 180s/12 captures/exit 0, build 32s. Stationary median selection 0.064ms versus 0.428ms (-85%); CPU 6.545ms, approximately 154 FPS versus 145. H1 supported; H2 falsified. Walking selection 0.058ms versus 0.069ms, but whole-frame CPU 7.41ms/134 FPS versus 7.10ms/141 FPS; no walking improvement established. Streaming/convergence differ across diagnostic runs, so these are not final repeatable benchmarks.

Reviewed exact 74.9s/150.0s: castle retained; existing gaps/procedural hills/sparse surroundings remain unacceptable. Focused module player passed 48s/seven captures/exit 0, including edits/restart/far handoff, zero missing/fallback. Reviewed 18s/42s intact production-rendered diagnostic fixture; prototype composition is not final art acceptance. Keep publication/eviction invalidation correct; do not remove whole-set missing-visible protection. Next: dynamic coordinate bookkeeping, which still dominates visibility even after eliminating repeated geometry/LOD work.

## Remaining gates

Solid CPU renderer removal, GPU coverage/visual fidelity, G11 retirement/error policy, lifecycle/pressure/edit validation, module/integration players and repeated frame/memory workloads. G01–G27 remain incomplete; no completion claim.
