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

`f1e72dd37` fixes source-lane starvation and repeated missing-source reads that blocked their
own uploads.495 tests/module pass. Showcase248/188FPS,2947 publications,413 missing,coarse15,
oldest24.6s; first400 near chunks40.4→23.3s. Faster throughput exposes386 arena failures/evictions.

## Hypotheses and next experiments

H1 current migration: GPU classifies padded Chebyshev bands and suspension. CPU supplies all
known clipmap candidates, including out-of-band current/empty/missing metadata; GPU masks flags
before parent reduction. Candidate lists/hierarchy no longer invalidate on camera/projection
changes. CPU refreshes only cached unsettled build urgency and resident ages. Unchanged queries
refresh ages without repeating bounds/dictionary work; projection planes always reach GPU.
Bounded host pending metadata reuses lists limited by active clipmap coordinates; no GPU buffers
or budget increases.502 Rendering tests pass,including four-step band equality, suspension,
parent handoff, GPU buffer reuse, and cached demand/age behavior across camera movement.
GPU bands/candidate cache module passes; Showcase245/195FPS,CPU3.9/4.6ms,451 missing,
140 arena failures. Walking traversal~0.9ms versus1.7ms, but larger metadata rebuilds cost more.
Preserve topology when owned membership and referenced keys are unchanged; update flags/handles
only. GPU test proves handoff/edit reversal without topology rebuild and rebuild after membership
changes. Module passes all markers,262.5MB. Final Showcase231/201FPS,CPU4.01/4.585ms,414 missing,
55 allocation failures,coarse22. Traversal median1.664→.977ms; input max2.924ms still includes
membership rebuilds. Source hashes match. Stationary regresses; not complete performance proof.
H2: renewed arena pressure and128 procedural draw buckets may limit completed-scene throughput.
Keep memory/frame budgets unchanged; measure after this migration before choosing a rewrite.
Next migrate per-camera missing-build band/frustum scans to bounded GPU presentation-demand
feedback, retaining epoch/membership/urgency/age invariants. Profile stationary render residual
separately before a draw rewrite. CPU hierarchy membership updates and helper cleanup remain.

## Remaining gates

Startup pop-in, CPU helper retirement, GPU coverage/visual fidelity, lifetime/pressure, canonical
Kentridge integration and repeated frame/memory workloads. Castle silhouette persists; terrain
seams/far scenery/vegetation remain unacceptable. G01–G27 and400FPS are incomplete.
