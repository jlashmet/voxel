# Character Factory

Headless local asset factory for modular characters. Character bodies, clothing, weapons, and
accessories are deliberately separate products; a dressed character is assembled at runtime rather
than baked into one generated mesh.

## Boundary layout

The Unity side follows the engine subsystem rule:

```text
Assets/VoxelEngine/Characters/
  Api/       stable equipment contract
  Runtime/   catalogue, part authoring assets, skeleton rebinding/socket attachment
```

Consumers depend on `VoxelEngine.Characters.Api`; concrete Unity implementation stays in
`VoxelEngine.Characters.Runtime`.

The offline factory mirrors that separation and now has four explicit pipelines:

```text
tools/character-factory/
  api/                       stable JSON build-spec model
  runtime/
    pipeline.py              dispatcher only
    pipelines/
      character.py           body mesh -> canonical skeleton -> FBX
      clothing.py            garment mesh -> canonical weights -> FBX
      weapon.py              rigid weapon mesh -> FBX + socket metadata
      accessory.py           rigid accessory mesh -> FBX + socket metadata
```

There is intentionally no generic `wearable` pipeline. The category is part of the build contract so
we can evolve character fitting, garment fitting, weapon grip/origin processing, and accessory mount
processing independently.

## Pipeline behavior

All four pipelines share only the low-level multiview mesh-generation adapter. The post-processing
path is different by asset class:

```text
character
  4 views -> Hunyuan3D-2mv -> generated body GLB
          -> Blender character pass
          -> transfer canonical body weights / canonical armature
          -> character FBX

clothing
  4 views -> Hunyuan3D-2mv -> garment GLB
          -> Blender clothing pass
          -> transfer canonical body weights / canonical armature
          -> independent clothing FBX

weapon
  views -> Hunyuan3D-2mv -> rigid GLB
        -> Blender rigid weapon pass
        -> weapon FBX + hand/socket metadata

accessory
  views -> Hunyuan3D-2mv -> rigid GLB
        -> Blender rigid accessory pass
        -> accessory FBX + bone/socket metadata
```

The manifest records the exact pipeline, output FBX, generator/prepare commands, and runtime-part
metadata. Clothing is marked `SkinnedToCharacterSkeleton`; weapons/accessories are marked
`BoneSocket`.

## Current limitations

The character and clothing passes currently automate canonical weight transfer, not robust geometric
fitting. Generated meshes must already be reasonably aligned to the canonical body. Automatic body
conforming, loose-garment-aware fitting, collision/poke-through correction, body-region hiding, LODs,
weapon grip inference, accessory mount inference, and automatic Unity prefab/`WearableAsset` creation
are subsequent stages. Keeping the pipelines separate means those algorithms can be added without
turning one generic postprocessor into a pile of category conditionals.

Accessories are currently rigid/socket-mounted. Skinned hair/capes should use the clothing pipeline
until a dedicated skinned-accessory mode is implemented.

## Commands

Dry-run one pipeline without requiring the placeholder example files to exist:

```bash
python3 tools/character-factory/character_factory.py build \
  tools/character-factory/examples/cleric_character.json --dry-run

python3 tools/character-factory/character_factory.py build \
  tools/character-factory/examples/cleric_robe.json --dry-run

python3 tools/character-factory/character_factory.py build \
  tools/character-factory/examples/cleric_staff.json --dry-run
```

Run the routing/spec tests:

```bash
python3 -m unittest discover tools/character-factory/tests -v
```

Every build writes a `manifest.json` beside the generated assets so later import/resume stages can be
added without changing the public factory API.
