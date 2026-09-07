# Experiment 003 — persistent GPU source-demand lease starvation

## Hypothesis

The persistent visible holes are H1 source/admission starvation: fine-LOD requests keep their entire source neighbourhood demanded while waiting, preventing the bounded source-preparation/recovery path from recycling the shared mixed-brick mirror. This can leave occupied source unresolved long enough for GPU lookup to observe a missing directory entry as air.

## Exact baseline

- Feature source: `887a819f1ef6f795e546be73783ece40c3dca052`
- CI transport request: `fd092c40e8d19ddc5f67d7a1f2165ae74f865864`
- Workflow run/job: `34155859120` / `101847425373` — success
- Unity: `6000.5.6f1`
- Player: production VoxelShowcase standalone capture, 180 seconds
- Device/API: Apple M4 Max / Metal
- Surface: 1600x900, vSync off, targetFrameRate unset

The successful run proves cold build/compile and the production player path complete under the 14,336 MB guarded build envelope. It is therefore valid product evidence rather than the earlier harness failure.

## Evidence

The shared mirror grows to `mixed=40270/40270`. Once full, `noSlot` increases continuously and reaches `130550` by the end. During saturation the whole-request demand count remains roughly 5–12 and recovery keeps retrying, while GPU page pressure reports `allocFail=0 evicted=0` and demand feedback reports `unqueued=0`.

A fine step-1 request remains active for about 151.7 seconds. At 180 seconds the player still reports `missingVisible=63`, four GPU chains in flight, and only 768 current visible publications. This is far beyond the existing focused-scene convergence windows.

Performance degrades with the same accumulation. Early post-startup one-second windows are commonly 500–600 FPS. Late windows fall to roughly 150–220 FPS with increased present/GPU time and occasional large spikes. Correctness remains the blocker; these timings are causal evidence, not numeric performance signoff.

Static ownership trace:

1. `GpuSurfaceExtractionContext.BeginPersistentStage` takes a whole-footprint `RequestCoverage` lease for steps 1/2/4; step 8 already takes only `RequestEditWatch`.
2. Mirror eviction rejects every block covered by `IsBlockDemanded` or `IsBlockActive`.
3. The newer count-batch path already prepares every request in bounded source portions. `AdvanceSummaryPreparation` takes a temporary `RequestSourceRange`, dispatches GPU source resolution/HLOD summary work, queues bounded missing-source recovery, and releases that portion only after it resolves.
4. The coordinator comment explicitly states that the whole request should watch edits without keeping every source resident; source portions separately own demand/active leases.
5. A missing persistent-directory source resolves to the dense-cache empty representation, so unresolved occupied source can manifest as missing geometry rather than merely a delayed counter.

Pressure-retirement H2 was independently falsified: victim selection excludes current/pending replacement ownership and host acknowledgment validates live handle/generation. Clipmap discovery replay and GPU cutover for steps 1/2/4/8 were also traced and do not explain this baseline.

## Red regression and selected repair

Added `GpuPersistentSourceDemandOwnershipTests.AdmittedRequestWatchesEditsWithoutRetainingWholeSourceFootprint` at pre-fix feature SHA `c3a3ddb1051d478cb955f4a0e1daf323293983c8`. It uses a real bound voxel world, real `GpuSurfaceExtractionContext`, and real page arena. For steps 1/2/4/8 an admitted request must retain its edit/version watch while `GpuSurfaceMirrorCoordinator.DemandFootprintCount` remains zero.

Red CI transport `41cd5e3b07c7d5a9d8c4af87dbd1c360a7191fe4` / run `34163251595` / job `101869196874` completed on Unity `6000.5.6f1`. The requested regression failed exactly on pre-fix steps 1/2/4: each retained one whole-request source-demand footprint (`Expected: 0`, `But was: 1`). Step 8 passed, matching its pre-existing edit-watch-only path. The run reached the requested EditMode test normally (80 seconds, 6208 MB peak) and was not an infrastructure failure. This is the required red proof for the ownership defect.

Selected production repair is deliberately narrow: all admitted steps use `RequestEditWatch` for whole-request invalidation; only count-batch source-preparation portions own `RequestSourceRange` residency. No global capacity, draw distance, content, quality or device budget changes.

Production repair commit: `0c65f67b4e1ef1518923fb24a19eb4c8170eb707`.

## Verdict / next step

H1 source-residency ownership is now directly proven by both the 180-second product baseline and the pre-fix red regression. Run the same focused regression on the repaired exact feature SHA next. If green, replay the owning SolidGpu production validation and full 180-second VoxelShowcase capture. The repair is accepted only if source demand remains bounded, persistent occupied-visible holes converge away under the existing deadline, and the fix does not weaken quality/capacity or introduce fallback.
