# GPU renderer production restoration

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through GPU rendering, delete retired CPU-only rendering, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable measured result under [tasks.md](tasks.md). Preserve CPU authority and GPU host orchestration. No hidden content, weaker budgets, reduced distance or permanent CPU fallback. User explicitly wants the full GPU path before additional optimization work.

## Execution and retained results

Local harness/tests/screenshots are authorized. The last requested push reached `origin/fixes/agent-1` at `64b2921a3`; current work is local in `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`.

Earlier repairs cover GPU layout/allocation/prefix, asynchronous recovery, candidate approval/finalization, source retention/deferred clear, far handoff, omitted summit residency, paint-only proxy walls and prism roofs/normals. Terrain seams/gaps, coarse far geometry/materials/openings and water remain unacceptable. No visual or performance acceptance.

Step8 now uses GPU dense-cache summaries, bounded greedy count/write dispatches, the existing paged arena and versioned asynchronous publication. Profiles retain the existing production emission path. Step4 and water remain CPU-backed.

Three proven step8 blockers were repaired: quadratic pinned-readiness eviction scans (incremental cleanup), a 4.6-million-slot mirror beyond the packed directory's 65,536-slot address space (addressability clamp), and physical indices where the draw shader requires chunk-local indices. Initial standalone attempts failed or rendered blank despite publication. The test oracle now resolves both page tables like the draw consumer. **48 targeted tests pass**, including geometry bounds/area/winding, internal-face suppression, unknown halos, partial writes, integrated extraction, addressability and lifetime.

Rendering-owned production WorldBuilder landmark player: **60s, six captures, terminal exit 0**; reviewed 10s landmark is visible through step8 GPU geometry, but prototype/blockout quality. `gpu-hlod-coarse-module-draw/` retains source copies/hashes and logs. Full `gpu-hlod-showcase/`: **180s, 12 captures, terminal exit 0**, no exceptions/transaction rejections. Reviewed 75s/150s remains **unacceptable**: flat coarse background, terrain gaps and missing traversal coverage. Only one step8 publication; pending source footprints delay near work. Harness completion is not visual/coverage acceptance. Near module regression: 48s, eight captures, terminal exit 0; publication/edit/far-handoff assertions pass. Reviewed 42s remains a prototype fixture.

## Current hypotheses and next experiment

H1: resident source views cannot be borrowed or updated while footprints overlap. H2: shared recovery service is starved by coarse demands. Core-absent and upload-failure counters remain zero; missing core residency alone is not supported. Next inspect pending region borrow/active-reader/queue progress for one oldest request, then test the relevant bounded admission/source invariant. Do not equate unknown regions with air or add CPU fallback. GPU mesh generation failure is falsified for the fully resident module fixture, not for all production inputs.

## Remaining gates

Resolve mixed-LOD/frontier liveness, migrate step4/water, remove CPU-only rendering/oracles, and finish G11 last-consumer retirement/permanent-error policy. Validate edits, pressure, lifecycle, module/integration players and locked repeated frame/memory workloads. G01–G27 remain incomplete.
