# Experiment 006 — structural evidence streaming convergence

## Symptom
Exact-SHA run `33345454005` passed all three focused PlayMode tests and all built-player traversal/negative assertions, but the dedicated structural audit aborted before its first source frame:

`STRUCTURAL_AUDIT result=FAIL reason=evidence-streaming-not-settled frame=1 proof=0 pending=78`

The outer capture consequently emitted only five generic 10-second screenshots before the 60-second replay ended. Those wrapper frames are fallout, not the primary failure.

## Minimal repro / root cause
The evidence coroutine relocates the production `CharacterMotor` to a remote proof site, seeds a bounded line-of-sight strip synchronously, restores structural authoritative voxels after relocation, then requires `PendingRegionLoads == 0` within four seconds. `PendingRegionLoads` is the complete production wanted set. `WorldbuildingGalleryShowcase.Update` drains that set with the normal interactive per-frame streaming budget, which is intentionally too small to converge a freshly relocated mountain wanted set inside the audit-only four-second bound. In the failing run, 78 regions remained when the bound expired.

This distinguishes the defect from proof authoring, renderer lag, camera mismatch, and wrapper screenshot cadence: the audit fails closed before calling `ScreenCapture.CaptureScreenshot` for frame 1.

## Selected fix
Keep the acceptance unchanged (`PendingRegionLoads == 0` before every structural source frame) and keep production storage/streaming semantics unchanged. Add a SceneIssue-scoped evidence component under the Worldbuilding Gallery composition root. It activates only when the exact structural SceneIssue command-line argument is present and only while the audit has placed the gallery motor in fly mode; then it calls the existing production `StepStreaming` path with a 48 ms evidence budget after normal scene Update. This accelerates convergence rather than declaring incomplete residency acceptable.

No shared storage/renderer API, load radius, eviction policy, structural solver, geometry, or shipping scene budget is changed.

## Validation
Run the focused `VoxelEngine.Tests.PlayMode.WorldbuildingGalleryStructuralCompositionPlayModeTests` plus exact built-player capture at the supported 60-second maximum. Require all focused tests green, all traversal/negative contracts green, zero pending loads at every structural frame, all eight dedicated source PNGs, semantic wrapper PASS, and direct full-resolution visual review.
