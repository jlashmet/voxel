# GPU renderer production restoration

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through GPU rendering, delete retired CPU-only rendering, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable measured result under [tasks.md](tasks.md). Preserve CPU authority and GPU host orchestration. No hidden content, weaker budgets, reduced distance or permanent CPU fallback. User wants the full GPU path before additional optimization work.

## Retained evidence

Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`, HEAD `4df95a68d`. Local harness/tests/screenshots authorized; last requested push reached `origin/fixes/agent-1` at `64b2921a3`. Current work stays local.

All solid LOD steps have GPU implementations. Large coarse requests still lack bounded source streaming. Water migration and legacy CPU renderer removal remain open.

## Current coverage repair

Far material separation is repaired in `7bf5b0f09`. Previous Showcase remained unacceptable with incomplete castle/frontier coverage and 4,659,051 directory refusals. See tasks.md for exact evidence.

H1: whole-footprint source retention prevents coverage from converging under capacity pressure. H2: recovery merely needs more polls. Admission inspection confirms a 66³ padded request protects 287,496 bricks until complete coverage; the mirror has 58,144 mixed slots. Thus H2 cannot explain all configurations: an all-mixed footprint cannot fit, regardless of polling. This does not prove every stalled Showcase footprint is all mixed.

Selected direction: accumulate GPU summaries from bounded source portions, retain summary/generation validity, and release each source lease only after ordered GPU completion. No CPU-derived geometry or production blocking readback, larger budgets, shorter distance or hidden sources.

Summary accumulation landed in `512a5f746`: 35 GPU tests and the coarse module passed; visual quality remains prototype.

Edit-watch/source-range separation landed in `4df95a68d`; 25 focused tests and the coarse module passed.

Current integration admits step8 with a whole-request edit watch and no full source retention. Existing count lanes accumulate contiguous bounded rows into their existing HlodSummaries buffer. Each portion holds demand, active region/brick readers and the mirror allocation until an asynchronous callback on CPU-written request metadata. No summary payload readback. Lanes stay immutable during preparation; stale/cancelled lanes are discarded after completion, and retired worlds defer buffer disposal to the callback. Prepared step8 count/write skips dense source lookup/re-summarization. Other LOD steps retain their existing paths.

Verification: 42 existing regressions passed. Two new actual asynchronous lane tests initially stalled because EditMode did not advance player frame-budget guards; explicit test slices corrected the fixture. All 16 queue/lifetime tests now pass, including portion-to-publication, retired in-flight disposal, rejection after editing a completed portion, and real geometry from 4,096 mixed bricks through a 1,024-slot mirror. Coarse player passed 60s/six captures; reviewed 10s/50s remains prototype/blockout quality. Showcase completed 180s/11 captures/exit 0: reviewed 75.3s/150.3s remains unacceptable, though upper castle gaps are filled. Final directory refusals zero, 567 missing-visible, 84 step4/three step8 publications. Step4 still retains whole footprints. Next discriminate count-lane contention from remaining source-demand pressure before further coverage changes. Water/CPU-only removal remain required; no performance acceptance.

## Remaining gates

Finish coverage and visual fidelity, migrate water, delete CPU-only rendering/oracles, and complete G11 retirement/error policy. Validate edits, pressure, lifecycle, module/integration players and repeated frame/memory workloads. G01–G27 remain incomplete. Full GPU completion precedes broader optimization.
