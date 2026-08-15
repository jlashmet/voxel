# Character Factory

Headless local pipeline for generating **independent character parts and wearables**. It deliberately
does not generate a dressed character. Unity owns a canonical character skeleton; clothing, hair,
capes, and equipment are separate assets that can be swapped at runtime through
`VoxelEngine.Characters.Api`.

## Architecture

The Unity side follows the engine's subsystem boundary rule:

```text
Assets/VoxelEngine/Characters/
  Api/       stable equipment slots + ICharacterEquipment
  Runtime/   catalogue, wearable authoring assets, skeleton rebinding
```

No other subsystem needs to reference `VoxelEngine.Characters.Runtime`. Gameplay code should consume
`VoxelEngine.Characters.Api`; composition/authoring may wire the Runtime implementation.

The offline tool mirrors that separation:

```text
tools/character-factory/
  api/       JSON build-spec model
  runtime/   Hunyuan + Blender process adapters
```

## Current vertical slice

```text
4 reference views
    -> local Hunyuan3D-2mv Python environment
    -> raw GLB
    -> Blender in background mode (wearables only)
       - import canonical body + armature
       - transfer canonical skin weights by nearest surface
       - attach the canonical armature
       - export independent wearable FBX
    -> Unity's built-in FBX importer
    -> WearableAsset / CharacterEquipmentController.TryEquip(partId)
```

Hunyuan is invoked directly from Python; no browser or paid web API is used. The model/environment is
not vendored into this repository.

The Blender stage currently assumes the generated wearable is already aligned to the canonical body.
It automates skin-weight transfer and export, but it does **not yet** solve silhouette fitting,
collision clearance, body-region hiding, cloth simulation, LOD generation, or automatic creation of
the Unity `WearableAsset`/prefab. Those are explicit follow-up stages so failed automatic fitting
cannot be mistaken for a production-ready asset.

## One-time local setup

1. Install Hunyuan3D-2 in a dedicated local Python environment.
2. Install Blender for macOS.
3. Create/export the canonical body + canonical armature GLB once.
4. Point a build spec at the Hunyuan Python executable and Blender executable.

The generator adapter defaults to `tencent/Hunyuan3D-2mv` /
`hunyuan3d-dit-v2-mv` and automatically selects CUDA, Apple MPS, then CPU.

## Commands

Validate the pipeline shape without requiring the placeholder example files to exist and without
starting Hunyuan or Blender:

```bash
python3 tools/character-factory/character_factory.py build \
  tools/character-factory/examples/cleric_robe.json --dry-run
```

Build one asset:

```bash
python3 tools/character-factory/character_factory.py build path/to/cleric_robe.json
```

Build a directory of specs sequentially:

```bash
python3 tools/character-factory/character_factory.py batch path/to/specs
```

Every build writes a `manifest.json` beside the output so a failed/batch pipeline can later be made
resumable without changing the Unity API.
