# GPU renderer production restoration

## Objective and acceptance

Finish GPU-only rendering and reach400 FPS on VoxelShowcase. Preserve canonical integer CPU world
truth, generation/collision/simulation and required host orchestration. No hidden content, weaker
budgets or shorter distance. Startup fill-in and FPS are priorities; imperfect water is acceptable.
Delete retired CPU renderer helpers without losing coverage.

Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`. Local harness,
tests/screenshots authorized. Last requested push fulfilled at `origin/fixes/agent-1` `64b2921a3`;
newer work stays local. Detailed results are in tasks.md.

## Verified state

All solid steps1/2/4/8 and water use GPU geometry. CPU workers/workspace/contiguous arena/draw
route are deleted; helper/oracle cleanup remains. GPU resolves canonical air/uniform/water metadata
and mixed source readiness; only missing indices return for upload. Fine dense entries and coarse
summaries stay GPU-resident. CPU coverage polling is gone; world/version/epoch guards remain.
Cross-brick64x64 face merging and packed source portions reduced extraction cost. CPU generation
still~14.9s. Mirror/lane fixes removed full pinned scans and source-reader starvation; first400
near chunks improved40.4→23.3s.

GPU handles render bands/frustum and LOD selection. Candidate lists persist across camera motion;
hierarchy edges persist across readiness changes. CPU still handles missing-build urgency,
resident ages and membership updates. Previous checkpoint502 tests/module pass,Showcase231/201FPS,
CPU4.01/4.585ms,414 missing/55 arena failures; walking traversal1.664→.977ms.

## Hypotheses, selected fix and experiments

H1: pending-build classification and host cache aging still limit moving frames. Next migrate
these to bounded GPU demand feedback with membership/version/epoch guards, without full CPU
camera-band scans or synchronous readback. CPU world truth stays unchanged.

H2 confirmed: far replacement checks outside scheduler timing dominated stationary CPU rendering.
Native sample found2549/3498 main-thread samples in scripted rendering (managed symbols unresolved).
Scheduler~.34ms versus main~3.8ms falsified scheduler-only attribution. Temporary standalone
instrumentation measured far preparation1.642ms at60–90s; diagnostic removed after capture.

Selected fix: bounded coarse-first coverage proof, descending only where current coarse proof is
absent. Same fine-cell union semantics; no allocation, four recursion levels. Largest fully coarse
coverage needs8 proofs versus16,384.256 seeded mixed-level/negative-bound differential cases
compare original oracle.503/503 Rendering tests pass (22s). Module48s/seven captures/exit0 passes
all markers including edit/restart/far restoration,262.5MB,framep95/p99 .821/.910ms.
Full Showcase180s/12captures/exit0:350/219FPS,CPU2.71/4.10ms,363missing,12arena failures,
coarse20,oldest30.3s. Exact source hashes match;74.8s/149.9s screenshots reviewed. GPU demand and
far handoff migration remain.128 draw buckets are an unproven GPU-cost hypothesis.

## Remaining gates

400FPS, startup pop-in, CPU helper retirement, GPU coverage/visual fidelity, lifetime/pressure,
canonical Kentridge integration and repeated frame/memory workloads. Castle silhouette persists;
terrain gaps/seams/far scenery/vegetation remain unacceptable. Module fort is prototype quality,
behavioral evidence only. G01–G27 remain incomplete.
