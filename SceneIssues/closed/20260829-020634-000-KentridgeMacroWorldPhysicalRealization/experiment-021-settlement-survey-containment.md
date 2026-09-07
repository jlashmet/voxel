# Experiment 021 — settlement survey containment minimal repro

Two materially different validation-camera corrections left the same one-readable-building acceptance symptom, so no third camera change is permitted until the framing root cause is isolated.

Exact run `33370392969` completed the readiness-aligned experiment-020 discriminator: after all four Rossdam content columns settled and publication stabilized, all four authored building bounds intersected the real survey-camera frustum and each intersected four ready production solid chunks at source step 2. The full-resolution capture nevertheless showed only one unmistakable complete blockout. This rejects generation/storage/grounding/readiness and coarse publication, but `GeometryUtility.TestPlanesAABB` proves only frustum **intersection**, not full containment/readability.

Exact run `33372188117` then lowered the settlement survey from 70 m to 45 m without changing rotation; workflow gates were green but the resulting full-resolution settlement frame was cropped/mis-aimed. Exact run `33373258471` tested a second material correction that dollied along the existing focus ray. The built player again produced a cropped one-building Moordell survey, while the focused regression failed only on its very strict focus-angle tolerance (`0.027976°` versus `<0.01°`). This is a product-red source and is not eligible for same-SHA rerun.

Minimal framing repro from production dimensions and the exact scene lens:

- `TopDownWorldPhysicalPlanner` places the four generic plots at `±190 dm` in X/Z with half extents `68 dm x 52 dm`.
- The complete four-building footprint therefore spans X `516 dm` and Z `484 dm`.
- The diagonal survey view sees a ground-axis half-span of approximately `(25.8 m + 24.2 m) / sqrt(2) = 35.36 m` before extra terrain/roof margin.
- `KentridgePlayableSlice.unity` uses a 58° perspective camera.
- Ignoring terrain variation, the authored focus is 8 m above ground while the driver camera is 70 m above its ground, giving roughly 62 m vertical separation. A 58° vertical FOV covers only `62 * tan(29°) ≈ 34.37 m` per side on that axis, already smaller than the 35.36 m settlement footprint.
- Lowering the camera cannot solve containment; it necessarily narrows that footprint further. This exactly explains how all four AABBs can intersect the frustum while only one/two structures read fully in the frame.

Root cause: validation survey **framing containment**, not missing voxel generation or renderer publication. The next correction must keep the established semantic camera/focus position and widen only the validation settlement lens enough to contain the four-plot envelope, restoring the normal scene lens for non-settlement evidence. Production generation, streaming, residency, LOD policy, renderer budgets, and normal gameplay camera behavior must remain unchanged.
