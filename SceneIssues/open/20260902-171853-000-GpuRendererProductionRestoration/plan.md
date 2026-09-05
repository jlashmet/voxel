# GPU renderer production restoration — implementation plan

**Acceptance scene:** `Assets/Scenes/VoxelShowcase.unity`. CPU-only VoxelShowcase must be production-quality before GPU-specific acceptance resumes.  
**Starting SHA:** `b18d470f66221c7cb6091249f4683c2d994bffec`.

## Current state

GPU density/semantic/topology parity and the minimal one-chunk GPU fixture are already proven. Full-scene acceptance remains red.

CPU-only run `33978398855` showed large white/gray far-feature masses with GPU solid requests at zero. The first wrong boundary was material presentation: canonical material/style/coating survived the bake, but `ProceduralFarFeatureRenderer` used shader defaults. The selected generic repair resolves installed material/coating identity in Composition to semantic-free albedo/roughness values, preserves them through `ShowcaseFarFeatureStateAdapter`, and applies them in the shared far renderer.

Run `33987770257` built/replayed VoxelShowcase after that repair. It is defect evidence, not a successful gate: material appearance improved, but a huge rectangular far proxy and other blockout-like masses remained. The geometry trace now shows the next wrong boundary: canonical `Primitive` carries direction/profile/radii/C/D/arc data, while `FarFeatureGeometryPrimitive` retains only shape + AABB + axis; Rendering then maps prism/ramp/rounded-box/ellipsoid/frustum/capsule to boxes and annulus/arc-wedge to full cylinders.

The historical agent-1 branch also contained unrelated prior-assignment deletions. It was rebuilt on current `master` and, after `SmallVoxelShowcaseSharedInputSystemRestoration` merged, rebased again to master `cd77b927...`. Master now owns and has exact-SHA proof for the shared Input System/runtime validation prerequisite (`7e6c609c...`, run `33988857330`); this feature no longer carries a competing input implementation.

Clean run `33991474823` exposed a separate Composition test-ownership defect: a new NUnit regression sat under production `VoxelEngine.Composition`, so player linking tried to resolve `nunit.framework`. Composition now owns a module-local EditMode asmdef for that regression.

## Hypotheses / next discriminator

1. **Geometry representation loss:** the giant slab is produced by AABB fallback for a non-box canonical primitive. This is the leading hypothesis from both capture and code trace.
2. **Already-box-shaped source:** the slab could instead originate from a canonical box bake; this is falsified if a focused regression identifies a non-box source whose far payload/render mesh becomes a box.

Next: exact-SHA validation on the rebased branch, then CPU VoxelShowcase replay. If the slab persists, fix the first proven primitive mismatch generically; no scene names, coordinates, or material IDs.

## Module ownership

- `VoxelEngine.Rendering`: player-visible; `Assets/VoxelEngine/Rendering/Validation/FarWorld/` owns focused player validation.
- `Game/Composition/Showcase/SceneRuntime`: Input System/runtime validation is now current-master authority; this GPU feature only changes far-state presentation there.
- `VoxelEngine.Composition`: headless selection/value projection; its module-local EditMode assembly is the appropriate focused validation surface.

## Remaining gates

Exact CPU proof -> generic far-geometry correction if required -> production-quality CPU traversal/stationary proof -> restore normal GPU cutover -> deterministic GPU parity/publication/lifetime/streaming/edit/performance/no-fallback acceptance -> final exact-SHA gates -> close -> current-master merge -> PR + auto-merge -> verify closed issue on `origin/master`.
