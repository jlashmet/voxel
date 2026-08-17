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

## Build vs production

`build` is the low-level mesh primitive. It resolves the generator backend/profile, bootstraps it when necessary, runs generation, and runs the preparation pipeline selected by `assetType`.

`produce` is the standard image-to-game-asset lifecycle. It calls `build`, then routes appearance handling, verification, proof rendering, and optional Unity staging through the asset type plus its declared appearance strategy:

```text
character
  build -> character appearance -> skin/animation gates -> bind + Idle proof

clothing
  build -> garment appearance -> skin/deformation gate -> proof

weapon/accessory
  build -> rigid appearance -> rigid mesh gate -> proof
```

Generate one production asset:

```bash
python3 tools/character-factory/character_factory.py produce path/to/asset.json
```

Generate an asset library recursively:

```bash
python3 tools/character-factory/character_factory.py produce-batch \
  tools/character-factory/production-assets
```

Only JSON objects containing both `id` and `assetType` are discovered. Generated `manifest.json` and `*.characterfactory.json` files are ignored.

The scalable production plan and target reference-library layout are documented in `docs/character-factory-generation-framework-plan.md`.

## Reference sets

New production assets should separate geometry, appearance, and optional detail references instead of overloading one image set for every stage:

```json
{
  "references": {
    "geometry": { "directory": "geometry" },
    "appearance": { "directory": "appearance" },
    "details": {
      "face": "details/face.png"
    }
  }
}
```

A reference directory discovers canonical `front`, `back`, `left`, and `right` PNG/JPEG files. Explicit per-view paths remain available as overrides. Geometry and appearance can point at different turnarounds, which lets reconstruction use cleaned/modeling-safe images while appearance/identity work keeps higher-fidelity sources. Named details are arbitrary validated images such as `face`, `hands`, `ornament`, `material`, or `fit`.

Legacy top-level `views` remain supported during migration.

## Appearance strategies

Appearance selection is independent from the mesh generator. The top-level `appearance.strategy` chooses how references are applied after the prepared FBX exists:

```json
{
  "appearance": {
    "strategy": "garment-multiview"
  }
}
```

Current strategies:

```text
character-multiview
  character only
  uses the body/T-pose projection policy, including the outer-arm side-view redirect

garment-multiview
  clothing only
  uses local surface orientation and intentionally does not inherit character arm heuristics

rigid-multiview
  weapon/accessory only
  uses rigid/object surface orientation and requires an armature-free prepared FBX

preserve-generator
  any asset type
  keeps the generator-provided UV/material appearance unchanged
```

All multiview strategies require complete front/back/left/right appearance references. That is checked before backend bootstrap or geometry generation. Invalid combinations such as `weapon + character-multiview` fail while loading the spec.

The character, garment, and rigid policies share mask/atlas/UV mechanics, not shape-specific selection rules. This is deliberate: improvements to atlas padding or image handling can remain common while body, garment, and rigid visibility/orientation policy evolves separately.

Current garment/rigid multiview support establishes the production mechanics; it is not yet an art-quality claim. Garments still need semantic body fit, depth/occlusion, poke-through, and seam validation. Rigid equipment still needs stronger canonical orientation, disconnected-component masking, seam coverage, grip-axis inference, and scale validation.

## Generator backends and profiles

Product pipeline and mesh generator are independent choices. Low-level explicit backend configuration remains supported, but production assets should normally use a named profile so machine/environment details are not copied into every asset.

Current named profiles:

```text
hunyuan-quality-macos
  backend: hunyuan-pytorch
  model: tencent/Hunyuan3D-2mv / hunyuan3d-dit-v2-mv-turbo
  pinned Hunyuan source revision
  cached Python environment + multiview checkpoint
  production defaults: 5 steps, octree 256, 16000 chunks

hunyuan-smoke-macos
  backend: hunyuan-pytorch
  model: tencent/Hunyuan3D-2mini / hunyuan3d-dit-v2-mini-turbo
  pinned Hunyuan source revision
  cached Python environment + mini checkpoint

triposr-smoke-macos
  backend: triposr-mps
  pinned TripoSR source revision
  cached Python 3.12 environment + TripoSR/DINO weights
  production default: mcResolution 192
```

List them with:

```bash
python3 tools/character-factory/character_factory.py profiles
```

A production spec normally needs only the profile plus asset-specific generation choices:

```json
{
  "generator": {
    "profile": "hunyuan-quality-macos",
    "seed": 31827,
    "removeBackground": true
  }
}
```

The profile owns `backend`, `python`/interpreter, source checkout, weights, source revision, and bootstrap script. Those fields cannot be overridden by an asset using a profile. Asset-specific knobs such as seed, steps, resolution, chunking, model/subfolder selection, and background handling may override profile defaults.

Both `build` and `produce` automatically bootstrap a missing profile environment. If custom preprocessing needs the same managed Python runtime before generation, use:

```bash
python3 tools/character-factory/character_factory.py \
  bootstrap-profile triposr-smoke-macos
```

The final output line is the managed Python executable path. Transitional art stages can therefore share the managed environment without reintroducing cache/revision logic.

Current raw backends remain:

- `triposr-mps`: fast Apple-Silicon smoke/prototyping path.
- `hunyuan-pytorch`: higher-quality Hunyuan path, including multiview generation.

Swapping generators does not change the `character`, `clothing`, `weapon`, or `accessory` pipeline contract, and it does not choose the appearance strategy.

## Production examples

The Sunlit Cleric family is the migration fixture for the generic production system:

```bash
# character
bash tools/character-factory/production/sunlit-cleric/build_macos.sh

# separate swappable robe using garment-multiview
bash tools/character-factory/production/sunlit-cleric/build_robe_macos.sh

# separate staff; generator environment is profile-managed while the
# ornament + procedural-shaft composition is still a transitional custom stage
bash tools/character-factory/production/sunlit-cleric/build_staff_macos.sh
```

The robe entrypoint derives clothing-only four-view references, creates a canonical `GarmentDonor`, then calls the normal `produce` lifecycle. Its final FBX is staged as a `Torso` part using `SkinnedToCharacterSkeleton` rather than being baked into the body.

## Accepted mechanics

The Apple-Silicon Character Factory smoke validates these mechanics with real TripoSR inference:

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

A separate Blender-only appearance smoke exercises `character`, `garment`, and `rigid` multiview policies without running an image-to-3D model. This guards the Blender integration that Python dry-runs cannot execute.

These fixtures validate mechanics, not production art quality.

## Unity staging and automatic import

A completed build can be staged under Unity `Assets/` without launching Unity:

```bash
python3 tools/character-factory/character_factory.py stage-unity \
  path/to/manifest.json \
  --assets-root Assets/Generated/CharacterFactory
```

For the standard production lifecycle, use:

```bash
python3 tools/character-factory/character_factory.py produce path/to/spec.json \
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

Every completed build writes `manifest.json` containing the actual generator backend, selected named profile (when used), pinned source revision, resolved generation parameters, preparation pipeline, declared `appearanceStrategy`, output FBX, reference sets, generator/prepare/bootstrap commands, and runtime-part metadata.

`produce` extends the same manifest with a `production` section describing the selected appearance strategy/profile, atlas when projected, verification gates, proof images, reference audit, and exact production commands. This keeps the final artifact reproducible without requiring local environment paths in every source asset spec.

Clothing uses `SkinnedToCharacterSkeleton`; rigid weapons/accessories use `BoneSocket`.

## Integration validation

After synchronizing the feature branch with `master`, the Character Factory workflows must pass on the resulting feature head before the branch is merged into `master`.

## Current limitations

The mechanics are end-to-end validated, but production fitting and art quality still need work. Current body/garment alignment is global rather than semantic or landmark-driven. Remaining quality work includes realistic proportions, faces/fingers/hair, visibility-aware character projection, loose-garment conforming, collision/poke-through correction, body-region hiding, LOD generation, rigid orientation normalization, weapon-grip inference, and accessory-mount inference.

The generic production layer does not yet eliminate every production-specific art stage. Madeline still has custom body-only reference cleanup and face-identity transfer, and the Sun Staff still has an ornament/procedural-shaft composition stage. Their generator environments are profile-managed now; the remaining bespoke art operations become declared reusable stages before those scripts disappear entirely.

Accessories are currently rigid/socket-mounted. Skinned hair and capes should use the clothing path until a dedicated skinned-accessory mode exists.

## Commands

Dry-run a low-level spec:

```bash
python3 tools/character-factory/character_factory.py build \
  tools/character-factory/examples/cleric_character.json --dry-run
```

Dry-run the production lifecycle:

```bash
python3 tools/character-factory/character_factory.py produce \
  tools/character-factory/examples/cleric_character.json --dry-run
```

Build all specs in one directory:

```bash
python3 tools/character-factory/character_factory.py batch path/to/specs
```

Produce a recursive asset library:

```bash
python3 tools/character-factory/character_factory.py produce-batch path/to/asset-library
```

Run factory tests:

```bash
python3 -m unittest discover -s tools/character-factory/tests -p 'test_*.py' -v
```
