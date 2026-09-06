# GPU renderer production restoration

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through GPU rendering, delete retired CPU-only rendering, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable measured result under [tasks.md](tasks.md). Preserve CPU authority and GPU host orchestration. No hidden content, weaker budgets, reduced distance or permanent CPU fallback. User wants the full GPU path before additional optimization work.

## Retained evidence

Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`, HEAD `71d70c388`. Local harness/tests/screenshots authorized; last requested push reached `origin/fixes/agent-1` at `64b2921a3`. Current work stays local.

All solid LOD steps have GPU implementations. Step8 source streaming is bounded; full-scene coverage remains incomplete. Water migration and legacy CPU renderer removal remain open.

## Current GPU water migration

Step8 bounded summary streaming landed in `71d70c388`. A real 4,096-mixed-brick request converges through a 1,024-slot mirror; edits to completed portions reject candidates and retired worlds defer disposal. Showcase completed 180s/11 captures/exit 0 with zero directory refusals, but 567 missing-visible and only three step8 publications. Reviewed 75.3s/150.3s remains unacceptable. Detailed evidence is in tasks.md.

H1: coarse preparation monopolizes all count lanes. H2: remaining whole-source pressure prevents finer progress. Inspection limits step8 to two workers versus four lanes, falsifying H1 as a complete-lane monopoly. The last run has no slot/directory refusals, so H2 is not demonstrated for that run. Avoid speculative scheduling changes; user wants GPU completion before optimization.

Water still extracts CPU geometry and uploads it through CpuWaterSurfaceChunkCache. Selected migration keeps authoritative material snapshots and host orchestration but moves greedy extraction, topology flags, spray geometry, counts and paged writes onto the GPU. Existing water snapshots contain 512 cells plus six 64-cell face halos. They can feed GPU extraction without changing Storage truth or relying on the solid mirror's water-excluding invalidation policy.

First implementation: GPU count/write kernels consume packed snapshots in bounded eight-brick slices, preserving material-mask semantics, exposed-to-air faces, greedy merging, negative coordinates, lip/impact/edge flags, three spray sheets and spray UV bits. Uses the existing paged arena transaction; no geometry/count readback in production helpers. Five test fixtures compare actual GPU vertex multisets, material/active flags, counts and winding with the current CPU mesher. This oracle is temporary and must be removed with the CPU backend.

Initial Metal compilation rejected a dynamic vector component assignment; explicit axis vectors fixed it. All five parity fixtures passed (16s harness). Next integrate snapshot ownership/count/allocation/write/publication into the water cache, then run the module's real WaterDemo plus full Showcase. Neither runtime GPU water nor CPU renderer deletion is complete.

## Remaining gates

Finish coverage and visual fidelity, migrate water, delete CPU-only rendering/oracles, and complete G11 retirement/error policy. Validate edits, pressure, lifecycle, module/integration players and repeated frame/memory workloads. G01–G27 remain incomplete. Full GPU completion precedes broader optimization.
