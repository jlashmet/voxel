# GPU renderer production restoration — GPU-first plan

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through the GPU backend, physically delete the retired CPU-only renderer, and pursue **1,000 FPS /1.00ms whole frame**, or the closest repeatable result under [tasks.md](tasks.md). Preserve authoritative CPU storage/generation/collision/simulation and necessary GPU host orchestration. No hidden content, weaker budgets, reduced distance or permanent CPU fallback. All task gates remain mandatory.

## Execution and proven repairs

User directs local harness/tests and screenshot review; **no further origin pushes**. Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`. Existing remote request preserved.

Local repairs: bounded watchdog process accounting; shared44-byte GPU descriptor; common-point allocator status write; explicit indirect bucket metadata prefix; compatible batch resource layouts. Boundary regression alternating cache edges4/6 fails before (expected256 vertices, got0), passes after. Earlier huge step2 geometry counts came from malformed cache layouts and cannot justify compression or budget changes. Exact evidence is in tasks.md.

`layout-coverage-trace/` on d125cbe32 completed180s/11 PNGs. At60s all538 visible handles were live-ready,491 nonempty draws,7454 free vertex pages,zero failures. At120s only45/69 visible handles were ready and138 allocation failures had accumulated. Through175s,290 of1635 completed requests were Exhausted. Thus traversal pressure is real and fence-only completion hid rejected work. Visual acceptance remains unmet.

## Selected render-control contract and current implementation

G05 prohibits generated-geometry/extraction-count readback. G10–G13 permit a bounded asynchronous render-control record:16 bytes per chunk (status,handle,generation),32 bytes per lane. No blocking wait, authoritative-state derivation, geometry/count transfer, budget increase or hidden visible demand.

GPU finalization is followed by compact status export. Cached callbacks copy only those words; lane scratch stays owned until feedback completes, with deferred disposal for retired lanes. Only Ready reaches host completion; failures enter retry, and Exhausted triggers bounded offscreen GPU eviction. Geometry/page allocation remains GPU-owned. Existing automatic pending-publication bridge remains: explicit CPU-approved commit, permanent-error policy and full last-consumer retirement are still G10/G11 work.

Twenty-two focused tests pass, zero skips, in15s (`outcome-recovery/final-tests.xml`): real GPU exhaustion -> asynchronous16-byte result -> reclamation -> retry, independent record identity, and no blocking/count-transfer architecture. The180-second `outcome-recovery/` player completed with11 screenshots and no rejection errors. Terrain gaps/bands and blockout far/water presentation remain **unacceptable**. Diagnostic timings are archived; no visual/performance acceptance.

## Hypotheses and next discriminator

1. Offscreen eviction/retry restores coverage but needs admission prioritization to avoid churn.
2. Actual visible demand still exceeds capacity and needs semantic-preserving compaction.

Next correlate current successful publications, retries, page reclamation and exact live visible records through traversal, then fix the earliest remaining divergence. Finish explicit approval/lifetime and GPU step4/8/water migration; delete CPU-only rendering; prove independent-consumer/edit/lifecycle behavior; run locked repeated performance/memory workloads and final local review.
