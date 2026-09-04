# Experiment 025 - Rendering composition boundary

## Trigger

Exact-SHA run `33844103873` validated source `54bc99fb18524ebb007be081fb9bde858e8b2b6e` through request `d003d5ad69f23b47278a8a6157957b7471c716bb`. Automatic module planning correctly selected CaveWorldBuilder, Showcase, WorldBuilder, and Kentridge, but Unity stopped before tests because `WorldbuildingGallerySecretDiscoveryAcceptance.cs` could not resolve `VoxelEngine.Rendering.Runtime`.

## Competing hypotheses

1. **Showcase should directly consume `VoxelRenderBridge` from Rendering.Runtime and only needs another asmdef reference.**
   - Discriminator: inspect `Game.Composition.Showcase.asmdef` and the renderer ownership boundary.
   - Result: **rejected**. Showcase already references `VoxelEngine.Rendering.Runtime`. More importantly, `RenderingComposition` explicitly owns the application-facing renderer boundary and documents that concrete renderer bridges remain private to Rendering.Runtime.

2. **The evidence harness crossed the renderer ownership boundary; convergence should be observed through the existing application-owned composition diagnostics.**
   - Discriminator: verify a post-pin readiness predicate can be expressed with existing `RenderingComposition` APIs without adding a new production module contract.
   - Result: **supported**. `ResetSurfacePassDiagnostics` establishes a fresh post-pin diagnostic epoch and `GetVoxelSurfaceCounts` exposes visible/missing solid chunks allocation-free. Two consecutive frames with `visible > 0` and `missing == 0` preserve the same readiness invariant without reaching into the runtime bridge.

## Fix

`WorldbuildingGallerySecretDiscoveryAcceptance` now imports `VoxelEngine.Composition`, resets surface-pass diagnostics after the camera is pinned, and waits for two consecutive complete visible-surface frames through `RenderingComposition.GetVoxelSurfaceCounts`. The previous temporary direct mutations of renderer-internal budgets were removed because Showcase cannot safely read/restore those private values through its supported boundary.

This is a SceneIssue evidence synchronization change only: no cave geometry, clue placement, storage state, renderer algorithm, production renderer defaults, or runtime presentation policy changed.

## Next discriminator

Fresh exact-SHA CI must compile, pass the Showcase content-dirty publication regression and all automatically selected module/player gates, log `SECRET_DISCOVERY_RENDER_CONVERGENCE result=PASS ... missing=0`, and produce a full-resolution authored-breakable frame that no longer shows underside/void. If the composition-level readiness predicate passes but the image is still invalid, inspect render-pass/chunk ownership at the target instead of changing camera or publication semantics again.
