# GPU renderer production restoration

## Objective and acceptance

Finish GPU-only rendering and reach400 FPS on VoxelShowcase. Preserve canonical integer CPU world
truth, generation/collision/simulation and required host orchestration. No hidden content, weaker
budgets or shorter distance. Startup fill-in and FPS are priorities; imperfect water is acceptable.
Delete retired CPU renderer helpers without losing coverage.

Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`. Local harness,
tests/screenshots authorized. Last requested push fulfilled at `origin/fixes/agent-1` `64b2921a3`;
newer work stays local. Detailed results are in tasks.md.

## Verified state

All solid steps1/2/4/8 and water use GPU geometry. CPU workers/workspace/contiguous arena/draw
route are deleted; helper/oracle cleanup remains. GPU resolves canonical air/uniform/water metadata
and mixed source readiness; only missing indices return for upload. Fine dense entries and coarse
summaries stay GPU-resident. CPU coverage polling is gone; world/version/epoch guards remain.
Cross-brick64x64 face merging and packed source portions reduced extraction cost. CPU generation
still~14.9s. Geometry allocation/directory failures and quit finalizer warnings reached zero.

`e24a749fa` removed redundant40,270-slot scans on full pinned mirrors.493 tests/module pass;
Showcase271/186FPS,430 missing,1763 publications,coarse3,oldest49s. Slot checks0.

Current GPU regression confirms fixed-order lane starvation. Rotate after actual submission and
queue new requests for PrepareFrame arbitration. Initial variant passes494 tests and improves
Showcase loading(first400 near chunks40.4→22.3s,coarse3→14),but standalone module fails with
zero publications. Removing admission-time advancement alone does not fix it.

Diagnostic module proves overlapping retries block uploads:216 ready,7 pending,126,520 active
skips,7 GPU requests andzero publications after14s. GPU kernels repeatedly reacquire readers
without changed source inputs. New regression reproduces this. Missing-feedback retries now
wait for existing host upload-publication counter progress, then GPU decides readiness again.
No CPU coverage scan or additional buffers.495 Rendering tests pass19s. Combined module48s/seven captures passes all markers,zero missing/errors,262.5MB.
Showcase248/188FPS,2947 publications,413 missing,coarse15,oldest24.6s. First400 near
chunks40.4→23.3s. Active skips95830→9198; renewed arena pressure386 failures/evictions.
Source hashes match; throughput improves but coverage/performance gates remain incomplete. Validation failure logs now include production ring diagnostics.

## Hypotheses and next experiments

H1 confirmed in module: service fairness plus upload-progress backpressure eliminates the reader
convoy. Full Showcase improves loading but exposes arena pressure; preserve the fix and resolve
pressure without memory increases or a deeper dispatch queue.
H2: camera-driven CPU band classification/hierarchy updates limit walking (~1.85ms traversal).
Migrate bands to GPU; retain candidate inputs until readiness/demand/clipmap membership changes.
Separate bounded missing-source urgency from full ready-geometry traversal. Preserve current-empty
parent handoff, stale fallback and eviction age. Coarse330 source portions per66³ footprint and
128 procedural draw buckets are further latency/cost hypotheses, not proven rewrite choices.

## Remaining gates

Startup pop-in, CPU helper retirement, GPU coverage/visual fidelity, lifetime/pressure, canonical
Kentridge integration and repeated frame/memory workloads. Castle silhouette persists; terrain
seams/far scenery/vegetation remain unacceptable. G01–G27 and400FPS are incomplete.
