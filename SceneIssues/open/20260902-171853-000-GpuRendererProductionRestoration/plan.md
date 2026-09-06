# GPU renderer production restoration

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through GPU rendering, physically delete retired CPU-only rendering, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable result under [tasks.md](tasks.md). Preserve authoritative CPU storage/generation/collision/simulation and GPU host orchestration. No hidden content, weaker budgets or reduced distance. Full GPU functionality precedes optimization.

Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`, base `54902c4b8`. Local harness/tests/screenshots authorized. Last requested push: `origin/fixes/agent-1` at `64b2921a3`; current work stays local.

## Retained results

All solid LODs have GPU implementations. Step8 streaming handles 4,096 mixed source bricks through a 1,024-slot mirror and rejects edited candidates. The 180s Showcase run completed with zero directory refusals but 567 missing-visible chunks. Reviewed output remains unacceptable. Two step8 workers cannot monopolize all four count lanes; no evidence justifies speculative scheduling changes. CPU overhead remains elevated; optimization follows functionality.

GPU water kernels and paged shader passes passed actual Metal meshing/raster tests. Temporary CPU comparison oracles remain pending deletion.

## Current migration and experiment

Scheduler/discovery/render pass now select `GpuWaterSurfaceChunkCache`. It retains immutable voxel snapshots, counts and writes eight-brick slices, uses a separately owned GPU page arena within the existing geometry capacities, and receives only status/identity feedback. Version-checked publication commits GPU candidates; GPU-generated indirect arguments address live geometry. Teardown retains buffers through completion, including final water draws. The retired CPU cache still exists only for unmigrated tests; physical deletion remains required.

Initial runtime tests: four passed, covering canonical publication/arguments, stale rejection, same-chunk occluder edits and disposal. WaterDemo completed 30s/five captures/exit 0. Reviewed 8.3s/26.3s show lake and cascade but a dry river, missing surroundings and floating vegetation: unacceptable.

H1: inherited hardcoded water IDs omit river material 22. H2: paged shader addressing loses river geometry. Scene authoring uses 22; inherited cache classified only 11/16, proving H1. Selected fix: discover and mesh the installed presentation catalogue's water mask, captured immutably per transaction. Full geometry/indirect publication of additional material 22 now passes. Expanded spray bounds cover the canonical six-voxel extension.

`gpu-water-runtime-catalogue.xml`: 16 passed/exit 0/17s (five cache, five mesher, six draw/compaction cases). The 42s WaterDemo rerun passed with seven captures; reviewed 26.3s restores river/feeder/receiver water, while 32.3s waterfall sheets remain visually unacceptable. Showcase completed 180s/11 captures/exit 0; reviewed 75.1s/150.1s remains unacceptable, with 642 missing-visible chunks. Approximate stationary/walking rates are 138/136 FPS, not an accepted benchmark. Exact source copies/hashes accompany captures. Next migrate/delete the retired CPU water tests/cache, then address remaining GPU coverage and visual defects.

## Remaining gates

Finish GPU coverage/visual quality, delete CPU-only rendering and temporary oracles, complete G11 retirement/error policy, and validate lifecycle, pressure, edits, module/integration players and repeated frame/memory workloads. G01–G27 remain incomplete; no completion claim.
