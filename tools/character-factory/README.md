# Character Factory

Headless local asset factory for modular characters. Character bodies, clothing, weapons, and
accessories are deliberately separate products; a dressed character is assembled at runtime rather
than baked into one generated mesh.

## Boundary layout

The Unity side follows the engine subsystem rule:

```text
Assets/VoxelEngine/Characters/
  Api/       stable equipment slots, part kinds, and ICharacterEquipment
  Runtime/   CharacterPartCatalogue, CharacterPartAsset, skeleton rebinding/socket attachment
```

Consumers depend on `VoxelEngine.Characters.Api`; concrete Unity implementation stays in
`VoxelEngine.Characters.Runtime`. `CharacterPartKind` is stable API, while prefab/catalogue mechanics
remain Runtime-owned.

The offline factory mirrors that separation and has four explicit pipelines:

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

## Generator backends and presets

The product pipeline and mesh generator are independent choices. Current CI uses **TripoSR on Apple
MPS** as the fast end-to-end smoke backend because it reconstructs a single input image quickly on
the self-hosted Apple-Silicon runner. The TripoSR adapter also bakes UV color, harmonizes the inferred
palette toward the isolated source, and can project source pixels onto high-confidence aligned
surfaces while retaining generated texture on hidden/side surfaces.

Official Hunyuan/PyTorch remains available as the higher-quality path. The Hunyuan presets are:

```text
smoke
  tencent/Hunyuan3D-2mini
  hunyuan3d-dit-v2-mini-turbo
  FlashVDM enabled
  5 diffusion steps
  octree resolution 64

quality
  tencent/Hunyuan3D-2mv
  multiview
  50 diffusion steps
  octree resolution 380
```

Swapping the generator does not change the `character`, `clothing`, `weapon`, or `accessory` pipeline
classes. The fast CI path currently chooses `backend: triposr-mps` explicitly; production-quality
passes can choose Hunyuan instead.

## Pipeline behavior

All four pipelines share only the image-to-mesh backend boundary. Their post-processing remains
asset-specific:

```text
character
  image(s) -> generator -> generated body GLB
           -> infer generated/canonical axis mapping
           -> align body to canonical bounds
           -> transfer canonical body weights by group name
           -> canonical armature + generated skinned meshes
           -> character FBX

clothing
  image(s) -> generator -> garment GLB
           -> Blender clothing pass
           -> transfer canonical body weights / canonical armature
           -> independent clothing FBX

weapon
  image(s) -> generator -> rigid GLB
           -> Blender rigid weapon pass
           -> weapon FBX + hand/socket metadata

accessory
  image(s) -> generator -> rigid GLB
           -> Blender rigid accessory pass
           -> accessory FBX + bone/socket metadata
```

Character alignment handles generator axis permutation, scale, center, and sufficiently confident
axis flips before skinning. The weight-transfer stage pre-creates the canonical donor's vertex-group
layout on each generated mesh, transfers all groups by name, and verifies that vertices actually
received weights before export.

The manifest records the selected generator preset/model plus the exact pipeline, output FBX,
generator/prepare commands, and runtime-part metadata. Clothing is marked
`SkinnedToCharacterSkeleton`; weapons/accessories are marked `BoneSocket`.

## CI acceptance smoke

The Apple-Silicon workflow currently exercises two real neural-generation paths:

```text
weapon smoke
  Sunlit Cleric staff fixture
    -> TripoSR MPS
    -> textured GLB
    -> rigid weapon Blender pass
    -> detailed ornament + procedural shaft assembly
    -> FBX re-import/material render

character smoke
  deterministic rigged T-pose mannequin fixture
    -> rendered front image
    -> TripoSR MPS reconstruction
    -> generated textured GLB
    -> automatic canonical alignment
    -> canonical weight transfer
    -> 17-bone skinned FBX
    -> re-import
    -> programmatic RightUpperArm pose
    -> require visible generated-mesh deformation
```

The character smoke is deliberately a simple procedural mannequin: it proves that a **generated**
mesh can survive image-to-3D reconstruction, canonical alignment, weight transfer, FBX export/import,
and actual skeletal deformation. It is not a production character-quality benchmark.

## Current limitations

Character mechanics are now end-to-end validated, but robust production fitting is still incomplete.
The generated-body aligner is global rather than semantic/landmark-driven, so realistic humanoid
proportions, fingers, faces, hair, and unusual silhouettes need stronger fitting. Clothing still lacks
its own garment-aware alignment/conforming pass and has not yet been accepted by a real generated
skinned-garment CI smoke.

Automatic loose-garment fitting, collision/poke-through correction, body-region hiding, LODs, weapon
grip inference, accessory mount inference, and automatic Unity prefab/`CharacterPartAsset` creation
remain follow-up stages. Accessories are currently rigid/socket-mounted; skinned hair/capes should
use the clothing pipeline until a dedicated skinned-accessory mode exists.

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

Run the routing/spec/alignment tests:

```bash
python3 -m unittest discover -s tools/character-factory/tests -p 'test_*.py' -v
```

Every build writes a `manifest.json` beside generated assets so later import/resume stages can be
added without changing the public factory API.
