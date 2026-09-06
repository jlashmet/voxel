# GPU renderer production restoration — implementation plan

**Acceptance:** `Assets/Scenes/VoxelShowcase.unity` only. Production-quality CPU stationary/traversal presentation precedes GPU cutover; CPU images never count as GPU proof. Tested source `e4e2f9975dc2d3f3d437b5bfe3f853b6f2cf468b`; fetched master `ef475182b866eabfe8e1d1a39c82bf7810a03f49`.

## Exact result

Before source `da3f5be...`, request `6ddc727...`, run `33999899224` failed exactly eight canonical taper cases; 649 other EditMode tests passed. See [frustum-geometry-evidence.md](frustum-geometry-evidence.md).

Pass-after request `fc6c3320d9b986b8d2401fcae0a17de80d286691`, run `34003412217`, completed **success** without replacement. Artifact `9980566933`, ZIP SHA-256 `b902646131564cfb16367d0e05e329951a3232c583aa56691b1a998f8e0f03fa`: 657 module EditMode tests, three PlayMode tests, and eight repeated focused taper cases pass with no skips/failures. The canonical emitter -> adapter -> renderer repair is proven, including topology and cache assertions.

Inspected `SceneIssue/verification-final.png`: the left AABB is now tapered, but its white flat surface and the right-hand malformed masses still fail CPU acceptance (**prototype/blockout quality**). GPU requests/publications remain zero. Default-view replay logs no replayable camera snapshot; zero-sample frame timings are not performance proof.

The module summary reports nine player runs, but FarWorld and Water share `Players/Assets_VoxelEngine_Rendering`; the retained log/captures belong to Water. Required FarWorld evidence was overwritten. Preserve separate per-scene/scenario outputs before accepting module-player proof. This is a demonstrated validation defect, not permission to remove a target or weaken assertions.

## Hypotheses / next discriminator

1. **Semantic far-feature overlap/approximation:** `ShowcaseFarFeatureRuntime.Update` submits all selected proxies without per-feature detailed publication readiness. Other shapes still use AABBs and carves are omitted.
2. **Another presentation owner/source:** remaining masses could instead be CPU voxel geometry or far terrain; source shapes may already be boxes.

Next exact VoxelShowcase replay will use an explicitly opted-in, bounded normal/disabled/restored comparison of the existing far-feature renderer, at the same scene-authored camera. The disabled interval is diagnostic only, never visual success or a production fallback policy. Do not hide proxies by distance or global convergence. The comparison must restore original enable states and retain normal final output.

## Ownership and scope

Rendering owns canonical regressions and `Rendering/Validation/FarWorld/`. Headless Composition uses module-local EditMode tests. Showcase SceneRuntime owns replay orchestration and its existing validation scene; bounded instrumentation may toggle the existing renderer but must not create geometry/materials or mutate world state. Add behavioral restoration/opt-in regressions. `tools/run-module-validation.py` owns artifact isolation; prove the collision and preservation with filesystem-based tests.

## Remaining gates

Resolve required artifact isolation and CPU draw ownership, then correct proven geometry/material/handoff defects. Obtain clean CPU stationary/traversal proof before GPU reconciliation, parity, publication/lifetime, edits/streaming, no-fallback, performance and independent-consumer validation. Keep all unmet checkboxes open. Only full acceptance permits `open` -> `closed`, current-master integration and PR + auto-merge.
