# Canonical far-frustum discriminator

## Exact fail-before result

- Feature source: `da3f5be338c57f5fe99ad4324405422e78c3918e`.
- Request: `6ddc72724c6653538be5c5a9818ebee059726264`.
- Workflow: [33999899224](https://github.com/jlashmet/voxel/actions/runs/33999899224), job `101396766672`, terminal **failure**.
- Artifact: `single-test-33999899224`, ID `9979637933`, ZIP SHA-256 `b1dcb7411925bcf59c6d3cd67c58643486b5424bc6c70661ecbf7f0999ba2cbe`.
- Inspected `ModuleValidation/Results/Tests/Persistent/persistent-summary.txt`, `persistent-failures.txt`, and all thirteen `persistent-editmode-*.txt` result files. Total: **649 passed, 8 failed, 0 skipped, 0 quarantined failures, 0 inconclusive**. Rendering: 289 passed and the eight new failures; its assembly phase took 6.273 seconds. This was a behavioral product failure, not a compiler/import failure or zero-match run.

All failures are `FarFeatureFrustumGeometryTests.FrustumSilhouetteMatchesCanonicalTaper`, at the assertion comparing the production mesh's transverse intersection with integer `PrimitiveRasteriser.Contains`. Other topology/cache assertions follow this assertion and were therefore not reached by these failing cases.

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

The real canonical emitter -> bake -> Composition adapter -> production mesh path reproduced loss of taper on every axis, both directions and both scales. That proves this lossy geometry boundary. It does **not** identify every malformed region in the full-scene capture.

## Built-player before image

The failed workflow's always-run VoxelShowcase replay produced `SceneIssue/verification-final.png` and stationary captures at 15.1, 25.1 and 35.1 seconds. Inspected the final PNG: castle visible, giant left slab and right-hand white/tan blockout geometry remain. Classification: **prototype/blockout quality**. `SceneIssue/player-run.log` reports `gpu[req=0 ... pub=0]`; this is CPU diagnostic evidence, never GPU success. The replay also logs `SCENEISSUE issue.json has no replayable camera snapshot.` The supplied issue has empty captures and no camera property; this replay used the scene's default view, not a successfully pinned snapshot. Frame-pipeline timing entries with `n=0` are not performance proof.

## Pass-after request ownership

Repair `a164456a9eac5091ec3e5d6c2e03a9de7b675199` is included in exact feature `e4e2f9975dc2d3f3d437b5bfe3f853b6f2cf468b`. Request `fc6c3320d9b986b8d2401fcae0a17de80d286691` is its direct child and requests the same behavioral test plus the 45-second VoxelShowcase replay. Run `34003412217`, job `101406207152`, remained queued with no assigned runner during this review. Leave that request untouched until terminal; no repaired Unity result or new repaired image has been claimed.
