# Experiment 006 — dedicated production validation scene

User direction: provide a dedicated validation scene for this feature, but it must use the real production voxel/world-generation, material, vegetation/tree, and rendering paths rather than primitives or one-off presentation code.

Plan:
- Keep reusable secret/clue planning and cave composition in production modules.
- Add a purpose-built validation scene only as a consumer/composition root.
- Author the world through production WorldBuilder / structure authoring APIs and render it through the normal voxel renderer and production material/presentation systems.
- Do not use `GameObject.CreatePrimitive`, hand-built cubes/planes, bespoke fake trees, custom one-off materials/shaders, or capture-only geometry.
- Register the scene as built-player validation for the worldbuilder secret-discovery module.
- Validate at gameplay scale with the clue readable before discovery, the secret route physically traversable/interactive as intended, and no foreground vegetation obscuring the subject.

Acceptance evidence will come from the dedicated built-player scene; `WorldbuildingGalleryShowcase` remains a thin final integration consumer rather than the development surface.
