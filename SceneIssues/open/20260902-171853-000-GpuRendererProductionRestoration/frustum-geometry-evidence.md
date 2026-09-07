# Canonical far-frustum discriminator

## Exact fail-before result

- Feature source: `da3f5be338c57f5fe99ad4324405422e78c3918e`.
- Request: `6ddc72724c6653538be5c5a9818ebee059726264`.
- Workflow: [33999899224](https://github.com/jlashmet/voxel/actions/runs/33999899224), job `101396766672`, terminal **failure**.
- Artifact: `single-test-33999899224`, ID `9979637933`, ZIP SHA-256 `b1dcb7411925bcf59c6d3cd67c58643486b5424bc6c70661ecbf7f0999ba2cbe`.
- Inspected `ModuleValidation/Results/Tests/Persistent/persistent-summary.txt`, `persistent-failures.txt`, and all thirteen EditMode result files: **649 passed, 8 failed, 0 skipped, 0 quarantined failures, 0 inconclusive**. Rendering: 289 passed and eight intended failures; its assembly phase took 6.273 seconds. This was a behavioral product failure, not compilation/import failure or zero-match evidence.

All failures are `FarFeatureFrustumGeometryTests.FrustumSilhouetteMatchesCanonicalTaper`, comparing production mesh transverse intersections with integer `PrimitiveRasteriser.Contains`. Topology/cache assertions follow that assertion and were not reached in the failing cases.

| Axis | Direction | Base / end radius | Voxel metres | Expected radius, tolerance 1.25 voxels | Actual radius, rounded |
|---|---|---|---|---|---|
| X | + | 24 / 6 | 1.0 | 11.5 | 24.5 |
| X | - | 24 / 6 | 0.1 | 11.5 | 24.5 |
| Y | + | 24 / 6 | 0.1 | 11.5 | 24.5 |
| Y | - | 24 / 6 | 1.0 | 11.5 | 24.5 |
| Z | + | 24 / 6 | 1.0 | 11.5 | 24.5 |
| Z | - | 24 / 6 | 0.1 | 11.5 | 24.5 |
| Y | + | 6 / 24 | 1.0 | 19.5 | 24.5 |
| Z | - | 24 / 0 | 0.1 | 6.5 | 24.5 |

The real emitter -> bake -> adapter -> mesh path reproduced loss of taper on all axes, both directions and scales. It does not identify every malformed full-scene region.

## Exact pass-after result

- Repair: `a164456a9eac5091ec3e5d6c2e03a9de7b675199`, included in tested feature `e4e2f9975dc2d3f3d437b5bfe3f853b6f2cf468b`.
- Direct-child request: `fc6c3320d9b986b8d2401fcae0a17de80d286691`; left untouched until terminal.
- Run [34003412217](https://github.com/jlashmet/voxel/actions/runs/34003412217), job `101406207152`, **completed/success**.
- Artifact `9980566933`, `single-test-34003412217`, ZIP SHA-256 `b902646131564cfb16367d0e05e329951a3232c583aa56691b1a998f8e0f03fa`.
- Inspected persistent summaries: **657 module EditMode passes**, **3 PlayMode passes**, plus **8 repeated requested taper passes**. No failed/skipped/inconclusive cases. Rendering has 297 passing tests. The explicit `persistent-requested.txt` identifies the exact taper test and eight passes, so topology, winding, closure and cache assertions were reached successfully.

The module summary records nine successful player executions and canonical Kentridge integration. **Not all durable module-player proof survives:** FarWorld and Water both used `Players/Assets_VoxelEngine_Rendering`; the retained log says `WATER_VALIDATION liquid-ready` and captures belong to Water. The FarWorld images/log were overwritten. Do not mark module-player artifact acceptance complete from this summary alone.

## Inspected production images and limits

Both runs produced `SceneIssue/verification-final.png` and stationary images. The repaired final image replaces the left rectangular AABB with a sloped taper while keeping the detailed castle visible. The left surface remains flat white and right-hand white/tan masses remain malformed. Classification: **prototype/blockout quality**, not CPU visual acceptance. Actual player logs continue to report `gpu[req=0 ... pub=0]`; neither image is GPU proof.

Both replays report `SCENEISSUE issue.json has no replayable camera snapshot.` Capture-less feature metadata selected the scene default rather than a pinned stored snapshot. Zero-sample frame-pipeline entries (`n=0`) are not measured performance. Final acceptance still needs representative stationary/traversal views with valid pose and timing evidence.

## Required continuation

CPU4E: runner output identity now includes module, scene and scenario, with the actual output path recorded in the summary. Filesystem regressions execute the real Python runner with simulated capture processes: the exact old script blob `332ecc949991e40e9f29a145b25a9dac5052c59e` fails four collision/preservation cases; the correction passes all five cases, including preserved process-failure propagation (0.012 s locally). Real standalone artifact preservation remains pending exact CI.

CPU2A: explicit `renderProbe: far-feature-visibility` requests a bounded diagnostic of the existing renderer. Capture at 25 s with normal visibility, suppress only far features during 30–40 s (35 s capture), restore at 40 s (45 s capture), terminate probe at 50 s, retain restored final output in a 55 s replay. The actual authored camera is held unchanged. Fourteen module-owned behavioral cases cover explicit opt-in, phase boundaries, real renderer enable/instance preservation and teardown. No new Unity result for the probe has been claimed. The suppressed view is owner evidence only, never final acceptance.
