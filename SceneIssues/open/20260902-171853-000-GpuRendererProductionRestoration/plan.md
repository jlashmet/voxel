# GPU renderer production restoration

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through GPU rendering, physically delete retired CPU-only rendering, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable result under [tasks.md](tasks.md). Preserve authoritative CPU storage/generation/collision/simulation and GPU host orchestration. No hidden content, weaker budgets or reduced distance. Latest user steering: accept imperfect water appearance for now and prioritize performance.

Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`, base `73989d7ac`. Local harness/tests/screenshots authorized. Last requested push: `origin/fixes/agent-1` at `64b2921a3`; current work stays local.

## Retained results

All solid source steps1/2/4/8 have GPU implementations. GPU water is active and its CPU cache/job/shader path is deleted. Full solid retirement, production coverage and visual acceptance remain open.

## Current CPU arena/draw retirement

`86733d373` removed CPU worker phases/workspace allocation:469 rendering tests and the module
passed; module allocated memory687→262MB. Earlier GPU face merging measured236/140 FPS
stationary/walking, zero allocation failures/evictions and340 missing-visible chunks. Terrain
holes/far content remain unacceptable. No repeated performance acceptance.

H1: the unused contiguous CPU arena and draw staging still reserve memory/host work. H2: source
recovery and moving-camera traversal dominate steady performance despite that removal.

Selected change: GPU-paged-only SmoothSurface shader and render pass; remove contiguous CPU
Entry upload/draw state, scheduler arena and upload/lease-pressure loops. Preserve GPU capacity
at the existing three-quarter share; leave the freed allocation uncommitted. Rename the host to
`GpuSolidChunkCache`, preserving its asset GUID and admission/version/publication logic. Retire
old editor Kentridge capture utilities that rebuilt CPU meshes; executable player gates use the
existing ShowcasePlayerBuild harness. Canonical CPU storage/generation/collision untouched.

Validation:468 rendering EditMode tests passed(26s), plus the migrated material-distance
PlayMode test(13s). Fixed a leftover staging-array clear and stale architecture expectations.
Two retired CPU lease/upload pressure fixtures were removed; GPU transaction tests cover
exhaustion/retry and preservation, but full GPU pressure-player coverage remains a G11 gate.
Module48s/seven captures passed all lifecycle/edit/far markers; CPU arena committed/used0,
fort intact, prototype composition.19 finalizer warnings persist.

Showcase180s/11 captures passed:238/142 FPS stationary/walking, CPU4.06/7.065ms;330 missing,
zero allocation failures/evictions. Previous236/140: no significant speed improvement established.
Reviewed75s/150s: castle retained; terrain holes/far/vegetation finish remains unacceptable.
Exact sources/hashes/windows in `gpu-draw-retirement-showcase`. Source recovery/traversal and
20.9s coarse publication latency remain unresolved; the removal chiefly saves memory.

Next: remove remaining standalone CPU workspace,
jobs/oracles and transitional GPU-to-CPU arena bridge. Direct GPU retained-profile suppression/
backing regression is needed before removing its last CPU predicate oracle. Both prior module
runs had19 ComputeBuffer finalizer warnings; fix lifetime ownership under G11.

## Remaining gates

Solid CPU renderer removal, GPU coverage/visual fidelity, G11 retirement/error policy, lifecycle/pressure/edit validation, module/integration players and repeated frame/memory workloads. G01–G27 remain incomplete; no completion claim.
