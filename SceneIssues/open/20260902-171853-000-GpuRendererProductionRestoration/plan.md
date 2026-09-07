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

Incremental demand checkpoint `65b560afc`:527tests/module pass,Showcase332/285FPS,
CPU2.87/2.90ms,sampled feedback.129ms.350missing/2133residents,oldest21.62s(coarse),zero
allocation failures/evictions; all164liveness samples show no unqueued demand. Coverage remains
unresolved; this is not equivalent-coverage or400FPS acceptance.

H1:128indirect solid submissions impose avoidable host/driver cost, as106empty far draws did.
H2: broader groups increase padded GPU vertex work enough to outweigh the CPU saving.
Experiment:32power-of-two buckets instead of128quarter-power buckets, same selected handles,
index counts, materials, generations, page tables, distances and budgets. GPU maximum index
count still defines each draw and per-instance live counts still guard fetches; padding stays
below2x. No CPU readback or selection is introduced. Compare identical60–90s/120–180s Showcase
windows, CPU/GPU timings and coverage, plus real GPU scatter/raster tests and module captures.

32groups:527tests/module pass;Showcase331/294FPS,353missing,zero allocation failures/evictions,
3798publications. Module p95 .889→.714ms, but stationary GPU diagnostic5.83→7.76ms and no FPS
gain. Test64half-power groups next (<1.5x padding), preserving the600-handle/prefix/bank checks
and shipped solid/water raster regressions.64groups passed527tests/module;Showcase339/288FPS,272missing,zero allocation failures,
4074publications. Keep64 interim; unequal coverage and single runs do not prove a robust gain.

## Next steps and remaining gates

Move conservative far replacement proof to GPU using current publication, discovery and region
residency evidence; preserve unknown/stale/edit guards. Avoid replacing CPU proof with per-batch
compute/empty-draw overhead. Retain the incremental feedback improvement.
Next structural experiment: GPU compacts selected live indices into one hardware index stream
(global physical vertex IDs), then one indexed indirect draw consumes it without chunk padding.
Use Index|Raw storage and five-word arguments. Preserve bank/generation/page retirement guards;
record compaction and draw in graphics-queue order. First prove page remapping, exact triangle
counts, no stale/released geometry, and shipped shader rasterization. A single stream sized to
index-page capacity may fit the currently unused geometry-budget remainder; verify aggregate
allocation at every tier before adding it. Never triple-buffer a full arena or reduce coverage
to fit. Pressure eviction and helper cleanup remain.

400FPS, startup pop-in, coverage/visual fidelity, long-session memory/pressure, canonical Kentridge
integration and repeated workloads remain unproven. Castle silhouette persists; terrain gaps/seams,
far scenery and sparse vegetation remain unacceptable. Module fort/landmark are prototype quality,
behavioral evidence only. G01–G27 remain incomplete.
