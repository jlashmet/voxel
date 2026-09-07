# VoxelShowcase GPU correctness and performance handoff

## Observed state and scope

User reports old performance problems and missing geometry. Local master `1d3e9d3af` includes
PR #316 (`619a3c230`), but 317 PR-changed Assets/tools files initially matched pre-merge content.
Restored all 338 PR paths; subsequently moved two obsolete untracked CPU-water sources and their
metadata into a backup. Fresh successful Unity compilation is **unverified**. Local backup:
`/Users/jlashmet/voxel-before-gpu-restore-n2l5g6qz` (not portable evidence).

The [previous checkpoint](../20260902-171853-000-GpuRendererProductionRestoration/plan.md)
records 566 passing Rendering EditMode tests and small module scenes without missing geometry,
but full VoxelShowcase still reported 293 missing chunks, 48 allocation failures/evictions,
terrain seams/gaps and incomplete houses. Stationary/walking FPS was 172.17/429.57 in different
views. These historical reports neither establish current correctness nor prove a regression cause.
This issue is the execution plan for the requested continuation; preserve previous evidence and
coordinate exclusive ownership with the earlier open issue. Do not close either issue by association.

## Acceptance and ordering

First establish clean compilation and exact-source standalone reproduction. Then prove canonical
occupied content reaches valid visible geometry across startup, stationary views, traversal, LOD
handoff, edits and restart, with no persistent unexplained gaps or stale geometry. Require both
independent behavioral regressions and production-quality full-scene images. Only after that gate
may optimization proceed. Keep correctness tests active after every performance change.

Diagnose stationary cost with matched workloads; target the latest recorded 400 FPS objective at
1920×1080, scale 1.0 on M4 Max/Metal, reporting repeatability and whole-frame tails. Earlier issue
metadata says 1,000 FPS; reconcile that historical discrepancy with the coordinator before numeric
performance signoff. Never claim either target passed from walking FPS or incomplete content.
Finish remaining GPU presentation migration and resource lifetime/budget gates without altering
integer CPU world truth, collision, content, quality, radii or device budgets.

## Hypotheses and discriminator

H1: missing source readiness/admission starves otherwise valid occupied chunks. H2: valid geometry
is published then wrongly retired, suppressed or culled at replacement/LOD handoff. Trace one
visible gap from canonical occupancy through source versions, demand, allocation, publication and
draw eligibility to distinguish them; a missing counter alone proves neither.

For performance, compare GPU submission/shader cost against presentation/external GPU contention
using a matched stationary Metal trace. Prior zero stationary eviction dispatches falsify eviction
work as that historical window's cause only. No product fix is selected yet.

## Ownership and remaining gates

Rendering owns GPU caches, extraction, selection and draws; reuse its SolidGpu, FarWorld and Water
validation scenes. Composition owns far modifiers and Showcase orchestration; reuse its own local
validation surfaces when changed. Storage remains integer authority; pure data-copy changes may
use module-local unit tests. Follow [tasks.md](tasks.md) in order. Final gates: exact-SHA owned tests,
module players, full VoxelShowcase evidence, canonical Kentridge player, budgets and PR workflow.
