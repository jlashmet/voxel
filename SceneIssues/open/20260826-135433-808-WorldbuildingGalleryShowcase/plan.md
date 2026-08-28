# Plan — 20260826-135433-808 WorldbuildingGalleryShowcase

## Evidence
- One marked region: `screenshot-001.png`, normalized center `(0.4802, 0.6678)`, radius `0.0690` (about pixel `(926, 558)` at the captured 1928×836 pose). Human review describes the meadow there as repeated three-blade icons/dark billboard bars instead of the dense layered target in `reference-grass-target.jpg`.
- The production path batches semantic vegetation through `ProceduralVegetationBatchRenderer`. Ordinary tuft foliage used the generic seven rectangular-card mesh, which explains the repeated stamped silhouettes.
- The SceneIssue includes `grass-renderer-reference.shader`; its packed root/lateral/height/phase vertex contract, coherent three-wave wind, camera-right reconstruction, local player push, and stateless recovery are the acceptance implementation to migrate.

## Competing hypotheses
1. **Wrong grass geometry/presentation — confirmed.** Generic foliage cards cannot reproduce the supplied tapered ribbon meadow. Give the meadow grass family a dedicated packed ribbon mesh and production grass shader.
2. **Interactor publishing is missing — rejected.** Production already publishes a bounded grass-interactor array; the migrated dedicated shader was not consuming it. Apply the reference push math to the strongest local interactor while leaving the existing publisher intact.
3. **Whole vegetation renderer/layout is wrong — rejected for this capture.** The defect is isolated to the marked meadow presentation. Keep flowers, aquatic grass, reeds, surfaces, vines, woody vegetation, instance placement, and batching unchanged.

## Fix + regression
- Port `grass-renderer-reference.shader` into `Assets/VoxelEngine/Rendering/Runtime/Shaders/ProceduralVegetationGrass.shader` and keep all per-frame blade deformation on the GPU.
- Pack 11 tapered, segmented ribbon blades per grass tuft with deterministic root/height/width/phase and regional vertex colors; route only `Grass`, `Clover`, `Weed`, `Nettle`, and `DeadGrass` through that meadow path.
- PlayMode regression uses the imported production mesh/material/shader: assert packed topology and unaffected non-meadow kinds, render front + 90° orbit views, apply a nearby production interactor, and verify local displacement plus return to the deterministic baseline after the interactor leaves.
- Replay the saved WorldbuildingGallery camera pose at native 1928×836 for final visual evidence after exact-SHA CI.

## Blast radius / cost
- Shared batching remains one `Graphics.DrawMeshInstanced` path with the same instance/draw count and no per-frame CPU grass vertex rewrites.
- Meadow tuft geometry rises from the old generic 28 vertices / 14 triangles to 110 vertices / 88 triangles per instance (about 3.9× vertices, 6.3× triangles). This cost is limited to the five meadow kinds above; flowers, wetland plants, and other foliage keep existing meshes/shaders.
