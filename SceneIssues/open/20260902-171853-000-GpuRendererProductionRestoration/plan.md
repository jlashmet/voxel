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

H1: repeated far transform construction, registration and matrix copying adds CPU overhead.
H2: conservative coverage proof dominates instead. Migrate resident submission, then compare
identical stationary60–90s/walking120–180s windows before moving coverage proof.

Implemented persistent GPU object/inverse transforms, visibility compaction and indirect submesh
counts. Unchanged source queries reuse batches. Configured65536-instance ceiling rejects overflow
without hiding content. Shader retains production URP forward lighting. CPU replacement proof
still supplies visibility flags; GPU owns compaction. Removed the old matrix submission route.

Initial522tests and standalone landmark28s/seven captures pass. Initial Showcase275/245FPS
regressed:106fully replaced batches still submitted empty indirect draws. Suppressed empty
submissions;523tests now pass (22s), including compaction1/65/1025instances, submesh arguments,
nonuniform transforms, repeated lifecycle/source updates, empty submission and capacity rejection.
Production module48s/seven captures/exit0 passes all seven markers,262.6MB,framep95/p99 .829/.974ms.
Corrected Showcase180s/12captures/exit0:322.26/271.36FPS,CPU2.89/3.305ms,272missing,
zero allocation failures/evictions,3933publications/2454resident candidates. Source hashes match;
74.9s/149.9s reviewed. Stationary did not improve versus332FPS; walking improved versus256FPS
in this run. Transform work alone does not close the gap; coverage proof/feedback remain.
Oldest active coarse build20.6s; startup/source-service latency still needs work.

## Next steps and remaining gates

Move conservative far replacement proof to GPU using current publication, discovery and region
residency evidence; preserve unknown/stale/edit guards. Avoid replacing CPU proof with per-batch
compute/empty-draw overhead. Reduce host feedback application (~.6ms on feedback frames).
128solid draw buckets remain unprofiled; pressure eviction and helper cleanup remain.

400FPS, startup pop-in, coverage/visual fidelity, long-session memory/pressure, canonical Kentridge
integration and repeated workloads remain unproven. Castle silhouette persists; terrain gaps/seams,
far scenery and sparse vegetation remain unacceptable. Module fort/landmark are prototype quality,
behavioral evidence only. G01–G27 remain incomplete.
