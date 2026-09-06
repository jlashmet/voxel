# GPU renderer production restoration

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through GPU rendering, delete retired CPU-only rendering, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable measured result under [tasks.md](tasks.md). Preserve CPU authority and GPU host orchestration. No hidden content, weaker budgets, reduced distance or permanent CPU fallback. User wants the full GPU path before additional optimization work.

## Retained evidence

Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`, water-kernel base `08236d15e`. Local harness/tests/screenshots authorized; last requested push reached `origin/fixes/agent-1` at `64b2921a3`. Current work stays local.

All solid LOD steps have GPU implementations. Step8 source streaming is bounded; full-scene coverage remains incomplete. Water migration and legacy CPU renderer removal remain open.

## Current GPU water migration

Step8 bounded summary streaming landed in `71d70c388`. A real 4,096-mixed-brick request converges through a 1,024-slot mirror; edits to completed portions reject candidates and retired worlds defer disposal. Showcase completed 180s/11 captures/exit 0 with zero directory refusals, but 567 missing-visible and only three step8 publications. Reviewed 75.3s/150.3s remains unacceptable. Detailed evidence is in tasks.md.

H1: coarse preparation monopolizes all count lanes. H2: remaining whole-source pressure prevents finer progress. Inspection limits step8 to two workers versus four lanes, falsifying H1 as a complete-lane monopoly. The last run has no slot/directory refusals, so H2 is not demonstrated for that run. Avoid speculative scheduling changes; user wants GPU completion before optimization.

Water still extracts CPU geometry and uploads it through CpuWaterSurfaceChunkCache. Selected migration keeps authoritative material snapshots and host orchestration but moves greedy extraction, topology flags, spray geometry, counts and paged writes onto the GPU. Existing water snapshots contain 512 cells plus six 64-cell face halos. They can feed GPU extraction without changing Storage truth or relying on the solid mirror's water-excluding invalidation policy.

GPU count/write kernels now consume eight-brick snapshot slices and emit greedy surfaces, topology and spray into the paged arena without CPU geometry/count readback. Five actual-GPU parity fixtures use a temporary CPU oracle, which must disappear with the CPU backend.

Initial Metal compilation rejected a dynamic vector component assignment; explicit axis vectors fixed it. All five parity fixtures passed (16s harness). Next integrate snapshot ownership/count/allocation/write/publication into the water cache, then run the module's real WaterDemo plus full Showcase. Neither runtime GPU water nor CPU renderer deletion is complete.

Water drawing now supports paged indirect geometry in both production shader passes through a shared fetch helper. Real Metal raster tests exercise bucket offsets, alternating banks, body/spray rejection and exact pixel parity with the temporary contiguous path. Final `gpu-water-paged-raster-parity.xml`: 11 passed, exit 0, 13s (five mesher fixtures plus six draw/compaction cases). These are narrow addressing regressions, not standalone visual acceptance. Runtime cache still uses CPU extraction; next wire GPU transaction ownership and publication, select paged water drawing, then delete contiguous support. Existing module-local WaterDemo owns subsequent standalone validation.

## Remaining gates

Finish coverage and visual fidelity, migrate water, delete CPU-only rendering/oracles, and complete G11 retirement/error policy. Validate edits, pressure, lifecycle, module/integration players and repeated frame/memory workloads. G01–G27 remain incomplete. Full GPU completion precedes broader optimization.
