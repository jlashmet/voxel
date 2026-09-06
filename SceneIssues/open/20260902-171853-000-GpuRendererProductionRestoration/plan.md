# GPU renderer production restoration

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through GPU rendering, delete retired CPU-only rendering, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable measured result under [tasks.md](tasks.md). Preserve CPU authority and GPU host orchestration. No hidden content, weaker budgets, reduced distance or permanent CPU fallback. User wants the full GPU path before additional optimization work.

## Retained evidence

Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`, HEAD `512a5f746`. Local harness/tests/screenshots authorized; last requested push reached `origin/fixes/agent-1` at `64b2921a3`. Current work stays local.

All solid LOD steps have GPU implementations. Large coarse requests still lack bounded source streaming. Water migration and legacy CPU renderer removal remain open.

## Current coverage repair

Far material collapse is repaired in `7bf5b0f09`: per-primitive resolved slots restore brown roofs. Thirty-one focused tests, both module players and 180s/11-capture Showcase passed behavior gates. Reviewed Showcase 75.2s/150.2s remains unacceptable: simplified buildings/terrain and incomplete castle/frontier. Final 62 step4/three step8 publications, 612 missing-visible and 4,659,051 directory refusals. Detailed evidence is in tasks.md.

H1: whole-footprint source retention prevents coverage from converging under capacity pressure. H2: recovery merely needs more polls. Admission inspection confirms a 66³ padded request protects 287,496 bricks until complete coverage; the mirror has 58,144 mixed slots. Thus H2 cannot explain all configurations: an all-mixed footprint cannot fit, regardless of polling. This does not prove every stalled Showcase footprint is all mixed.

Selected direction: accumulate GPU summaries from bounded source portions, retain summary/generation validity, and release each source lease only after ordered GPU completion. No CPU-derived geometry or production blocking readback, larger budgets, shorter distance or hidden sources.

Summary accumulation landed in `512a5f746`: 35 GPU tests preserve summaries through slot reuse and mesh released adjacent sources. Coarse module passed 60s/six captures; 10s/50s remains prototype quality.

Current change separates whole-request edit-watch readers/epochs from source-demand readers. Existing full-coverage callers acquire/release both. New rectangular source portions are limited to one 1,024-brick summary dispatch and use the existing recovery scan. Tests cover edits after portion release, unrelated edits, shared watches, retired-world releases, negative-coordinate range boundaries and overlap. Existing queued cancellation/lifetime tests remain required. All 25 focused tests passed, including actual ready-record eviction after source release. Coarse player passed 60s/six captures; reviewed 10s/50s remains prototype/blockout quality.

Next integrate step8 accumulation into existing count lanes, reusing their HlodSummaries allocation. Admit whole-request watches before preparation; fill bounded source ranges under ordered GPU leases before count/write. Preserve cancellation/world teardown and unknown-halo rejection. Prove a production request larger than mirror capacity converges, including edits between portions. Step4 ordinary density still needs a separate bounded preparation solution. Do not claim the scheduler capacity defect fixed by the offset primitive alone.

## Remaining gates

Finish coverage and visual fidelity, migrate water, delete CPU-only rendering/oracles, and complete G11 retirement/error policy. Validate edits, pressure, lifecycle, module/integration players and repeated frame/memory workloads. G01–G27 remain incomplete. Full GPU completion precedes broader optimization.
