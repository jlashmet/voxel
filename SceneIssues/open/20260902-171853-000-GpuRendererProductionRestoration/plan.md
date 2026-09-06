# GPU renderer production restoration — implementation plan

**Acceptance:** `Assets/Scenes/VoxelShowcase.unity` only. Production-quality CPU stationary/traversal presentation precedes restoring GPU cutover. CPU captures never count as GPU acceptance. Current production/test source: `e4e2f9975dc2d3f3d437b5bfe3f853b6f2cf468b`; latest fetched master: `ef475182b866eabfe8e1d1a39c82bf7810a03f49`.

## Observed result and hypotheses

Material baseline feature `fc767620...`, request `8e6aac9...`, run `33996360570` passed automation but its CPU-only VoxelShowcase captures remain **prototype/blockout quality**: castle visible, giant left slab and right-hand masses unresolved. GPU request/publication counters are zero.

1. **Lost frustum taper:** confirmed at the canonical-to-far boundary. The production mountain emits a frustum, but the old adapter discarded centre/direction/radii and the renderer substituted an AABB. The exact before-fix regression now fails for the intended reason, not compilation or infrastructure.
2. **Additional owners/near-far overlap:** the image may also contain canonical boxes or overlapping presentation. `ShowcaseFarFeatureRuntime.Update` submits all selected proxies without consulting detailed publication readiness. Terrain already uses `RenderingComposition.HasCompletePublishedNearSurfaceCoverage`; it is an aggregate view signal, not proof of an individual feature's coverage. Do not introduce distance-only hiding or claim every slab is fixed.

## Exact fail-before evidence

Source `da3f5be338c57f5fe99ad4324405422e78c3918e`, request `6ddc72724c6653538be5c5a9818ebee059726264`, run `33999899224`, job `101396766672`: **completed/failure**, as expected. Artifact `9979637933` (`single-test-33999899224`), `ModuleValidation/Results/Tests/Persistent/persistent-failures.txt` and `persistent-editmode-12.txt`: 289 Rendering tests passed, exactly eight `FarFeatureFrustumGeometryTests.FrustumSilhouetteMatchesCanonicalTaper` cases failed at line 81. Representative expected radius 11.5 +/- 1.25 voxels, actual 24.5. The other twelve completed EditMode assemblies passed. The separate 45-second player replay succeeded but remains before-fix defect evidence; required module/player gates did not all run.

## Repair, ownership and current request

Candidate `a164456a9eac5091ec3e5d6c2e03a9de7b675199` preserves signed cap centres/radii as renderer-neutral normalized values. The existing renderer tessellates 50 vertices / 96 triangles per frustum with revision-based caching. No scene-specific recipe, new renderer or authoritative-state change. Eight regressions cover axes/directions, negative coordinates, unequal normalization, voxel scales, zero-radius endpoints, winding, closure and caching.

Rendering owns `Assets/VoxelEngine/Rendering/Validation/FarWorld/`; its existing player now includes forward/reverse frusta. Composition's selection/value projection is headless and uses module-local EditMode coverage; no separate Composition scene applies. Showcase SceneRuntime retains its master-owned input validation pair. The render-ready module tableau is not a substitute for canonical VoxelShowcase visual proof.

Pass-after request **`fc6c3320d9b986b8d2401fcae0a17de80d286691`**, run **`34003412217`**, job `101406207152`, is **queued** at the latest observation, directly parented by exact feature `e4e2f997...`. Do not replace queued/running work. After termination inspect tests, required module/player proof, VoxelShowcase PNGs and GPU counters before marking TGPU-019CPU4F complete.

## Remaining gates

Production-quality CPU stationary/traversal evidence; remaining geometry/material/handoff corrections; then retained GPU reconciliation, deterministic parity, paging/lifetime, streaming/edit, no-fallback, performance and independent-consumer proof. All required gates/checklists precede `open` -> `closed`, current-master integration and PR + auto-merge.
