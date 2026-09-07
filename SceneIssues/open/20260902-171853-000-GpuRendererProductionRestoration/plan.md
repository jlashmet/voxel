# GPU renderer production restoration

## Objective and acceptance

Finish GPU-only rendering and reach400 FPS on VoxelShowcase. Preserve canonical CPU world truth,
generation/collision/simulation and required host orchestration. Delete retired CPU renderer
helpers without losing coverage. No hidden content, weaker budgets or shorter distance. Startup
fill-in and FPS are priorities; imperfect water is acceptable for now.

Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`. Local harness,
tests/screenshots authorized. Last requested push fulfilled at `origin/fixes/agent-1` `64b2921a3`;
newer work stays local. Detailed results are in tasks.md.

## Verified state

All solid steps1/2/4/8 and water use GPU geometry. CPU worker phases, workspace, contiguous arena
and draw route are deleted; legacy helper/oracle cleanup remains. Water quit drain removed19
finalizer warnings. Initial nearby discovery improved first publication5.1→2.1s after generation;
CPU generation still14.9s.

`2c4a5aedc` added coarse GPU source readiness from canonical block metadata and64x64 cross-brick
face merging.562 Rendering+Storage tests/module pass; Showcase222/134 FPS,259 allocation failures.

Current fine migration removes production CPU Covers calls. GPU builds dense entries from canonical
air/uniform/water metadata and current mixed payloads; only missing mixed indices return for upload.
Fine demand protects accumulated slots; readers protect GPU submissions only. Context admission
uses an extraction slot without prematurely blocking missing uploads. Count descriptors preserve
prepared dense entries; cancellation/world/version/epoch rejection remain. Source portions pack
complete XY planes up to1024 sources and64 metadata rows, reusing128KiB GPU/16KiB CPU scratch.
Typical nearest cube requires two rather than ten preparation submissions. Legacy Covers is test-only.

488/488 Rendering tests pass20s, including realStorage fine geometry at steps1/2/4,dense packing,
edits and lifecycle. Packed module48s/seven captures passes all markers,zero missing/errors;
frame p95/p99 .817/.918ms,262.4MB. Full180s/12 captures:224/184 FPS,CPU3.89/5.245ms. CPU
coverage polls0,directory failures0,geometry allocation failures/evictions0. Recovery publications
8.63M→347,749;host-ready entries894,513→40,270. Source hashes match. Castle retained; terrain
seams/far/vegetation unacceptable. Missing-visible456,coarse publications37→13,oldest coarse21.17s.
Not400FPS or complete readiness; mixed-slot pressure remains307,479 no-slot attempts.

## Next experiment and fix

H1: waiting requests protect too much mixed source data. BeginPersistentStage registers full fine
demand before TryBeginExtraction can refuse admission. Move demand after admission; test denied
requests retain nothing, then measure whether admitted footprints need mixed-slot arbitration.
H2: camera-driven visibility/hierarchy CPU work limits walking (~1.73ms traversal plus periodic
rebuild). Migrate that next. Static FPS barely changed despite recovery often0ms: profile draw
cost separately;128 procedural buckets/manual indexed fetch may matter, but indexed-draw savings
are unproven. No memory increases, hidden geometry or weaker budgets.

## Remaining gates

Startup pop-in, CPU helper retirement,GPU coverage/visual fidelity,G11 lifetime/pressure,canonical
Kentridge integration and repeated frame/memory workloads. G01–G27 incomplete.
