# MountingForce World Generation

This package owns semantic world and location generation for MountingForce. It is deliberately separate from the voxel engine and from rendering.

## Dependency direction

```text
MountingForce.WorldGen.Core
        ^
        |
MountingForce.WorldGen.Voxel ----> VoxelEngine Storage/Terrain/Structures/Vegetation Api
        ^
        |
     Game / Composition ----------> VoxelEngine.Composition
```

`MountingForce.WorldGen.Core` must not reference `VoxelEngine`, `UnityEngine`, meshes, shaders, GameObjects, or material byte ids. It works in deterministic integer decimetres and semantic material roles.

`MountingForce.WorldGen.Voxel` is an adapter. It translates semantic plans into `VoxelEngine.Structures.Api.FeatureCatalogue` data and uses only the specific Storage, Terrain, Structures, and Vegetation Api assemblies required for realization. It never depends on an engine Runtime assembly or on rendering.

The renderer must not know what Kentridge, an inn, a quest location, or a druid grove is.

## Current vertical slice

Kentridge is the first content definition. The semantic package currently contains its stable roles, architecture theme, and a prototype settlement plan. The voxel adapter compiles that plan into distinct townhouse, shop, inn, warehouse, mansion, church, and well grammars.

The prototype coordinates are temporary plot decisions rather than story identity. Gameplay should bind to stable semantic role ids. A future road/plot solver can replace the current placement method without changing quests or the voxel renderer.

## Next layers

1. Add semantic streets, districts, entrances, and required connections to `SettlementPlan`.
2. Add terrain-aware plot preparation and road realization in backend adapters.
3. Add seeded architectural variation within each archetype.
4. Resolve generated anchors back to stable semantic roles for NPCs, chests, cutscenes, and entrances.
5. Generalize the same core contracts with non-human grammars for the druid grove, fairy village, and dwarf mountain.
