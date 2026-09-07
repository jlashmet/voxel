# Experiment 040 — post-alignment GPU count-batch lane starvation

## Trigger
Exact-SHA run `33812580159` on feature source `32b8972a9e6f3876b87b2c2885f806727da23d78` is workflow-green: the requested `KentridgeMacroWorldSurveyStreamingAlignmentTests.ElevatedSurveyPinsStreamingDemandToRenderedCameraBeforeSliceStreaming`, repository-derived module validation, and the 180-second standalone `KentridgePlayableSlice` replay all complete successfully. Artifact `9915578779` is nevertheless rejected for closure because the durable player evidence never progresses past the first Moordell survey.

This is the second materially different correction after the same strict-publication symptom: experiment 037 corrected the discrete streaming/metric-radius mismatch, and experiment 038 aligned CharacterMotor streaming demand with the elevated renderer camera. Per the assignment rule, no third remediation is selected until the surviving symptom is isolated to a minimal repro/root cause.

## Exact player evidence
Immediately before the macro teleport, renderer telemetry has `coreAbsent=0`. The Moordell survey begins at `cameraDm=(2030,3780)` with `cameraHeightM=70.0`. Moordell's four authored content columns eventually become ready around 74 seconds, and the residency diagnostic reports the expected radius-3 horizontal set (`horizontalColumns=29`, `residentInRadius=58`, `featureVerticalExtra=0`).

After content readiness, the player continues emitting more than 100 renderer samples but never advances to Rossdam. The visible hole count decreases only from roughly 310 to 221. The exact near step has no ground residents because the survey camera is approximately 70 m above terrain (`step1 0-57.6m res=0`); the half-resolution ring owns the visible ground and grows only from roughly 223 to 315 resident chunks. Eight GPU requests remain in phase 2 and the oldest request grows beyond 20 seconds. The cumulative `coreAbsent=2797` value does not continue rising; it is a camera-transition diagnostic, not the continuing wait.

Phase 2 is important: `GpuSurfaceExtractionContext.BeginPersistentStage` has already passed mirror coverage, called `TryBeginExtraction`, configured the persistent lookup and set `_hasStaged=true`. `TryDispatchPendingCount` then submits that staged request to `GpuSurfaceMirrorCoordinator.TryDispatchCountBatch`. The continuing stall is therefore after mirror-coverage admission, in the cross-chunk count/paged-publication lane.

## Minimal deterministic repro
`GpuSurfaceMirrorCoordinator` currently defines four count lanes with capacity two and a maximum of eight concurrent extraction chains. Two fixed-order choices compose into starvation under sustained cold-view demand:

1. `TryDispatchCountBatch` scans lanes from index 0 and selects the first lane whose `Count < CountBatchCapacity`. When that lane reaches capacity it calls `SealCountBatch` immediately.
2. `AdvanceCountBatches` also scans lanes from index 0 every frame. `SealCountBatch` calls `TryReserveExtractionDispatch(frame)`, which allows only one extraction dispatch per rendered frame.

Start with all four lanes full. At the next frame, lane 0 receives the one dispatch token and is reset. Lanes 1-3 cannot reserve another dispatch that frame. Sustained visible demand then refills the newly empty lane 0 because admission also starts at index 0. On the next frame, `AdvanceCountBatches` again sees a full lane 0 first, dispatches it, and lanes 1-3 again lose the frame token. Repeating the current algorithm produces the dispatch sequence:

`0, 0, 0, 0, 0, 0, ...`

while lanes 1, 2, and 3 remain full forever. Those three lanes contain exactly six records, matching the observed shape in which a cold macro view keeps eight phase-2 requests in flight while some work continues to publish and a subset ages indefinitely.

The `batchArenaWait` counter does not invalidate this isolate. It increments only when the previous graphics fence has not passed. A lane that loses `TryReserveExtractionDispatch(frame)` because an earlier lane already consumed the frame token returns without incrementing that counter, so fixed `batchArenaWait` alongside growing request age is consistent with dispatch-token starvation.

## Root cause
The renderer's one-dispatch-per-frame backpressure is valid, but the queue servicing policy is not fair. A fixed lane-0 scan combined with fixed first-free admission allows a freshly refilled lane 0 to monopolize the global frame token forever. This is a shared renderer liveness defect exposed by a sustained cold-view demand transition; it is not a Kentridge geography, CharacterMotor, streaming-radius, device-budget, or publication-semantic defect.

## Selected correction boundary
The smallest correctness change is to make the count-batch seal authority round-robin across lanes and prevent an inline newly-filled lane from bypassing older full lanes. Keep all existing capacities, extraction concurrency, GPU/device budgets, coverage semantics, fences, and one-dispatch-per-frame backpressure unchanged. A focused renderer regression must prove that sustained refill services all four lane identities rather than repeatedly selecting lane 0. Kentridge must then re-run the exact 180-second built-player evidence before any visual acceptance is claimed.
