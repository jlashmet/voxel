# GPU renderer production restoration

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through GPU rendering, delete retired CPU-only rendering, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable measured result under [tasks.md](tasks.md). Preserve CPU authority and GPU host orchestration. No hidden content, weaker budgets, reduced distance or permanent CPU fallback. User wants the full GPU path before additional optimization work.

## Execution and retained results

Local harness/tests/screenshots are authorized. Last requested push reached `origin/fixes/agent-1` at `64b2921a3`; local HEAD is `84403618f` in `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`. Current changes are uncommitted.

Earlier work repaired allocation/layout/publication, source retention/clear, far handoff, summit residency, proxy walls and prism roofs. Step8 has GPU summaries, meshing and real-player module proof. Step4 and water remain CPU-backed. Coverage invalidation is footprint-scoped; world resets still cancel all. Previous Showcase remains **unacceptable**: terrain gaps, coarse far geometry/materials and missing traversal coverage. No visual or performance acceptance.

## Current findings and experiments

`gpu-coarse-progress-trace/` completed 90s/six captures. Oldest coarse request made 6,732 polls with zero restarts; recovery ran 9,566 times with zero deadline skips. Borrow/block-read/active-reader counters remained zero. Scan starvation, edit replay and global resets are falsified as sole causes.

Three bounded host/source repairs are under validation:
- A failed cleanup slice now runs once per frame, resumes next frame and retains demanded sources. Regression: 1,024 checks before versus at most 64 after for 16 admissions. This alone did not unblock Showcase.
- A CPU index of existing GPU directory keys avoids exhaustive absent-key searches through tombstones and permits immediate tombstone reuse. Regression: 16,384 probes for 16 missing keys before; bounded afterwards. GPU collision/reuse/clear tests pass. This exposed `NoSlot` with only 1,949/65,536 mixed slots used: the separate uniform directory was full.
- Shared allocation now exchanges mixed payload slots for 1,048,576 directory entries within the previous actual GPU-byte ceiling. Byte accounting includes metadata. The old layout failed after 262,144 uniform keys; the new layout publishes and GPU-reads two 64³ coarse cores (524,288 keys), with no larger GPU allocation.

All 49 targeted tests pass in 20s. `gpu-directory-capacity-showcase/` finished 180s with 11 captures and no exceptions/transaction rejection. Step8 publications rose to four; near publication advanced, then traversal reached 11,981 directory refusals despite only 39,070/58,144 mixed slots used. Reviewed 75.1s/150.2s remains **unacceptable**: terrain gaps/coarse artifacts and an obstructed black traversal view. Module passes: 48s, eight captures, terminal exit0. No coverage/performance acceptance.

H1: directory pressure cannot reclaim cold uniform keys because `PublishBlock` only evicts mixed entries on `NoSlot`. H2: long-lived GPU tombstone tables also make absent-key lookups scan the full capacity; observed GPU windows reach about 750ms. Next distinguish directory exhaustion from mixed-slot exhaustion, test reclamation of cold uniform keys while protecting demanded/active footprints, then bound GPU lookup by a proven probe limit or compact safely. Do not just enlarge the directory again. Host index memory and mixed-slot pressure remain G22 validation gates.

## Remaining gates

Finish mixed-LOD/frontier liveness, migrate step4/water, delete CPU-only rendering/oracles, and complete G11 retirement/error policy. Validate edits, pressure, lifecycle, module/integration players and repeated frame/memory workloads. G01–G27 remain incomplete.
