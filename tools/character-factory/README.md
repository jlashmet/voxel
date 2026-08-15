# Character Factory

Headless asset factory for modular Unity characters. Character bodies, clothing, weapons, and accessories are separate products; clothing and equipment are attached to a character at runtime instead of being baked into one generated mesh.

## Boundary layout

The Unity subsystem follows the engine boundary rule:

```text
Assets/VoxelEngine/Characters/
  Api/       stable equipment slots, part kinds, ICharacterEquipment
  Runtime/   catalogue, part assets, skeleton rebinding, socket attachment
  Editor/    automatic import of staged Character Factory outputs
```

Consumers depend on `VoxelEngine.Characters.Api`. Unity implementation details remain in `Runtime`/`Editor`.

The offline factory mirrors that separation and keeps four explicit pipelines:

```text
character   generated body -> canonical skeleton/weights -> FBX
clothing    generated garment -> canonical skeleton/weights -> FBX
weapon      rigid generated mesh -> FBX + socket metadata
accessory   rigid generated mesh -> FBX + socket metadata
```

There is intentionally no generic `wearable` pipeline. Character fitting, garment fitting, weapon processing, and accessory mounting can evolve independently.

## Generator backends

Product pipeline and mesh generator are independent choices.

Current backends:

- `triposr-mps`: fast Apple-Silicon smoke/prototyping path. It reconstructs a single image, bakes UV color, harmonizes source palette, and can project source pixels onto confidently aligned surfaces.
- `hunyuan-pytorch`: higher-quality Hunyuan path, including the multiview quality preset.

Current Hunyuan presets:

```text
smoke
  tencent/Hunyuan3D-2mini
  hunyuan3d-dit-v2-mini-turbo
  FlashVDM enabled
  5 diffusion steps

quality
  tencent/Hunyuan3D-2mv
  hunyuan3d-dit-v2-mv
  50 diffusion steps
```

Swapping generators does not change the `character`, `clothing`, `weapon`, or `accessory` pipeline contract.

## Accepted mechanics

The Apple-Silicon CI smoke now validates all of these paths with real TripoSR inference:

```text
weapon
  source image -> textured generated GLB -> rigid FBX
  -> detailed ornament + procedural shaft -> material-preserving render

character
  T-pose source -> generated textured body
  -> automatic global axis/scale/center alignment
  -> canonical 17-bone weight transfer
  -> skinned FBX -> re-import -> programmatic pose deformation

clothing
  isolated garment source -> generated garment
  -> donor alignment -> canonical weight transfer
  -> independent skinned FBX -> re-import -> pose deformation

modular composition
  separate character FBX + separate clothing FBX
  -> discard clothing's duplicate imported armature
  -> rebind clothing renderer to the character armature by canonical bone name
  -> pose one shared skeleton
  -> require both body and clothing to deform
```

These fixtures validate mechanics, not production art quality.

## Unity staging and automatic import

A completed build can be staged under Unity `Assets/` without launching Unity:

```bash
python3 tools/character-factory/character_factory.py stage-unity \
  path/to/manifest.json \
  --assets-root Assets/Generated/CharacterFactory
```

Or build and stage in one command:

```bash
python3 tools/character-factory/character_factory.py build path/to/spec.json \
  --unity-assets-root Assets/Generated/CharacterFactory
```

Staging copies the generated FBX to:

```text
Assets/Generated/CharacterFactory/<assetType>/<id>/
```

and writes a portable `*.characterfactory.json` descriptor beside it. The Unity Editor importer consumes that descriptor during normal asset import:

- clothing/weapons/accessories create or update `CharacterPartAsset` entries and the shared `CharacterPartCatalogue`;
- character bodies create or update a prefab containing the generated model and a configured `CharacterEquipmentController` wired to the shared catalogue.

The Character Factory CLI itself never launches Unity.

## Manifest

Every completed build writes `manifest.json` containing the actual generator backend, pipeline, output FBX, generator/prepare commands, and runtime-part metadata. TripoSR manifests do not claim Hunyuan model metadata.

Clothing uses `SkinnedToCharacterSkeleton`; rigid weapons/accessories use `BoneSocket`.

## Current limitations

The mechanics are now end-to-end validated, but production fitting and art quality still need work. Current body/garment alignment is global rather than semantic or landmark-driven. Remaining quality work includes realistic proportions, faces/fingers/hair, loose-garment conforming, collision/poke-through correction, body-region hiding, LOD generation, weapon-grip inference, and accessory-mount inference.

Accessories are currently rigid/socket-mounted. Skinned hair and capes should use the clothing path until a dedicated skinned-accessory mode exists.

## Commands

Dry-run a spec:

```bash
python3 tools/character-factory/character_factory.py build \
  tools/character-factory/examples/cleric_character.json --dry-run
```

Build all specs in a directory:

```bash
python3 tools/character-factory/character_factory.py batch path/to/specs
```

Run factory tests:

```bash
python3 -m unittest discover -s tools/character-factory/tests -p 'test_*.py' -v
```
