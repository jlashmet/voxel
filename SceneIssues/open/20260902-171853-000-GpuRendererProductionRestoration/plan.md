# GPU renderer production restoration

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through GPU rendering, delete retired CPU-only rendering, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable measured result under [tasks.md](tasks.md). Preserve CPU authority and GPU host orchestration. No hidden content, weaker budgets, reduced distance or permanent CPU fallback. User wants the full GPU path before additional optimization work.

## Execution and retained results

Local harness/tests/screenshots are authorized. Last requested push reached `origin/fixes/agent-1` at `64b2921a3`; local HEAD is `bda8c4ef8` in `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`. Current changes are uncommitted.

Earlier allocation, lifetime, handoff and geometry repairs are retained. Step8 has GPU summaries/meshing and module proof. Step4 and water remain CPU-backed. Scoped invalidation protects unrelated work. Host directory indexing and rebalanced allocation preserve the prior GPU-byte ceiling while supporting two uniform coarse cores. Detailed evidence is in [tasks.md](tasks.md).

The previous 180s Showcase completed but failed acceptance: four coarse publications followed by 11,981 directory refusals with mixed slots still available. Reviewed 75.1s/150.2s was **unacceptable**, including an obstructed black traversal view. Borrowing, active-reader stalls, scan starvation, replay deadlines and global restarts were falsified as sole backlog causes.

## Current findings and experiments

Directory exhaustion now has an explicit `DirectoryFull` outcome. Recovery scans a bounded directory slice to reclaim cold uniform or mixed keys, retaining its cursor and protecting demanded/active footprints. Before regression returned `NoSlot` instead of reclaiming cold uniform keys; after tests preserve protected sources and refuse when everything is protected.

The allocated directory retains 25% free capacity. CPU insertion records a conservative maximum probe distance; deletions cannot increase any live key's displacement. GPU resolver, summary and legacy persistent lookup consume that bound. This preserves all live keys while avoiding full-capacity negative searches through tombstones. The host rejects a full live-key set without scanning the whole table. No allocation-byte increase or authoritative-state change.

The first reclamation/probe-bound run completed 180s/11 captures but still developed long searches: a linear eviction sweep concentrated directory holes, and deletion tombstones retained long probe chains. That combination is falsified as sufficient.

Deletion now shifts entries backward along valid probe chains without moving payload slots. Ordered GPU updates preserve queued readers; odd-stride victim selection spreads reclamation. All 48 targeted tests pass in 24s, including queued reads across table wrap and protected/all-protected sources.

`gpu-directory-backshift-showcase/` completed 180s/11 captures/exit 0. Reviewed 75.1s and 150.1s: black obstruction absent, but overall **unacceptable** (unfinished terrain/presentation and incomplete coverage). Final diagnostics: 2,203 publications, only three step8; 1,041,989 directory refusals, 29,984,389 insertion probes, oldest step8 request 20.8s. Late diagnostic CPU p50 6–8ms/GPU p50 roughly 1ms is not accepted performance evidence. Near-module rerun passed 48s/eight captures/exit 0; publication/edit/far handoff passed with zero fallback. Reviewed 42s fixture is prototype quality.

H1: source demand exceeds simultaneous protected directory capacity. H2: coarse readiness rescans delay publication despite recovered sources. Next isolate coarse source coverage in the production module, distinguishing protected live-key capacity from readiness rounds before changing admission. Full GPU migration remains the priority; defer broader optimization.

## Remaining gates

Finish mixed-LOD/frontier liveness, migrate step4/water, delete CPU-only rendering/oracles, and complete G11 retirement/error policy. Validate edits, pressure, lifecycle, module/integration players and repeated frame/memory workloads. G01–G27 remain incomplete.
