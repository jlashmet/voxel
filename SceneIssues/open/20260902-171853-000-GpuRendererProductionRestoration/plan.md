# GPU renderer production restoration

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through GPU rendering, delete retired CPU-only rendering, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable measured result under [tasks.md](tasks.md). Preserve CPU authority and GPU host orchestration. No hidden content, weaker budgets, reduced distance or permanent CPU fallback. User explicitly wants the full GPU path before additional optimization work.

## Execution and retained results

Local harness/tests/screenshots are authorized. The last requested push reached `origin/fixes/agent-1` at `64b2921a3`; current work is local in `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`.

Earlier repairs cover GPU layout/allocation/prefix, asynchronous recovery, candidate approval/finalization, source retention/deferred clear, far handoff, omitted summit residency, paint-only proxy walls and prism roofs/normals. Terrain seams/gaps, coarse far geometry/materials/openings and water remain unacceptable. No visual or performance acceptance.

Step8 now uses GPU dense-cache summaries, bounded greedy count/write dispatches, the existing paged arena and versioned asynchronous publication. Profiles retain the existing production emission path. Step4 and water remain CPU-backed.

Step8 module rendering and 48 targeted tests pass after admission-scan, mirror-addressability and draw-index repairs. Previous Showcase remains **unacceptable**; source coverage stalls during traversal. Detailed evidence is retained in [tasks.md](tasks.md). No visual or performance acceptance.

## Current hypotheses and next experiment

The 90s `gpu-hlod-recovery-reasons/` trace completed with six captures and no exceptions. Borrow/block-read/active-reader stall counters remain zero while recovery publishes over two million records, falsifying those failure paths for this run.

Coverage invalidation now affects only footprints intersecting a solid change; mirror resets still invalidate all. New occupancy in an absent halo invalidates the scan too. Admission and queued-request validity share the stamp. Forty targeted tests pass; a second 37-test suite adds actual queued cancellation and world-replacement checks, all passed. The 180s `gpu-hlod-scoped-coverage/` run finished with 12 captures and no exceptions/rejections. Reviewed 74.9s/149.9s remains **unacceptable**, with only one step8 publication. Global restart is falsified as the sole backlog cause; this fix preserves a useful source-validity invariant but does not complete G07. Module rerun passes: 48s, eight captures, required publication/edit/far-handoff markers, terminal exit 0. Reviewed 42s retains prototype fixture quality.

H1: the 128-brick-per-poll scan, combined with scheduler time slicing, cannot finish coarse footprints promptly. H2: change replay consumes the shared recovery deadline, delaying already-requested bricks. Next measure useful polls/cursor progress for the oldest coarse request and recovery calls versus deadlines consumed by change replay. Preserve existing budgets and source truth; do not equate unknown core regions with air or add CPU fallback.

## Remaining gates

Resolve mixed-LOD/frontier liveness, migrate step4/water, remove CPU-only rendering/oracles, and finish G11 last-consumer retirement/permanent-error policy. Validate edits, pressure, lifecycle, module/integration players and locked repeated frame/memory workloads. G01–G27 remain incomplete.
