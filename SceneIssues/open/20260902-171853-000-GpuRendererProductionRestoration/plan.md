# GPU renderer production restoration

## Objective and acceptance

Finish GPU-only presentation and reach400 FPS on VoxelShowcase. Preserve canonical integer CPU
world truth, generation/collision/simulation and required host orchestration. No hidden content,
weaker budgets or shorter distance. Startup fill-in and FPS are priorities; imperfect water is
acceptable. Delete retired CPU renderer helpers without losing coverage.

Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`. Local harness,
tests/screenshots authorized. Last requested push fulfilled at `origin/fixes/agent-1` `64b2921a3`;
newer work stays local. Detailed evidence is in tasks.md.

## Verified state

Solid steps1/2/4/8 and water use GPU geometry. CPU workers/workspace/contiguous arena/draw route
are deleted; helper/oracle cleanup remains. GPU resolves canonical metadata/source readiness;
only missing source indices return for upload. Fine dense entries/coarse summaries remain
GPU-resident. CPU generation~14.9s; first400near chunks40.4→23.3s after mirror/lane fixes.

GPU handles bands/frustum, LOD and build rank; host applies feedback and membership updates.
Vertex reclamation loss fixed in `c7d3d5db0`;518tests/module passed with no allocation failures.

## Hypotheses and current experiment

Resident far-instance checkpoint `8b50ca1b8`: GPU transforms/compaction/indirect arguments;
CPU replacement supplies flags.523tests/module pass;Showcase322/271FPS,272missing,zero
allocation failures. Source-service latency remains unresolved.

Incremental demand checkpoint `65b560afc` reduced feedback to~.13ms; no unqueued demand,
but coverage/service latency remains unresolved.

64-group checkpoint `15668edeb`:527tests/module passed,Showcase339/288FPS,272missing,zero allocation failures. Single runs
with unequal coverage do not prove a robust gain.

H1: manual index pulling and chunk padding waste GPU vertex work and host submissions.
H2: extra GPU index-copy bandwidth may outweigh hardware vertex reuse/submission savings.
Experiment now implemented: GPU reserves exact selected index ranges, builds bounded page-copy
records, remaps local indices to physical vertex IDs, and emits five-word arguments. One
Index|Raw stream is written immediately before one indexed draw on the graphics queue. Same
live bank metadata, geometry, materials, ranges and retirement policy; no CPU index readback.
529tests/module passed, including page/bank/removal and shipped shader rasterization. Module
proved one draw;Showcase329/295FPS,310missing,zero allocation failures,4072publications. No
robust overall gain. Next version now bypasses four bucket dispatches and consumes GPU selection
directly;530tests/module passed with one draw. Showcase333/297FPS,229missing,zero allocation
failures,3777publications. Exact captures reviewed; no robust overall gain or400FPS acceptance.

## Remaining validation and next steps

Memory audit: full stream adds~181MB on PC. Geometry payload plus bounded primary metadata/LOD
fits PC; console/mobile aggregate accounting does not fit once metadata is included. Existing
mobile metadata already exceeds the nominal envelope. Do not checkpoint as tier-safe or weaken
budgets. After PC measurement, use bounded tiled scratch and resolve metadata allocation without
reducing resident geometry capacity. Audit artifact: gpu-index-stream-memory-audit.json.

Next move far replacement proof to GPU using current publication/discovery/region evidence.
Discovery prerequisite implemented: versioned512-bit region images,270KB buffered GPU hash/query
map;532tests passed with CPU/GPU differential coverage across lifecycle,negative coordinates,
all bits/LODs and1024-region pressure. Not yet connected to far visibility; no player/FPS claim.
Next bind journal/residency guards and current selected/live geometry to shared GPU far queries
and draw data. Avoid106empty submissions. Pressure eviction and retired CPU
helper cleanup remain; keep incremental demand feedback.

400FPS, startup pop-in, coverage/visual fidelity, long-session memory/pressure, canonical Kentridge
integration and repeated workloads remain unproven. Castle silhouette persists; terrain gaps/seams,
far scenery and sparse vegetation remain unacceptable. Module fort/landmark are prototype quality,
behavioral evidence only. G01–G27 remain incomplete.
