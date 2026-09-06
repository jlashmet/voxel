# External agent feedback — GPU renderer defect candidates

**Assignment:** `20260902-171853-000-GpuRendererProductionRestoration`  
**Received:** 2026-09-05 (America/Los_Angeles)  
**Source:** user-supplied `Pasted text(2).txt`, attributed by the user to another agent.  
**Reviewer identity / reviewed commit:** not supplied.  
**Source SHA-256:** `5d5b92390defcd641402b86c08d85fd9652fb712cad3a3f6a8c2b5b22e0945d9`

## Import status and relationship to the active work

This is preserved review input, not independent verification of the current branch. The original reviewer’s “confirmed” labels, priorities, qualifications, proposed API names and implementation advice are retained verbatim below. No code audit, reproduction, runtime test or fix is claimed by this import. References to reviewed master and historical `TGPU-*` tasks may refer to an older implementation; reconcile existing repairs before adding duplicate work.

[plan.md](plan.md) and [tasks.md](tasks.md) remain the active GPU-first execution documents. This file is supporting feedback, not a second plan or a new CPU-first gate. The mapping below routes the six findings to existing tasks for verification; it does not mark those tasks complete or mandate the proposed implementation.

Proposals concerning production status feedback, coarser representations and CPU-oracle comparisons do not amend the current no-blocking, content/quality, CPU-backend-deletion or benchmark requirements. Resolve any conflict explicitly in the active plan/tasks before implementation. Preserve independent canonical regression expectations through the CPU-backend removal rather than retaining a hidden CPU renderer.

## Routing to the current checklist

All six items are **unverified against the current source at import**. Priorities below are the reviewer’s priorities.

| Original item | Reported concern | Reviewer priority | Existing tasks for verification |
| --- | --- | --- | --- |
| 1 | Submitted work treated as successful completion without an outcome check | P0 | G10 completion/publication; G12 pressure/recovery; G19 attribution |
| 2 | Pending geometry becomes live before final stale-build validation; ambiguous request identity | P0 | G10 two-phase publication; G13 edit invalidation |
| 3 | Batch-lane and source-lease ownership ends too early; cancellation of the filling-lane owner | P0 | G11 completion/lifetime; G12 bounded recovery |
| 4 | Release/cancel/handle reuse lacks a complete allocation transaction | P0 | G10 commit/abort and command coalescing; G12 allocation conservation |
| 5 | Fixed-frame page retirement and modulo upload-buffer reuse lack demonstrated lifetime guarantees | P1 | G11 last-consumer lifetime; G19 stalls/attribution; G22 memory |
| 6 | Mixed-LOD batch scratch layouts are not explicitly checked for compatibility | P1 | G07 LOD/batch coverage; G11 lane reuse; G12 reuse pressure |

Record each disposition and exact-source evidence under the corresponding active task: reproduced, already repaired, not reproduced with stated coverage, or superseded with a documented reason. Reuse the supplied regression scenarios where applicable; do not restart previously proven shader-math investigations without a new failing case.

## Original review (verbatim)

```text
1. Dispatched work is marked complete without checking whether geometry was successfully produced

Status: confirmed completion-contract defect. Priority: P0.

Locations: GpuSurfaceMirrorCoordinator.SealCountBatch(), GpuSurfaceExtractionContext.CompletePagedBatch(), and CpuTransvoxelChunkCache.FinishPagedGpuBuild().

What is wrong

SealCountBatch() submits the GPU work, inserts a fence, and immediately calls CompletePagedBatch() for every request. It does not wait for completion or consume a per-chunk result. FinishPagedGpuBuild() can then publish the handle, increment successful-build counters, and remove the chunk’s desired version from the work queue.

Meanwhile, the allocation shader already distinguishes:

AllocationReady     = 0
AllocationExhausted = 1
AllocationStale     = 2
AllocationTooLarge  = 3

It writes that status into RecordWord(record) + 10, but the CPU completion path does not consume it. The GPU can reject the allocation while the CPU regards the build as finished.

Possible result: a missing chunk never gets retried, or old geometry remains indefinitely while metrics incorrectly report a completed replacement.

Implementation change

Separate these states:

Requested → Submitted → GPU finished → Result validated → Published

Replace the current handle-only completion with an identity-tagged outcome such as:

ReadyCandidate
EmptyCandidate
Exhausted
TooLarge
Stale
Unsupported
Failed

ReadyCandidate means successful pending geometry—not yet permission to replace the live geometry.

For Exhausted, retain the previous live representation and keep a retryable request. For TooLarge, take an explicit supported action: split the request, use an acceptable coarser representation, or report a permanent capacity/configuration failure. Do not endlessly retry the identical impossible allocation. Treat valid empty geometry as a successful result distinct from failure.

Do not read generated vertices or indices back to the CPU. A small, batched status-and-identity channel is sufficient for this design. Unity supports asynchronous resource readback, with added latency rather than a synchronous wait; use it only for bounded render-control feedback and account for that latency. The current “no production counters” design would need an explicit status-channel contract rather than silently reinstating geometry/count readbacks.

Also, report separate submitted, GPU-finished, committed, and draw-visible metrics.

Regression test: deliberately exhaust a tiny arena while replacing an existing chunk. The old chunk must remain visible, success counters must not advance, and the replacement must succeed after capacity becomes available. Repeat with genuinely empty output and an oversized request.

2. GPU geometry can become live before the CPU performs its final stale-build check

Status: confirmed publication-contract defect. Priority: P0.

Locations: GpuSurfaceExtractor.DispatchBaseWriteBatch(), GpuSurfacePageArena.PublishBatch(), CSPublishBatchPages, and FinishPagedGpuBuild().

What is wrong

DispatchBaseWriteBatch() automatically calls pageArena.PublishBatch() after writing geometry. The shader can swap pending geometry into _LiveChunkGeometry. The CPU’s slot-ownership and desired-version checks happen afterward in FinishPagedGpuBuild(). Therefore, rejecting the CPU result does not itself prevent the already-submitted GPU publication.

The shader does compare pending and desired generations, but that check is only sufficient when its desired generation represents the same current renderer request the CPU validates.

That identity is currently ambiguous: BeginPersistentStage() substitutes the Storage generation when request.Generation is zero. A Storage revision and a renderer rebuild revision have different meanings and should not be interchangeable.

Implementation change

Make publication explicitly two-phase:

GPU: allocate + write pending geometry
CPU: validate the completed candidate against current render demand
GPU: execute explicit Commit or Abort

Remove unconditional publication from DispatchBaseWriteBatch(). Introduce generation-tagged CommitPending and AbortPending commands. The commit kernel must revalidate the identity immediately before changing the live record; a delayed CPU approval must not authorize a superseded request.

Use separate identity fields:

Field	Meaning
WorldEpoch	Which world/session owns the request.
HandleEpoch	Which allocation of a reusable handle it belongs to.
BuildGeneration	Which renderer request it is satisfying.
SourceStamp	Which source-data state was consumed.
GeometryConfigRevision	Relevant material/surface/coating/profile configuration.

These are proposed fields, not existing names. Compare like-for-like identities; never substitute Storage.Version for BuildGeneration. Capture the actual source stamp when admission establishes the input, while retaining the immutable renderer request identity.

A committed empty result should remove the old live geometry through this same transaction. An aborted result should preserve the old live geometry and reclaim only the rejected candidate’s allocation.

Regression test: submit generation A, invalidate it with an edit or configuration change, then allow A to finish. A must never replace the current live record. Test edits before submission, between submission and completion, and between CPU approval and GPU commit.

3. Batch lanes and source leases are released before their ownership has actually ended

Status: confirmed ownership mismatch; actual corruption requires reproduction. Priority: P0.

Locations: SealCountBatch(), ResetCountBatchLane(), GpuSurfaceExtractionContext.Release()/Dispose(), and ResetCountBatches().

What is wrong

After dispatch, the coordinator immediately clears the lane’s contexts and requests. The CPU completion path can then call Release(), which removes coverage and active-extraction ownership. The lane no longer records the submitted work as in flight, even though the fence may not have passed. Teardown also reasons primarily about the lane’s remaining Count.

There is a second, concrete cancellation hazard: an unsealed lane retains a reference to its first PrefixExtractor. Disposing that context disposes the extractor, while DispatchCountBatch() calls ThrowIfDisposed(). A lane that survives because other contexts still exist needs a defined way to remove or retain that canceled owner before sealing.

Important qualification: early CPU release does not automatically prove a GPU read/write race. Ordered GPU commands and Unity’s resource synchronization can protect particular accesses. The defect is that the ownership model does not explicitly distinguish those guarantees from actual completion.

Implementation change

Give each lane explicit states:

Free → Filling → Submitted → GPUComplete → DecisionsQueued → Reusable

Once submitted, the lane owns the immutable request descriptors, scratch buffers, required catalogue resources, and source-mirror leases. A worker can stop caring about the result without destroying resources that the lane still needs.

Cancellation must differ by phase:

Before submission: remove the record safely, compact the batch if necessary, and release resources no remaining record uses.

After submission: mark the result unwanted, but retain resources until their final GPU consumer is finished; then abort pending output and release ownership.

Teardown should stop admissions, invalidate the world epoch, and drain or defer destruction of submitted resources. A late callback must never modify the replacement world.

Use a completion mechanism validated on each supported backend. Do not assume compute support implies that every fence operation has the required semantics. Also, do not remove the existing global backpressure until the replacement has an equally explicit in-flight bound.

Regression test: keep another context alive, cancel/dispose the first context while its lane is still filling, and then seal it. Separately, delay submitted GPU work and cancel, unload, or restart the world. Assert no premature disposal, no early input reuse, and no late result applied to the new world.

4. Release, cancellation, and handle reuse do not form a complete allocation transaction

Status: confirmed incomplete cleanup contract; leak/double-retirement triggers need targeted tests. Priority: P0.

Locations: GpuSurfacePageArena.QueueRelease()/FlushHandleCommands(), CSApplyHandleCommands, CSAllocateBatchPages, and CSPublishBatchPages.

What is wrong

The release kernel retires live geometry and clears its ready flag, but does not resolve pending geometry. The CPU also returns released handles to _freeHandles immediately after dispatching the release commands. Meanwhile, allocation can overwrite the pending record for a handle, and publication reads the handle’s current pending record rather than validating a complete unique request identity.

This becomes particularly important once publication is split into explicit commit/abort: there will intentionally be a period during which allocated pending geometry exists without being live.

There is another unsafe API path on reviewed master: multiple commands for the same handle can be processed by separate GPU threads. Atomic retirement-counter increments do not serialize updates to that handle’s live record. The task file says a duplicate-command coalescing repair was previously proven, so reconcile that existing work before implementing another version.

Implementation change

Give each pending allocation exactly one terminal outcome: committed or aborted.

A release must cancel unsubmitted requests, invalidate submitted requests, schedule cleanup of pending allocations after their writers finish, and retire live allocations after their readers finish. Do not make a handle reusable until its ownership protocol permits it; additionally, increment its epoch so late commands cannot affect a later occupant.

Enforce at most one pending writer per handle, or explicitly represent multiple pending allocations by unique request IDs. Never silently overwrite the only record identifying allocated pages.

Serialize or coalesce same-handle commands while preserving their semantics. “Last command wins” is not sufficient when an earlier command contains necessary cleanup.

Require pending.ready and the full expected identity to match before commit or abort. Make repeated cancellation/release idempotent.

Regression test: allocate pending geometry, then exercise cancel, double cancel, release, double release, immediate reacquire, and late completion. For both vertex and index arenas, verify:

free pages + live pages + pending pages + retired pages = total pages

Each physical page must belong to exactly one category.

5. Fixed-frame retirement and three-buffer reuse need a real lifetime guarantee

Status: correctness/performance risk—not a proven use-after-free. Priority: P1, required before production acceptance.

Locations: GpuSurfacePageArena.RetirementDelayFrames, the page-retirement kernels, and GpuSurfaceDrawDispatcher.Prepare().

What needs scrutiny

Pages are retired using CPU frame epoch + 4. Draw buffers rotate through three slots, selected by frame % 3. Neither number, by itself, proves that every previous consumer has finished.

However, GPU lag exceeding four frames alone does not prove corruption: ordered commands may still use and recycle resources safely. Dynamic buffer updates may instead produce synchronization or stalls. This needs measurement and a deterministic lifetime test, not an automatic “increase the ring to eight” fix.

Implementation change

Define the last consumer of each resource: geometry pages, page-table banks, indirect arguments, draw metadata, and CPU-upload buffers.

Either establish a documented, tested ordering guarantee that prevents reuse from overtaking all prior consumers, or track completion tokens and recycle only completed allocations/slots. A fence after extraction protects extraction resources; it does not prove that subsequent draws have finished.

Include every relevant camera and pass when identifying the final draw consumer. Protect page-table bank reuse as well as the underlying pages—a retained page is not useful if its lookup table has already been replaced.

For CPU-upload rings, choose a completed slot rather than blindly selecting one by modulo. When none is available, use bounded backpressure or another explicitly safe policy rather than unbounded allocation.

Regression test: introduce GPU delay, repeatedly rebuild the same chunks, and render multiple cameras in one frame. Inspect both stale/incorrect geometry and stalls in buffer updates. Verify that no CPU-mapped upload storage is overwritten while still owned by a prior submission.

6. Mixed-LOD batches do not enforce scratch-layout compatibility

Status: confirmed missing compatibility check; resulting geometry failure needs a production-batch reproduction. Priority: P1.

Locations: TryDispatchCountBatch(), GpuSurfaceExtractor.CreateCountBatchResources()/DispatchCountBatch(), and GpuBrickCachePreparation.

What is wrong

A lane selects its first extractor and allocates resources with:

lane.Resources ??= extractor.CreateCountBatchResources(CountBatchCapacity);

Later records are admitted based on available capacity, without an explicit layout-compatibility check. Those resources are also retained for subsequent batches.

The resources depend on the creating extractor’s grid dimensions, face-sample dimensions, and BrickCacheEdge. Cache preparation uses a fixed edge and a physical per-request stride. A later request requiring a different layout must not silently inherit those assumptions.

Possible result: incomplete cache coverage, wrong request offsets, or incorrect geometry when the same batch/lane services different LOD configurations. Different source steps do not necessarily imply incompatibility; the actual layout must be compared.

Implementation change

Start with the smallest safe repair: a batch compatibility key covering the dimensions and shared inputs the batch actually assumes.

Include grid/cell dimensions, padding, cache edge/stride, face-sample dimensions, shader layout version, and relevant shared catalogue/table revisions. Admit requests only to compatible lanes. Reuse an existing allocation only after checking that it satisfies the next batch’s requirements.

A more flexible implementation can separate physical allocation stride from logical request extent. In that design, each request must carry its own logical extent and output base; both resolver and mesher must agree on the indexing. Do not mix “allocated maximum size” with “this request’s valid footprint.”

Regression test: exercise LOD1 → LOD2 → LOD1 on the same reusable lane, then both request orders in a mixed batch. Use boundary geometry, negative coordinates, and transition faces. Compare canonical geometry and attributes against the CPU oracle, and guard unused scratch regions with sentinels.

The common implementation contract

I would solve these as one coherent state machine rather than six unrelated patches:

Admit immutable request identity and acquire input leases
    ↓
Submit allocation and generation into pending storage
    ↓
Observe final GPU result; retain resources until their consumers finish
    ↓
Validate current world, handle, build, source, and configuration
    ↓
Queue identity-checked Commit or Abort
    ↓
Publish successful replacement or preserve old live geometry
    ↓
Reclaim allocations and handles exactly once, when safe

Implementation order: establish request identity and two-phase publication first; add accurate outcome handling; then make cancellation and lane ownership safe. Validate page retirement, draw-buffer reuse, and mixed-LOD compatibility before increasing concurrency.

These items substantially match existing tasks TGPU-026, 026A–C, 027, 027A–D, 028/028A, and 029A–B. The checklist also records earlier density, material, and topology parity work as proven; that is a reason to retain those regressions, not restart the shader-math investigation without a new failing case.

The most important invariant is: a chunk must never leave the retry queue merely because commands were submitted, and pending geometry must never become live merely because memory was allocated.
```
