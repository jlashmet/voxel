# GPU renderer production restoration

## Objective and acceptance

Finish GPU-only rendering and reach400 FPS on VoxelShowcase. Preserve canonical CPU world truth,
generation/collision/simulation and necessary host orchestration. Delete retired CPU renderer
helpers without losing behavioral coverage. No hidden content, weaker budgets or shorter distance.
Startup castle/house fill-in and FPS are priorities; imperfect water is acceptable for now.

Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`. Local harness,
tests and screenshots authorized. Last requested push fulfilled at `origin/fixes/agent-1`
`64b2921a3`; newer work remains local. Details/evidence are in tasks.md.

## Verified state

All solid steps1/2/4/8 and water use GPU geometry. CPU worker phases, workspace, contiguous arena
and draw route are deleted; legacy helper/oracle cleanup remains. Water quit drain removed19
finalizer warnings. Initial nearby discovery priority improved first publication5.1→2.1s after
CPU generation, which still takes14.9s.

GPU coarse readiness now generates coordinates and interprets canonical Storage block metadata:
air/uniform/water decode directly; only mixed payloads require current non-pending directory keys.
RegionReadView exposes bounded copies without per-brick transformation. Host validates region
residency/version, holds source demand/readers correctly, and receives only bounded missing-source
indices. Each lane reuses16KiB CPU and128KiB GPU metadata scratch. Mirror/arena budgets unchanged.
Near-ready work takes priority, with coarse service after eight near reservations.

Coarse GPU merging now spans64x64 subcell tiles across bricks,16KiB shared mask and bounded plane
dispatches. Count/write agree on material, exposed faces, winding and coverage. Two adjacent bricks
emit6 rather than10 quads;3D/tile-boundary tests prove larger merged coverage.

562/562 Rendering+Storage EditMode tests pass20s. Module48s/seven captures passes all lifecycle,
edit/far gates with zero missing/errors; frame p95/p99 .818/.892ms. Headless Storage copy API uses
module-local EditMode coverage; Rendering owns the production validation scene. Source hashes match.
Full180s/12 captures:222/134 FPS stationary/walking,442 missing-visible,259 allocation failures/
evictions. Directory refusals175,837→82,993 and recovery publications11.56M→8.63M; failures return
late. Reviewed castle retained; terrain seams/far/vegetation remain unacceptable. Not400FPS acceptance.

## Next experiment and fix

H1: fine CPU Covers and per-brick uploads dominate remaining source pressure. Final step4 request
spent536 polls/4.07s; mixed slots40,270/40,270. H2: camera-driven CPU visibility/hierarchy work
limits walking even after source migration (~1.9ms traversal plus periodic1.3ms rebuild).

Next reuse canonical GPU source resolution to fill fine dense-cache entries and report missing
mixed payloads. Replace fine CPU Covers; retain whole fine demand against eviction, but hold GPU
readers only during submissions so missing sources can recover. Validate epoch/version rejection,
cancellation, pressure and dense-cache parity; then module/Showcase evidence. Follow with GPU
visibility/band/hierarchy migration and repeated locked FPS. No deeper queues or weakened budgets.

## Remaining gates

Startup pop-in, CPU helper retirement,GPU coverage/visual fidelity,G11 lifetime/pressure,canonical
Kentridge integration and repeated frame/memory workloads. G01–G27 incomplete.
