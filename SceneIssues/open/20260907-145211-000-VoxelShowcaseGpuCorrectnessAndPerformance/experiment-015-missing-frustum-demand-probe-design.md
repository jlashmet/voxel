# Experiment 015 - exact missing-frustum demand probe discriminator

## Scope

This experiment narrows the next diagnostic change only. It does not change mirror capacity, admission fairness, eviction, LOD distance/quality, content, publication policy, or authoritative world state.

## Source inspection

`GpuSurfaceDrawDispatcher` already requests one bounded asynchronous GPU readback of `_LodState`. `GpuSurfaceDrawCompact.compute` defines the raw state bits produced by `CSClassifyLodCandidates` / `CSReduceLodCoverage`:

- bit 0: drawable
- bit 1: complete
- bit 2: expand to physical children
- bit 3: physical coverage
- bit 4: in configured LOD band
- bit 5: in camera frustum
- bit 6: renderer-owned candidate
- bits 7+: squared camera-distance admission priority

`ReceiveDemand` currently stores only `rawState >> 4` in `_demandGeometry`. That is sufficient for the existing admission/rank feedback, but it discards exactly the lower four GPU coverage bits needed to identify an in-frustum candidate whose physical coverage is missing.

## First exact discriminator

A renderer-local candidate eligible for the requested causal trace can be selected directly from the existing GPU readback with this observational predicate:

`owned && inBand && inFrustum && !physical`

That is `(rawState & 0x70u) == 0x70u && (rawState & 0x08u) == 0`.

This does not use the CPU brick map or any authoritative CPU world state as a rendering oracle. The coordinate comes from the renderer's own `GpuSurfaceLodInputs` identity associated with the same `_LodState` index.

## Async identity hazard

The readback is asynchronous while `GpuSurfaceLodInputs` may be updated/rebuilt on later frames. The current callback therefore must not look up `KeyAt(i)` from whatever input image happens to be live when the callback executes. To preserve an exact candidate identity, the bounded request must snapshot the renderer-local `SurfaceLodNodeKey` array (or at minimum the selected index-to-key mapping) together with request input/settings/topology versions at `RequestDemandFeedback()` time. The callback can then emit one immutable probe tied to the exact readback image.

This is an evidence/correctness requirement for the diagnostic itself: reconstructing the coordinate after the callback from a newer host image could falsely correlate a GPU state word with a different cell.

## Smallest permitted production instrumentation

The next source change should be observational only:

1. Snapshot request-time LOD node keys for the bounded `_LodState` request.
2. Preserve raw state long enough to choose one candidate satisfying `owned && inBand && inFrustum && !physical`.
3. Emit one bounded renderer diagnostic containing the exact `(step, chunk)`, raw state bits, request input/settings/topology versions, priority and renderer-derived voxel/world bounds.
4. Carry that same immutable `(step, chunk)` into existing renderer-local source coverage/admission, queue-age, extraction/count, allocation/page, publication/retirement and selected-draw diagnostics.
5. Add a focused Rendering EditMode regression proving that request-time identity survives an input-image advance before callback consumption and that the state predicate never selects off-frustum/off-band/unowned/already-physical candidates.

Only after that exact candidate is visible in stationary evidence can the first broken transition for the captured castle gap be assigned. No capacity or policy repair is justified by aggregate `missingVisible`/`noSlot` counters alone.

## Relation to experiments 013-014

Experiments 013-014 proved a persistent source-mirror liveness/capacity stall upstream of page allocation, but their oldest step-2 source request is an aggregate queue-age sample, not necessarily the missing castle-frustum candidate. This discriminator closes that identity gap without changing renderer behavior.
