# Character Factory production asset library

This directory is the convention-driven source library for generated characters, clothing, weapons, and accessories.

The intended workflow is **asset data + reference images**, not one production script per named asset.

## Create an asset

Use the scaffold command rather than hand-writing the directory/spec contract.

Rigid weapon example:

```bash
python3 tools/character-factory/init_asset.py \
  weapon guard_sword_01 \
  --tag castle \
  --tag guard \
  --tag sword
```

Character example:

```bash
python3 tools/character-factory/init_asset.py \
  character steven_01 \
  --canonical-body /path/to/canonical_female.glb \
  --tag main-cast \
  --tag kentridge
```

Clothing example:

```bash
python3 tools/character-factory/init_asset.py \
  clothing guard_tunic_01 \
  --canonical-body /path/to/canonical_female_with_garment_donor.glb \
  --tag castle \
  --tag guard
```

The scaffold refuses to overwrite an existing `asset.json` unless `--force` is supplied. Existing reference files are never deleted by the scaffold.

## Directory convention

A character or garment scaffold creates:

```text
production-assets/
  characters|clothing/
    <id>/
      asset.json
      geometry/
      appearance/
      details/
```

A rigid weapon/accessory defaults to `preserve-generator`, so it initially creates:

```text
production-assets/
  weapons|accessories/
    <id>/
      asset.json
      geometry/
      details/
```

Use canonical names for turnaround images:

```text
front.png|jpg
back.png|jpg
left.png|jpg
right.png|jpg
```

`character-multiview`, `garment-multiview`, and `rigid-multiview` require all four appearance views. `preserve-generator` does not.

Geometry and appearance references are intentionally separate. Reconstruction can use cleaned/modeling-safe images while texture/identity projection uses the higher-fidelity appearance set. Named detail images such as `face`, `hands`, `ornament`, `material`, or `fit` belong under `details/` and are declared in `asset.json`.

## Produce one asset

```bash
python3 tools/character-factory/character_factory.py produce \
  tools/character-factory/production-assets/weapons/guard_sword_01/asset.json
```

## Produce a library

```bash
python3 tools/character-factory/character_factory.py produce-batch \
  tools/character-factory/production-assets
```

Filters can be combined:

```bash
python3 tools/character-factory/character_factory.py produce-batch \
  tools/character-factory/production-assets \
  --type weapon \
  --tag castle \
  --tag guard
```

Repeated tags use AND semantics in the example above: an asset must have both `castle` and `guard`.

## Incremental production

Keep the last successful catalogue snapshot outside the source asset directories, for example:

```bash
STATE=Artifacts/CharacterFactoryProductionState/catalogue.json
```

First snapshot:

```bash
python3 tools/character-factory/character_factory.py catalogue \
  tools/character-factory/production-assets \
  --output "$STATE"
```

Subsequent changed-only production:

```bash
python3 tools/character-factory/character_factory.py produce-batch \
  tools/character-factory/production-assets \
  --changed-from "$STATE" \
  --catalogue-output "$STATE"
```

The next catalogue is written atomically after a successful batch (including a no-change batch).

Change classes are:

```text
new
spec
geometry
appearance
details
```

They can be filtered when useful:

```bash
python3 tools/character-factory/character_factory.py produce-batch \
  tools/character-factory/production-assets \
  --changed-from "$STATE" \
  --change-kind appearance \
  --tag main-cast \
  --catalogue-output "$STATE"
```

## Geometry reuse

Prepared geometry is cached independently from appearance/detail work. Its fingerprint includes geometry-reference bytes, generator/profile/revision/command, canonical donor content, preparation command/code, and relevant alignment configuration.

Appearance and detail image bytes are intentionally excluded from that geometry fingerprint. Therefore an appearance-only or detail-only rebuild can restore the prepared FBX from the persistent Character Factory geometry cache **before backend bootstrap**, skipping Hunyuan/TripoSR inference entirely.

A real geometry input/config/code change produces a different fingerprint and rebuilds geometry.

The final `manifest.json` records the geometry-cache fingerprint and whether the current build was a cache hit. The catalogue records the last-known final FBX/proof hashes and cache state when build artifacts exist.

## GitHub production workflow

The generic workflow `.github/workflows/character-factory-production.yml` can run the same selection model on the self-hosted Mac. It supports:

- asset-library directory;
- asset type;
- exact asset ID;
- catalogue tag;
- prior catalogue + change kind;
- optional Unity staging.

It uploads production artifacts/proofs and the next catalogue snapshot. This workflow is intended to replace per-character/per-item Actions workflows once its first end-to-end production proof is accepted.

## Current quality gates

Fast framework CI validates the data/routing/cache/catalogue contracts. Blender-specific appearance, rigid canonicalization, and strengthened skin-weight coverage have a dedicated self-hosted smoke workflow.

Do not treat a fast/dry-run green result as an art-quality acceptance. Character projection quality, garment fit/poke-through, rigid seams/orientation, and final visual proofs remain separate gates.
