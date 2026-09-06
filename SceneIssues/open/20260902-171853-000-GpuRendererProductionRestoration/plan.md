# GPU renderer production restoration

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through GPU rendering, physically delete retired CPU-only rendering, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable result under [tasks.md](tasks.md). Preserve authoritative CPU storage/generation/collision/simulation and GPU host orchestration. No hidden content, weaker budgets or reduced distance. Latest user steering: accept imperfect water appearance for now and prioritize performance.

Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`, base `73989d7ac`. Local harness/tests/screenshots authorized. Last requested push: `origin/fixes/agent-1` at `64b2921a3`; current work stays local.

## Retained results

All solid LODs have GPU implementations. Step8 streaming handles 4,096 mixed sources through a 1,024-slot mirror and rejects edited candidates. Full coverage remains incomplete. Latest Showcase completed 180s/11 captures/exit 0, with 642 missing-visible chunks; reviewed 75.1s/150.1s remains unacceptable. Approximate stationary/walking diagnostics: 138/136 FPS, not accepted benchmarks.

Production water now uses GPU snapshot/count/write/allocation/publication/draw. The installed water mask fixes omitted river material 22. WaterDemo 42s/seven captures passed; river is restored, but planar cascade bands, incomplete surroundings and floating vegetation remain unacceptable.

## Current performance experiment

Water retirement committed locally as `d66763890`: deleted CPU water cache/job and contiguous shader input; migrated canonical extraction/publication and raster regressions to GPU. Final focused checks: 21 EditMode +14 PlayMode +3 architecture passed. WaterDemo completed 42s/seven captures/exit 0 and was reviewed. The requested WorldBuilder boundary test was not discovered and is not claimed passed. Earlier fixture failures were obsolete CPU-upload/property-access expectations, now corrected; fixed output digests preserve the independently checked retired oracle without retaining its algorithm.

User accepts imperfect water and prioritizes performance. Last Showcase diagnostic: CPU approximately 7.3ms, prepare 3.60ms, visibility 2.08ms across 5,735 candidates. H1: repeated coordinate traversal/LOD selection dominates; missing-visible chunks prevent stationary reuse. H2: the visibility aggregate mostly measures GPU submission/buffer-upload cost instead. Experiment: split existing frame diagnostics and capture the same standalone Showcase workload. Result: `gpu-visibility-perf-breakdown` completed 180s/12 screenshots/exit 0; stationary medians: traversal 1.842ms, selection 0.405ms, water 0.001ms, dispatch 0.013ms. Walking: 1.722/0.066/0.001/0.010ms. H2 falsified for draw preparation; H1 supported. Approximate FPS 133/135, CPU 7.59/7.51ms, still diagnostic/incomplete coverage. Reviewed 75s/150s screenshots: castle present, existing terrain gaps and sparse surroundings remain unacceptable. Next: cache geometric camera classification independently of changing publication state, preserving dynamic demand and LOD coverage.

Inspection confirms any missing-visible chunk disables reuse. Simply removing that guard is unsafe: GPU publication does not advance ReadySetVersion, cached GPU handles need lifetime refresh, and camera projection/voxel-scale changes are absent from the old reuse key. Preserve publication, eviction, new-chunk and edit correctness before changing reuse. No rendering behavior, draw distance, or content is reduced by timing instrumentation.

## Remaining gates

Solid CPU renderer removal, GPU coverage/visual fidelity, G11 retirement/error policy, lifecycle/pressure/edit validation, module/integration players and repeated frame/memory workloads. G01–G27 remain incomplete; no completion claim.
