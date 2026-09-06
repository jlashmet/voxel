# GPU renderer production restoration

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through the GPU backend, physically delete the retired CPU-only renderer, and pursue **1,000 FPS / 1.00ms whole frame**, or the closest repeatable result under [tasks.md](tasks.md). Preserve authoritative CPU storage/generation/collision/simulation and GPU host orchestration. No hidden content, weaker budgets, reduced distance or permanent CPU fallback.

## Execution and material results

Local harness/tests/screenshots are authorized. User-requested push verified `origin/fixes/agent-1` at `690b61756`. Continue in `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`; later work is local.

Prior repairs cover GPU layout, allocation, bucket prefix, asynchronous recovery, explicit candidate approval/write-finalization, submitted resource retention and deferred mirror clear. Geometry/counts never return to CPU authority. World replacement passes 24 focused checks; final-draw lifetime/permanent-error policy remain open.

Near publication plus regional discovery drive same-pass far handoff; unknown coverage retains proxies. Explicit footprint residency repairs omitted summit region(-2,1,0), with 5 tests and 200-region bake. Composed children/separated vertical spans and Showcase-owned streaming coverage still need audit.

Paint-only modifier bounds incorrectly became solid far boxes. The fix passes 5 checks and removes phantom traversal walls in the normal 180s player. Bounded ramp/prism profiles and planar box normals pass 27 checks, both 28s module players and 180s Showcase. Reviewed 75s/150s remain below acceptance: terrain aliasing/seams, near gaps, coarse far geometry, missing openings/material separation. Visual quality is **unacceptable**.

## Current hypotheses and experiment

H1: increasing GPU source step alone loses thin features. Existing CPU step 8 preserves 2×2×2 subcell occupancy and exposed-column voting; step 4 invokes the same preservation when sampled geometry vanishes. H2: port that predicate into bounded GPU brick batches before GPU mesh emission.

New summary kernel consumes the actual persistent mirror and writes 19 words per brick, with explicit unknown-source status. Initial tests exposed two defects: empty bricks intentionally have no directory entry, and dynamic packed-byte extraction returned incorrect values on Metal despite correct source bytes. Complete mirrored-region coverage must authorize absent-as-air; CPU readiness alone cannot. Simplifying voting did not fix byte extraction. Explicit byte selection passes all 13 tests, including all 512 isolated voxel positions, material voting/ties, configured water and reset/disposal rejection. Test-only readback observes results; production dispatch neither waits nor reads back.

The kernel is not yet integrated into scene mesh generation. Current validation is headless GPU data preparation through Rendering-owned EditMode tests; it makes no rendered-quality claim. Normal 180s Showcase passed 11 captures without exceptions/rejections; reviewed 75s/150s remain **unacceptable** with no phantom-wall regression. Next: connect bounded summary batches to GPU meshing with versioned source leases and module-owned player coverage.

## Remaining gates

Finish G11 last-consumer retirement and permanent-error policy; migrate steps 4/8 and water; replace CPU-dependent oracles and delete CPU-only rendering. Resolve all visual defects and validate module/integration players, edits, pressure and lifecycle. Run locked repeated frame/memory workloads. G01–G27 remain authoritative and incomplete.
