# GPU renderer production restoration

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through GPU rendering, physically delete retired CPU-only rendering, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable result under [tasks.md](tasks.md). Preserve authoritative CPU storage/generation/collision/simulation and GPU host orchestration. No hidden content, weaker budgets or reduced distance. Latest user steering: accept imperfect water appearance for now and prioritize performance.

Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`, base `73989d7ac`. Local harness/tests/screenshots authorized. Last requested push: `origin/fixes/agent-1` at `64b2921a3`; current work stays local.

## Retained results

All solid LODs have GPU implementations. Step8 streaming handles 4,096 mixed sources through a 1,024-slot mirror and rejects edited candidates. Full coverage remains incomplete. Latest Showcase completed 180s/11 captures/exit 0, with 642 missing-visible chunks; reviewed 75.1s/150.1s remains unacceptable. Approximate stationary/walking diagnostics: 138/136 FPS, not accepted benchmarks.

Production water now uses GPU snapshot/count/write/allocation/publication/draw. The installed water mask fixes omitted river material 22. WaterDemo 42s/seven captures passed; river is restored, but planar cascade bands, incomplete surroundings and floating vegetation remain unacceptable.

## Current performance experiment

Water CPU cache/job and shader path are deleted (`d66763890`). Focused water tests/player passed; WorldBuilder boundary test was not discovered. Geometry classification caching, exact LOD selection reuse and direct visibility results reduced stationary visibility CPU cost; tests/module players passed. Full solid retirement and coverage remain open.

User accepts imperfect water and prioritizes performance. Existing PREPARESECTIONS logs showed median worst-frame arena relief 0.000ms (121 samples, max1.102ms): steady eviction-scan dominance falsified. `batchArenaWait` means fence backpressure, not allocation exhaustion. Added mirror CPU phase timing and actual failure/eviction counters.

`gpu-mirror-cpu-breakdown` completed 180s/12 captures/exit0: mirror flush medians 0.567/0.570ms stationary/walking. Inspection found dirty min/max scans covering almost all 1,048,576 directory slots for sparse hashed updates. H1: table scanning dominates flush. H2: transfer/scatter dominates. Selected fix tracks unique dirty indices in fixed arrays; flush visits each changed slot once, preserving final values, clear and backward-shift deletion. Adds bounded CPU index storage (~4.2MiB at Showcase capacities); GPU capacities/budgets unchanged.

15 focused GPU tests passed (18s), including sparse three-entry flush in a million-entry table, repeated writes, GPU material lookup, collision-chain relocation, pressure protection and clear/lifetime behavior. `gpu-mirror-dense-dirty` completed 180s/12 captures/exit0, build27s. Flush medians 0.0055/0.0155ms; H1 supported, H2 falsified as dominant. Approximate FPS 180/147 versus156/140; CPU 5.485/6.79ms. Still single-run incomplete-coverage diagnostics. Reviewed baseline/fixed74.9s and fixed149.9s: castle retained, existing terrain gaps/procedural hills/sparse surroundings remain unacceptable. Focused module player passed48s/seven captures/exit0, including edits/restart/far handoff, zero missing/fallback. Reviewed18s/42s intact production-rendered fixture; prototype composition, not art acceptance.

Pressure remains: baseline ended3,800 failures/1,906 evictions; fixed run1,645/1,239, both643 missing-visible. Cheap eviction scans do not falsify expensive downstream rebuild churn. Next experiment distinguishes actual GPU page exhaustion/retirement from per-chunk allocation limits, correlating failures with live residency. Preserve visible geometry and generation correctness before changing relief policy.

## Remaining gates

Solid CPU renderer removal, GPU coverage/visual fidelity, G11 retirement/error policy, lifecycle/pressure/edit validation, module/integration players and repeated frame/memory workloads. G01–G27 remain incomplete; no completion claim.
