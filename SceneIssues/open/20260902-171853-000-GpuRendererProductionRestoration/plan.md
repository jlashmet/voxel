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
hierarchy edges persist across readiness changes. GPU now supplies missing-build urgency and distance rank; CPU applies feedback,
stamps resident ages and updates membership. Previous checkpoint502 tests/module pass,Showcase231/201FPS,
CPU4.01/4.585ms,414 missing/55 arena failures; walking traversal1.664→.977ms.

## Hypotheses, selected fix and experiments

H1 migration implemented: existing GPU LOD state carries band/frustum/owned bits and bounded
25-bit distance rank. One async readback; staging bounded by candidate capacity, no extra GPU
buffers. Host validates topology/settings and live source generations; admission uses GPU urgency
and distance rank. CPU candidate transport no longer tests bounds. Stationary queries reuse
feedback; unchanged inactive candidates skip application. Host age stamps reuse GPU tags.
Rejected builds explicitly requeue. Far replacement hierarchy optimization previously gave350/219FPS.

Initial full feedback application cost~1ms. Query reuse/ranking reduced it, but revised Showcase
330/265FPS still had523missing/1532evictions; near-first order alone did not explain the churn.

H2 confirmed: pre-existing GPU vertex reclamation leaks capacity. Temporary standalone accounting
(gpu-demand-arena-diagnostic,180s/12captures/exit0) found17,315 of25,197 vertex pages unaccounted
for, while index pages balanced exactly and GPU live handles equaled CPU residents. Diagnostic
blocking snapshots removed; this run is not FPS evidence.

Three focused GPU tests failed: reclaiming52 retired vertex pages restored only one. Replaced
buffer-subscript post-increment with explicit local free counts and final stores in reclamation.
All three pass16 multi-handle cycles with full counts/unique IDs. Pending multi-page supersession
also conserves capacity.518 Rendering tests pass (19s). Module48s/seven captures/exit0 passes all
markers,262.5MB,framep95/p99 .820/.910ms. Clean Showcase180s/12captures/exit0:332/256FPS,
CPU2.885/3.51ms,331missing,zero allocation failures/evictions,3913publications/coarse21,oldest4.03s.
Exact source hashes match;74.9s/149.9s screenshots reviewed. Stationary regresses350→332; walking
improves219→256 with more retained geometry. No budget/retirement changes;400FPS remains unmet.
Next profile remaining far handoff/submission costs and reduce host feedback application (~.6ms
when applied), preserving coverage. GPU far handoff, pressure eviction and helper cleanup remain;
128 draw buckets are unprofiled.

## Remaining gates

400FPS, startup pop-in, CPU helper retirement, GPU coverage/visual fidelity, lifetime/pressure,
canonical Kentridge integration and repeated frame/memory workloads. Castle silhouette persists;
terrain gaps/seams/far scenery/vegetation remain unacceptable. Module fort is prototype quality,
behavioral evidence only. G01–G27 remain incomplete.
