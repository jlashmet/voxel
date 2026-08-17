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

`produce` is the standard image-to-game-asset lifecycle. It calls `build`, then routes appearance handling, verification, proof rendering, and optional Unity staging through an asset-type production profile:

```text
character
  build -> multiview appearance when supported -> skin/animation gates -> bind + Idle proof

clothing
  build -> garment production profile -> skin/deformation gate -> proof

weapon/accessory
  build -> rigid production profile -> rigid mesh gate -> proof
```

The current character multiview projector contains body/T-pose-specific heuristics, so clothing and rigid products intentionally preserve generator appearance until dedicated garment and rigid appearance profiles are implemented. We do not silently reuse a character-specific texture algorithm for unrelated asset shapes.

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

A production spec now only needs the profile plus asset-specific generation choices:

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

The final output line is the managed Python executable path. This is how bespoke transitional stages can share the profile without reintroducing cache/revision logic.

Current raw backends remain:

- `triposr-mps`: fast Apple-Silicon smoke/prototyping path. It reconstructs a single image, bakes UV color, harmonizes source palette, and can project source pixels onto confidently aligned surfaces.
- `hunyuan-pytorch`: higher-quality Hunyuan path, including multiview generation.

Swapping generators does not change the `character`, `clothing`, `weapon`, or `accessory` pipeline contract.

## Accepted mechanics

The Apple-Silicon CI smoke validates these mechanics with real TripoSR inference:

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

Every completed build writes `manifest.json` containing the actual generator backend, selected named profile (when used), pinned source revision, resolved generation parameters, pipeline, output FBX, reference sets, generator/prepare/bootstrap commands, and runtime-part metadata. TripoSR manifests do not claim Hunyuan model metadata.

`produce` extends the same manifest with a `production` section describing the selected appearance mode, verification gates, proof images, reference audit, and exact production commands. This keeps the final artifact reproducible without requiring local environment paths in every source asset spec.

Clothing uses `SkinnedToCharacterSkeleton`; rigid weapons/accessories use `BoneSocket`.

## Integration validation

After synchronizing the feature branch with `master`, the Character Factory workflow must pass on the resulting feature head before the branch is merged into `master`.

## Current limitations

The mechanics are end-to-end validated, but production fitting and art quality still need work. Current body/garment alignment is global rather than semantic or landmark-driven. Remaining quality work includes realistic proportions, faces/fingers/hair, loose-garment conforming, collision/poke-through correction, body-region hiding, LOD generation, weapon-grip inference, and accessory-mount inference.

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
