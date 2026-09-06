# Experiment 024 — survey lens execution order

## Observation
Exact run `33384191758` is workflow-green. The independent generic shell regression passes, and settlement convergence improves enough to capture Moordell around t40 and Rossdam around t57. Rossdam building publication now reports roughly 100k–120k ready indices per blockout rather than experiment 019's ~1.03–1.20M solid structures.

Direct full-resolution inspection is still closure-red: Rossdam's lower structure is clipped and only a subset of the four authored blockouts reads clearly. This contradicts experiment 022's exact 90-degree projected-envelope regression, which places every authored building corner inside a 4% viewport margin.

## Root cause
The contradiction is explained by frame execution order. `KentridgePlayableSlice.Update` restores the camera object to the CharacterMotor eye position every gameplay frame. The evidence driver applies the 70 m settlement survey pose in its `LateUpdate`. The separate settlement-lens composition also used `LateUpdate` with default execution order.

With no ordering guarantee, the lens component can execute first, inspect the temporary player-height pose, decide it is not a settlement survey, and restore the normal 58-degree lens. The evidence driver then applies the 70 m survey pose later in the same frame. `ScreenCapture.CaptureScreenshot` is written at end of frame, so the captured pose is the survey pose but the lens can still be the normal lens. That exactly matches a screenshot which clips despite the 90-degree projection proof.

## Correction and regression
Keep lens policy in the validation-only composition, but give it an explicit late execution order so it runs after the evidence driver's default-order `LateUpdate`. No production camera, streaming, renderer, worldgen, LOD, or residency policy changes.

A focused reflection regression requires the lens composition's `DefaultExecutionOrder` to be greater than the evidence driver's effective order.

## Gate
Run the execution-order regression on the exact current feature SHA and inspect the same 60-second built-player settlement frames. If the 90-degree lens is now visibly active, all four blockouts should fit the proven projection envelope. If not, log/measure the end-of-frame camera FOV/pose before any further framing correction.
