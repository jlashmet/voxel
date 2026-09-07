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

GPU handles bands/frustum, LOD and build urgency/distance rank. Candidate lists persist across
camera motion; topology persists across readiness changes. Host applies feedback, stamps ages
and updates membership. Far hierarchy optimization gave350/219FPS. GPU demand migration exposed
vertex reclamation losing51 of52 released pages; explicit counters restored capacity. Checkpoint
`c7d3d5db0`:518tests/module pass,Showcase332/256FPS,331missing,zero allocation failures/evictions.
No budget or retirement-delay changes.

## Hypotheses and current experiment

Resident far-instance checkpoint `8b50ca1b8`: transforms persist on GPU, GPU compacts instances
and writes indirect arguments. Removed CPU matrix submission. Empty draws initially regressed
FPS; skipping fully replaced batches restored it.523tests/module pass; Showcase322/271FPS,
CPU2.89/3.305ms,272missing,zero allocation failures/evictions. Exact captures reviewed. CPU
replacement still supplies flags; oldest coarse build20.6s, source-service latency unresolved.

H1: full host demand application repeats publication/queue/accounting work for unchanged GPU
classifications (~.607ms walking). H2: readiness churn forces most feedback to refresh regardless,
or dictionary/rank transport dominates. Compare same60–90s/120–180s Showcase windows and new
full-refresh/coordinate-refresh/rank counters.

Implemented incremental GPU feedback: stable live metadata uses classification deltas, pending
nodes receive rank-only updates, current complete nodes skip unchanged feedback. Topology,
settings, readiness or a newer host image than the readback require full live checks. Worker
accounting retains per-coordinate contributions; rank-only updates cannot create/change demand.
Counters survive metadata collection, and removal subtracts contributions. Existing GPU age tags
and current-generation admission/retry rules remain. No new GPU buffers or distance decisions.

527Rendering tests pass (24s), including real GPU delayed-readiness/delta checks,80-cycle
incremental/full parity, rank-driven admission and a queue-liveness diagnostic. Final production
module48s/seven captures/exit0 passes all markers,262.6MB,framep95/p99 .889/.914ms.
Two Showcases180s/12captures/exit0:332/282 and332/285FPS. Repeat CPU2.87/2.90ms; sampled nonzero
feedback median.129ms,2338full resets/11894accepted. All164liveness samples unqueued=0.
However350missing versus parent272,3380publications/2133residents and oldest21.62s(coarse) leave
coverage/service latency unresolved. Zero allocation failures/evictions. Source hashes and exact
74.9s/149.9s captures reviewed. Reduced CPU work is not400FPS or equivalent-coverage acceptance.

## Next steps and remaining gates

Move conservative far replacement proof to GPU using current publication, discovery and region
residency evidence; preserve unknown/stale/edit guards. Avoid replacing CPU proof with per-batch
compute/empty-draw overhead. Retain the incremental feedback improvement.
Next test128solid indirect submission overhead with fewer GPU size buckets, preserving every
handle/index. Pressure eviction and helper cleanup remain.

400FPS, startup pop-in, coverage/visual fidelity, long-session memory/pressure, canonical Kentridge
integration and repeated workloads remain unproven. Castle silhouette persists; terrain gaps/seams,
far scenery and sparse vegetation remain unacceptable. Module fort/landmark are prototype quality,
behavioral evidence only. G01–G27 remain incomplete.
