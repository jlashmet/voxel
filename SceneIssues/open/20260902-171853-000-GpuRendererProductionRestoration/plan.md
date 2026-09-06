# GPU renderer production restoration

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through GPU rendering, delete retired CPU-only rendering, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable measured result under [tasks.md](tasks.md). Preserve CPU authority and GPU host orchestration. No hidden content, weaker budgets, reduced distance or permanent CPU fallback. User wants the full GPU path before additional optimization work.

## Retained evidence

Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`, HEAD `7bf5b0f09`. Local harness/tests/screenshots authorized; last requested push reached `origin/fixes/agent-1` at `64b2921a3`. Current work stays local.

All solid LOD steps have GPU implementations, including conditional step4 thin-feature preservation. A separate coarse classification mismatch is repaired and verified by 35 tests. Directory reclamation/collision fixes preserve the previous GPU-byte ceiling, but large coarse requests still lack bounded source streaming. Latest Showcase completed 180s/11 captures: reviewed 75.2s/150.2s **unacceptable**, grey distant houses and incomplete frontier. Full results in [tasks.md](tasks.md). Water and legacy CPU renderer removal remain open.

## Current coverage repair

Far material collapse is repaired in `7bf5b0f09`: per-primitive resolved slots restore brown roofs. Thirty-one focused tests, both module players and 180s/11-capture Showcase passed behavior gates. Reviewed Showcase 75.2s/150.2s remains unacceptable: simplified buildings/terrain and incomplete castle/frontier. Final 62 step4/three step8 publications, 612 missing-visible and 4,659,051 directory refusals. Detailed evidence is in tasks.md.

H1: whole-footprint source retention prevents coverage from converging under capacity pressure. H2: recovery merely needs more polls. Admission inspection confirms a 66³ padded request protects 287,496 bricks until complete coverage; the mirror has 58,144 mixed slots. Thus H2 cannot explain all configurations: an all-mixed footprint cannot fit, regardless of polling. This does not prove every stalled Showcase footprint is all mixed.

Selected direction: accumulate GPU summaries from bounded source portions, retain summary/generation validity, and release each source lease only after ordered GPU completion. No CPU-derived geometry or production blocking readback, larger budgets, shorter distance or hidden sources.

Implemented prerequisite: bounded summary dispatch accepts a destination block offset, preserving other portions and unknown-source flags. Dense dispatch explicitly resets the offset. Actual GPU tests preserve seven portions using one repeatedly reused mixed slot; adjacent portions still mesh into the correct closed paged geometry after source release. Thirty-five summary/mesher tests passed. Module player completed 60s/six captures/exit 0 with both distance-band markers. Reviewed 10s/50s remains prototype/blockout quality. It exercises the existing dense path, not scheduler streaming.

Next discriminating experiment: integrate portion-level demand/admission and asynchronous completion into step8 extraction, then run a production-faithful request larger than mirror capacity. Separate whole-request edit watches (currently keyed by demand footprints) from portion source protection; test edits between portions, cancellation/world teardown and unknown halo rejection. Step4 ordinary density still needs a separate bounded preparation solution. Do not claim the scheduler capacity defect fixed by the offset primitive alone.

## Remaining gates

Finish coverage and visual fidelity, migrate water, delete CPU-only rendering/oracles, and complete G11 retirement/error policy. Validate edits, pressure, lifecycle, module/integration players and repeated frame/memory workloads. G01–G27 remain incomplete. Full GPU completion precedes broader optimization.
